using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BAStudio.SceneDependency
{
    public static class SceneDependencyRuntime
    {
        public const string ConfigAddressPrefix = "sd:";

        static Dictionary<string, AsyncOperationHandle<SceneInstance>> loadedSceneHandles =
            new Dictionary<string, AsyncOperationHandle<SceneInstance>>();
        static Dictionary<string, Task> inFlightLoads =
            new Dictionary<string, Task>();

        static Dictionary<string, SceneDependency> configCache =
            new Dictionary<string, SceneDependency>();
#if !UNITY_EDITOR
        static Dictionary<string, AsyncOperationHandle<SceneDependency>> configHandles =
            new Dictionary<string, AsyncOperationHandle<SceneDependency>>();
        static Dictionary<string, Task<SceneDependency>> inFlightConfigLoads =
            new Dictionary<string, Task<SceneDependency>>();
#endif
#if UNITY_EDITOR
        static Dictionary<string, SceneDependency> editorLookup;
        static HashSet<string> editorNegativeCache = new HashSet<string>();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState()
        {
            loadedSceneHandles = new Dictionary<string, AsyncOperationHandle<SceneInstance>>();
            inFlightLoads = new Dictionary<string, Task>();
#if !UNITY_EDITOR
            foreach (var kvp in configHandles)
            {
                if (kvp.Value.IsValid())
                    Addressables.Release(kvp.Value);
            }
            configHandles = new Dictionary<string, AsyncOperationHandle<SceneDependency>>();
            inFlightConfigLoads = new Dictionary<string, Task<SceneDependency>>();
#endif
#if UNITY_EDITOR
            editorLookup = null;
            editorNegativeCache = new HashSet<string>();
#endif
            configCache = new Dictionary<string, SceneDependency>();
        }

        // --- Config loading (replaces central index) ---

        internal static Task<SceneDependency> TryLoadConfigAsync(string sceneGUID)
        {
            if (configCache.TryGetValue(sceneGUID, out var cached))
                return Task.FromResult(cached);

#if UNITY_EDITOR
            return Task.FromResult(TryLoadConfigEditor(sceneGUID));
#else
            return TryLoadConfigRuntime(sceneGUID);
#endif
        }

#if !UNITY_EDITOR
        static async Task<SceneDependency> TryLoadConfigRuntime(string sceneGUID)
        {
            if (inFlightConfigLoads.TryGetValue(sceneGUID, out var inFlight))
                return await inFlight;

            var task = LoadConfigFromAddressables(sceneGUID);
            inFlightConfigLoads[sceneGUID] = task;
            try
            {
                return await task;
            }
            finally
            {
                inFlightConfigLoads.Remove(sceneGUID);
            }
        }
#endif

        public static bool TryGetCachedConfig(string sceneGUID, out SceneDependency config)
        {
            return configCache.TryGetValue(sceneGUID, out config);
        }

#if UNITY_EDITOR
        static void BuildEditorLookup()
        {
            editorLookup = new Dictionary<string, SceneDependency>();
            var guids = AssetDatabase.FindAssets("t:SceneDependency");
            foreach (var assetGUID in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(assetGUID);
                var config = AssetDatabase.LoadAssetAtPath<SceneDependency>(path);
                if (config == null || config.subject == null || string.IsNullOrEmpty(config.subject.AssetGUID))
                    continue;

                var subjectGUID = config.subject.AssetGUID;
                if (editorLookup.ContainsKey(subjectGUID))
                {
                    Debug.LogWarning($"[SceneDependency] Duplicate config for scene GUID {subjectGUID} at {path}");
                    continue;
                }
                editorLookup[subjectGUID] = config;
            }
        }

        static SceneDependency TryLoadConfigEditor(string sceneGUID)
        {
            if (editorNegativeCache.Contains(sceneGUID))
                return null;

            if (editorLookup == null)
                BuildEditorLookup();

            if (editorLookup.TryGetValue(sceneGUID, out var config))
            {
                configCache[sceneGUID] = config;
                return config;
            }

            BuildEditorLookup();
            if (editorLookup.TryGetValue(sceneGUID, out config))
            {
                configCache[sceneGUID] = config;
                editorNegativeCache.Remove(sceneGUID);
                return config;
            }
            editorNegativeCache.Add(sceneGUID);
            return null;
        }
#endif

#if !UNITY_EDITOR
        static async Task<SceneDependency> LoadConfigFromAddressables(string sceneGUID)
        {
            try
            {
                var handle = Addressables.LoadAssetAsync<SceneDependency>(ConfigAddressPrefix + sceneGUID);
                var result = await AsyncOpToTask(handle);
                configCache[sceneGUID] = result;
                configHandles[sceneGUID] = handle;
                return result;
            }
            catch (Exception ex)
            {
                if (ex is InvalidKeyException || ex.InnerException is InvalidKeyException)
                    return null;
                Debug.LogWarning($"[SceneDependency] Failed to load config for GUID {sceneGUID}: {ex.Message}");
                return null;
            }
        }
#endif

        // --- Scene loading ---

        public static async Task<AsyncOperationHandle<SceneInstance>> LoadSceneAsync(
            AssetReference sceneRef, LoadSceneMode mode, bool reloadLoadedDep = false)
        {
            return await LoadSceneAsync(sceneRef.AssetGUID, mode, reloadLoadedDep);
        }

        public static async Task<AsyncOperationHandle<SceneInstance>> LoadSceneAsync(
            string sceneGUID, LoadSceneMode mode, bool reloadLoadedDep = false)
        {
            var deps = await TryLoadConfigAsync(sceneGUID);

            if (deps == null || deps.scenes == null || deps.scenes.Length == 0)
            {
                if (inFlightLoads.TryGetValue(sceneGUID, out var existing))
                {
                    await existing;
                    if (loadedSceneHandles.TryGetValue(sceneGUID, out var joined))
                        return joined;
                    throw new InvalidOperationException(
                        "[SceneDependency] Scene was unloaded before the awaiting caller could return: " + sceneGUID);
                }

                var directHandle = Addressables.LoadSceneAsync(sceneGUID, mode);
                loadedSceneHandles[sceneGUID] = directHandle;
                var loadTask = AsyncOpToTask(directHandle);
                inFlightLoads[sceneGUID] = loadTask;
                try
                {
                    await loadTask;
                }
                catch
                {
                    loadedSceneHandles.Remove(sceneGUID);
                    throw;
                }
                finally
                {
                    inFlightLoads.Remove(sceneGUID);
                }
                return directHandle;
            }

            var depGUIDs = await ResolveDependencyTreeAsync(deps);

            if (inFlightLoads.TryGetValue(sceneGUID, out var existingMasterLoad))
            {
                await existingMasterLoad;
                if (loadedSceneHandles.TryGetValue(sceneGUID, out var joined))
                    return joined;
                throw new InvalidOperationException(
                    "[SceneDependency] Scene was unloaded before the awaiting caller could return: " + sceneGUID);
            }

            var overallCompletion = new TaskCompletionSource<bool>();
            inFlightLoads[sceneGUID] = overallCompletion.Task;

            var handlesAllocatedThisCall = new List<string>();
            Scene? deferredSceneUnload = null;
            (string guid, AsyncOperationHandle<SceneInstance> handle)? deferredAddressableUnload = null;
            try
            {
                // Phase 1: Unload (Single mode)
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

                        if (loadedGUID != null && configCache.TryGetValue(loadedGUID, out var loadedConfig)
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
                    }
                    foreach (var scene in sceneManagerUnloads)
                    {
                        var op = SceneManager.UnloadSceneAsync(scene);
                        if (op != null)
                            unloadTasks.Add(AsyncOpToTask(op));
                    }
                    if (unloadTasks.Count > 0)
                        await Task.WhenAll(unloadTasks);
                    foreach (var (guid, handle) in addressableUnloads)
                        loadedSceneHandles.Remove(guid);
                }

                // Phase 2: Load dependencies in parallel
                var depLoadTasks = new List<Task>();
                foreach (var guid in depGUIDs)
                {
                    if (loadedSceneHandles.TryGetValue(guid, out var existing)
                        && existing.IsValid() && existing.IsDone
                        && existing.Status == AsyncOperationStatus.Succeeded)
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

                // Deferred unload
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
                if (loadedSceneHandles.TryGetValue(sceneGUID, out var prevMasterHandle) && prevMasterHandle.IsValid())
                    await AsyncOpToTask(Addressables.UnloadSceneAsync(prevMasterHandle));

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
                if (deferredAddressableUnload.HasValue)
                {
                    var (dGuid, dHandle) = deferredAddressableUnload.Value;
                    if (dHandle.IsValid())
                    {
                        try { cleanupTasks.Add(AsyncOpToTask(Addressables.UnloadSceneAsync(dHandle))); }
                        catch (Exception e) { Debug.LogException(e); }
                    }
                    loadedSceneHandles.Remove(dGuid);
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

        public static async Task<List<string>> ResolveDependencyTreeAsync(SceneDependency root)
        {
            HashSet<string> visited = new HashSet<string>();
            if (root.subject != null && !string.IsNullOrEmpty(root.subject.AssetGUID))
                visited.Add(root.subject.AssetGUID);
            List<string> result = new List<string>();
            await ResolveRequiredAsync(root, visited, result);
            LogDependencyOrder(result);
            return result;
        }

        static async Task ResolveRequiredAsync(SceneDependency subject, HashSet<string> visited, List<string> result)
        {
            if (subject.scenes == null) return;
            for (int i = 0; i < subject.scenes.Length; i++)
            {
                string guid = subject.scenes[i].AssetGUID;
                if (string.IsNullOrEmpty(guid) || visited.Contains(guid)) continue;
                visited.Add(guid);

                var subDeps = await TryLoadConfigAsync(guid);
                if (subDeps != null)
                    await ResolveRequiredAsync(subDeps, visited, result);

                result.Add(guid);
            }
        }

        public static List<string> ResolveDependencyTree(SceneDependency root, IReadOnlyDictionary<string, SceneDependency> configs)
        {
            HashSet<string> visited = new HashSet<string>();
            if (root.subject != null && !string.IsNullOrEmpty(root.subject.AssetGUID))
                visited.Add(root.subject.AssetGUID);
            List<string> result = new List<string>();
            ResolveRequired(root, configs, visited, result);
            LogDependencyOrder(result);
            return result;
        }

        static void ResolveRequired(SceneDependency subject, IReadOnlyDictionary<string, SceneDependency> configs,
            HashSet<string> visited, List<string> result)
        {
            if (subject.scenes == null) return;
            for (int i = 0; i < subject.scenes.Length; i++)
            {
                string guid = subject.scenes[i].AssetGUID;
                if (string.IsNullOrEmpty(guid) || visited.Contains(guid)) continue;
                visited.Add(guid);

                if (configs.TryGetValue(guid, out var subDeps) && subDeps != null)
                    ResolveRequired(subDeps, configs, visited, result);

                result.Add(guid);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        static void LogDependencyOrder(List<string> result)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[SceneDependency] Loading dependencies in order:");
            for (int i = 0; i < result.Count; i++)
                sb.AppendLine(result[i]);
            Debug.Log(sb.ToString());
        }

        // --- Async helpers ---

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
