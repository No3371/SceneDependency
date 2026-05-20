using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace BAStudio.SceneDependency
{
    public static class SceneDependencyRuntime
    {
        static Dictionary<string, AsyncOperationHandle<SceneInstance>> loadedSceneHandles =
            new Dictionary<string, AsyncOperationHandle<SceneInstance>>();
        static Dictionary<string, Task> inFlightLoads =
            new Dictionary<string, Task>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState()
        {
            loadedSceneHandles = new Dictionary<string, AsyncOperationHandle<SceneInstance>>();
            inFlightLoads = new Dictionary<string, Task>();
        }

        public static async Task<AsyncOperationHandle<SceneInstance>> LoadSceneAsync(
            AssetReference sceneRef, LoadSceneMode mode, bool reloadLoadedDep = false)
        {
            return await LoadSceneAsync(sceneRef.AssetGUID, mode, reloadLoadedDep);
        }

        public static async Task<AsyncOperationHandle<SceneInstance>> LoadSceneAsync(
            string sceneGUID, LoadSceneMode mode, bool reloadLoadedDep = false)
        {
            var index = await SceneDependencyIndex.EnsureInitializedAsync();
            if (index == null)
                throw new InvalidOperationException("[SceneDependency] Index not loaded. Ensure SceneDependencyIndex is available.");

            if (!index.TryGet(sceneGUID, out SceneDependency deps) || deps == null || deps.scenes.Length == 0)
            {
                if (inFlightLoads.TryGetValue(sceneGUID, out var existing))
                {
                    await existing;
                    return loadedSceneHandles[sceneGUID];
                }

                var directHandle = Addressables.LoadSceneAsync(sceneGUID, mode);
                loadedSceneHandles[sceneGUID] = directHandle;
                var loadTask = AsyncOpToTask(directHandle);
                inFlightLoads[sceneGUID] = loadTask;
                try
                {
                    await loadTask;
                }
                finally
                {
                    inFlightLoads.Remove(sceneGUID);
                }
                return directHandle;
            }

            var depGUIDs = ResolveDependencyTree(deps, index);

            if (inFlightLoads.TryGetValue(sceneGUID, out var existingMasterLoad))
            {
                await existingMasterLoad;
                return loadedSceneHandles[sceneGUID];
            }

            var overallCompletion = new TaskCompletionSource<bool>();
            inFlightLoads[sceneGUID] = overallCompletion.Task;

            var handlesAllocatedThisCall = new List<string>();
            Scene? deferredSceneUnload = null;
            (string guid, AsyncOperationHandle<SceneInstance> handle)? deferredAddressableUnload = null;
            try
            {
                // Phase 1: Unload (Single mode)
                // Collect targets first — if unloading all would leave zero loaded
                // scenes (which Unity forbids), defer one until after Phase 2.
                if (mode == LoadSceneMode.Single)
                {
                    var addressableUnloads = new List<(string guid, AsyncOperationHandle<SceneInstance> handle)>();
                    var sceneManagerUnloads = new List<Scene>();
                    int totalLoadedScenes = 0;

                    for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                    {
                        var scene = SceneManager.GetSceneAt(i);
                        if (!scene.IsValid() || !scene.isLoaded) continue;
                        if (scene.name == "DontDestroyOnLoad") continue;

                        totalLoadedScenes++;

                        string loadedGUID = FindGUIDForLoadedScene(scene);
                        if (loadedGUID == sceneGUID) continue;

                        bool isDep = loadedGUID != null && depGUIDs.Contains(loadedGUID);
                        if (isDep && !reloadLoadedDep) continue;

                        if (loadedGUID != null && index.TryGet(loadedGUID, out var loadedConfig)
                            && loadedConfig != null && loadedConfig.NoAutoUnloadInSingleLoadMode) continue;

                        if (loadedGUID != null && loadedSceneHandles.TryGetValue(loadedGUID, out var existingHandle))
                        {
                            addressableUnloads.Add((loadedGUID, existingHandle));
                        }
                        else
                        {
                            sceneManagerUnloads.Add(scene);
                        }
                    }

                    int survivingCount = totalLoadedScenes - addressableUnloads.Count - sceneManagerUnloads.Count;
                    if (survivingCount <= 0 && (addressableUnloads.Count + sceneManagerUnloads.Count) > 0)
                    {
                        if (sceneManagerUnloads.Count > 0)
                        {
                            deferredSceneUnload = sceneManagerUnloads[sceneManagerUnloads.Count - 1];
                            sceneManagerUnloads.RemoveAt(sceneManagerUnloads.Count - 1);
                        }
                        else
                        {
                            deferredAddressableUnload = addressableUnloads[addressableUnloads.Count - 1];
                            addressableUnloads.RemoveAt(addressableUnloads.Count - 1);
                        }
                    }

                    var unloadTasks = new List<Task>();
                    foreach (var (guid, handle) in addressableUnloads)
                    {
                        if (handle.IsValid())
                            unloadTasks.Add(AsyncOpToTask(Addressables.UnloadSceneAsync(handle)));
                        loadedSceneHandles.Remove(guid);
                    }
                    foreach (var scene in sceneManagerUnloads)
                    {
                        var op = SceneManager.UnloadSceneAsync(scene);
                        if (op != null)
                            unloadTasks.Add(AsyncOpToTask(op));
                    }
                    if (unloadTasks.Count > 0)
                        await Task.WhenAll(unloadTasks);
                }

                // Phase 2: Load dependencies in parallel
                var depLoadTasks = new List<Task>();
                foreach (var guid in depGUIDs)
                {
                    if (loadedSceneHandles.TryGetValue(guid, out var existing) && existing.IsValid())
                        continue;

                    if (inFlightLoads.TryGetValue(guid, out var inFlight))
                    {
                        depLoadTasks.Add(inFlight);
                        continue;
                    }

                    var depHandle = Addressables.LoadSceneAsync(guid, LoadSceneMode.Additive);
                    loadedSceneHandles[guid] = depHandle;
                    handlesAllocatedThisCall.Add(guid);
                    var loadTask = AsyncOpToTask(depHandle);
                    inFlightLoads[guid] = loadTask;
                    depLoadTasks.Add(loadTask);
                }
                if (depLoadTasks.Count > 0)
                    await Task.WhenAll(depLoadTasks);

                foreach (var guid in handlesAllocatedThisCall)
                    inFlightLoads.Remove(guid);

                // Deferred unload: now that new scenes are loaded, unload the scene
                // we kept alive to satisfy Unity's "cannot unload last scene" rule.
                if (deferredSceneUnload.HasValue || deferredAddressableUnload.HasValue)
                {
                    var deferredTasks = new List<Task>();
                    if (deferredSceneUnload.HasValue)
                    {
                        var ds = deferredSceneUnload.Value;
                        if (ds.IsValid() && ds.isLoaded)
                        {
                            var op = SceneManager.UnloadSceneAsync(ds);
                            if (op != null)
                                deferredTasks.Add(AsyncOpToTask(op));
                        }
                        deferredSceneUnload = null;
                    }
                    if (deferredAddressableUnload.HasValue)
                    {
                        var (guid, handle) = deferredAddressableUnload.Value;
                        if (handle.IsValid())
                            deferredTasks.Add(AsyncOpToTask(Addressables.UnloadSceneAsync(handle)));
                        loadedSceneHandles.Remove(guid);
                        deferredAddressableUnload = null;
                    }
                    if (deferredTasks.Count > 0)
                        await Task.WhenAll(deferredTasks);
                }

                // Phase 3: Load master scene
                var masterHandle = Addressables.LoadSceneAsync(sceneGUID, LoadSceneMode.Additive);
                loadedSceneHandles[sceneGUID] = masterHandle;
                handlesAllocatedThisCall.Add(sceneGUID);
                await AsyncOpToTask(masterHandle);

                // Phase 4: Callbacks and set active
                var masterScene = masterHandle.Result.Scene;
                var goCache = new List<GameObject>(32);
                foreach (var guid in depGUIDs)
                {
                    if (!loadedSceneHandles.TryGetValue(guid, out var dh) || !dh.IsValid()) continue;
                    if (dh.Status != AsyncOperationStatus.Succeeded) continue;
                    var depScene = dh.Result.Scene;
                    if (!depScene.IsValid() || !depScene.isLoaded) continue;
                    goCache.Clear();
                    depScene.GetRootGameObjects(goCache);
                    foreach (var go in goCache)
                        go.GetComponent<SceneDependencyProxy>()?.LoadedAsDep(masterScene.name, sceneGUID);
                }
                if (masterScene.IsValid())
                    SceneManager.SetActiveScene(masterScene);

                overallCompletion.SetResult(true);
                return masterHandle;
            }
            catch (Exception ex)
            {
                var cleanupTasks = new List<Task>();
                foreach (var guid in handlesAllocatedThisCall)
                {
                    inFlightLoads.Remove(guid);
                    if (loadedSceneHandles.TryGetValue(guid, out var h))
                    {
                        loadedSceneHandles.Remove(guid);
                        if (h.IsValid())
                        {
                            try { cleanupTasks.Add(AsyncOpToTask(Addressables.UnloadSceneAsync(h))); }
                            catch (Exception e) { Debug.LogException(e); }
                        }
                    }
                }
                if (cleanupTasks.Count > 0)
                {
                    try { await Task.WhenAll(cleanupTasks); }
                    catch (Exception e) { Debug.LogException(e); }
                }
                overallCompletion.TrySetException(ex);
                throw;
            }
            finally
            {
                inFlightLoads.Remove(sceneGUID);
            }
        }

        public static async Task UnloadSceneAsync(string sceneGUID)
        {
            if (inFlightLoads.TryGetValue(sceneGUID, out var inFlight))
            {
                try { await inFlight; }
                catch { }
            }

            if (loadedSceneHandles.TryGetValue(sceneGUID, out var handle))
            {
                loadedSceneHandles.Remove(sceneGUID);
                if (handle.IsValid())
                    await AsyncOpToTask(Addressables.UnloadSceneAsync(handle));
            }
        }

        public static async Task UnloadSceneAsync(AssetReference sceneRef)
        {
            await UnloadSceneAsync(sceneRef.AssetGUID);
        }

        // --- Dependency resolution ---

        public static List<string> ResolveDependencyTree(SceneDependency root, SceneDependencyIndex index = null)
        {
            if (index == null) index = SceneDependencyIndex.AutoInstance;
            HashSet<string> visited = new HashSet<string>();
            if (root.subject != null && !string.IsNullOrEmpty(root.subject.AssetGUID))
                visited.Add(root.subject.AssetGUID);
            List<string> result = new List<string>();
            ResolveRequired(root, index, visited, result);
#if UNITY_EDITOR
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[SceneDependency] Loading dependencies in order:");
            for (int i = 0; i < result.Count; i++)
                sb.AppendLine(result[i]);
            Debug.Log(sb.ToString());
#endif
            return result;
        }

        static void ResolveRequired(SceneDependency subject, SceneDependencyIndex index, HashSet<string> visited, List<string> result)
        {
            if (subject.scenes == null) return;
            for (int i = 0; i < subject.scenes.Length; i++)
            {
                string guid = subject.scenes[i].AssetGUID;
                if (string.IsNullOrEmpty(guid) || visited.Contains(guid)) continue;
                visited.Add(guid);

                if (index != null && index.TryGet(guid, out SceneDependency subDeps) && subDeps != null)
                {
                    ResolveRequired(subDeps, index, visited, result);
                }
                result.Add(guid);
            }
        }

        // --- Async helpers (compatible with all Addressables versions) ---

        internal static Task<T> AsyncOpToTask<T>(AsyncOperationHandle<T> handle)
        {
            if (handle.IsDone)
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                    return Task.FromResult(handle.Result);
                return Task.FromException<T>(handle.OperationException ??
                    new Exception("[SceneDependency] Addressables operation failed: " + handle.Status));
            }
            var tcs = new TaskCompletionSource<T>();
            handle.Completed += h =>
            {
                if (h.Status == AsyncOperationStatus.Succeeded)
                    tcs.SetResult(h.Result);
                else
                    tcs.SetException(h.OperationException ??
                        new Exception("[SceneDependency] Addressables operation failed: " + h.Status));
            };
            return tcs.Task;
        }

        static Task AsyncOpToTask(AsyncOperationHandle handle)
        {
            if (handle.IsDone)
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                    return Task.CompletedTask;
                return Task.FromException(handle.OperationException ??
                    new Exception("[SceneDependency] Addressables operation failed: " + handle.Status));
            }
            var tcs = new TaskCompletionSource<bool>();
            handle.Completed += h =>
            {
                if (h.Status == AsyncOperationStatus.Succeeded)
                    tcs.SetResult(true);
                else
                    tcs.SetException(h.OperationException ??
                        new Exception("[SceneDependency] Addressables operation failed: " + h.Status));
            };
            return tcs.Task;
        }

        static Task AsyncOpToTask(AsyncOperation op)
        {
            if (op.isDone) return Task.CompletedTask;
            var tcs = new TaskCompletionSource<bool>();
            op.completed += _ => tcs.SetResult(true);
            return tcs.Task;
        }

        static string FindGUIDForLoadedScene(Scene scene)
        {
            foreach (var kvp in loadedSceneHandles)
            {
                if (kvp.Value.IsValid() && kvp.Value.IsDone
                    && kvp.Value.Status == AsyncOperationStatus.Succeeded
                    && kvp.Value.Result.Scene == scene)
                    return kvp.Key;
            }
            return null;
        }
    }
}
