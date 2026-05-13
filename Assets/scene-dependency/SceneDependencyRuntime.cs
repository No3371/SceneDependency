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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState()
        {
            loadedSceneHandles = new Dictionary<string, AsyncOperationHandle<SceneInstance>>();
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
                var directHandle = Addressables.LoadSceneAsync(sceneGUID, mode);
                loadedSceneHandles[sceneGUID] = directHandle;
                await AsyncOpToTask(directHandle);
                return directHandle;
            }

            var depGUIDs = ResolveDependencyTree(deps, index);

            // Phase 1: Unload (Single mode)
            if (mode == LoadSceneMode.Single)
            {
                var unloadTasks = new List<Task>();
                for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (!scene.IsValid() || !scene.isLoaded) continue;
                    if (scene.name == "DontDestroyOnLoad") continue;

                    string loadedGUID = FindGUIDForLoadedScene(scene);
                    if (loadedGUID == sceneGUID) continue;

                    bool isDep = loadedGUID != null && depGUIDs.Contains(loadedGUID);
                    if (isDep && !reloadLoadedDep) continue;

                    if (loadedGUID != null && index.TryGet(loadedGUID, out var loadedConfig)
                        && loadedConfig != null && loadedConfig.NoAutoUnloadInSingleLoadMode) continue;

                    if (loadedGUID != null && loadedSceneHandles.TryGetValue(loadedGUID, out var existingHandle))
                    {
                        if (existingHandle.IsValid())
                            unloadTasks.Add(AsyncOpToTask(Addressables.UnloadSceneAsync(existingHandle)));
                        loadedSceneHandles.Remove(loadedGUID);
                    }
                    else
                    {
                        var op = SceneManager.UnloadSceneAsync(scene);
                        if (op != null)
                            unloadTasks.Add(AsyncOpToTask(op));
                    }
                }
                if (unloadTasks.Count > 0)
                    await Task.WhenAll(unloadTasks);
            }

            var handlesAllocatedThisCall = new List<string>();
            try
            {
                // Phase 2: Load dependencies in parallel
                var depLoadTasks = new List<Task>();
                foreach (var guid in depGUIDs)
                {
                    if (loadedSceneHandles.ContainsKey(guid) && loadedSceneHandles[guid].IsValid())
                        continue;

                    var depHandle = Addressables.LoadSceneAsync(guid, LoadSceneMode.Additive);
                    loadedSceneHandles[guid] = depHandle;
                    handlesAllocatedThisCall.Add(guid);
                    depLoadTasks.Add(AsyncOpToTask(depHandle));
                }
                if (depLoadTasks.Count > 0)
                    await Task.WhenAll(depLoadTasks);

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
                    var depScene = dh.Result.Scene;
                    if (!depScene.IsValid() || !depScene.isLoaded) continue;
                    goCache.Clear();
                    depScene.GetRootGameObjects(goCache);
                    foreach (var go in goCache)
                    {
                        if (go == null) continue;
                        go.GetComponent<SceneDependencyProxy>()?.LoadedAsDep(masterScene.name, sceneGUID);
                    }
                }
                if (masterScene.IsValid())
                    SceneManager.SetActiveScene(masterScene);

                return masterHandle;
            }
            catch
            {
                foreach (var guid in handlesAllocatedThisCall)
                {
                    if (loadedSceneHandles.TryGetValue(guid, out var h) && h.IsValid())
                    {
                        try { Addressables.UnloadSceneAsync(h); }
                        catch (Exception e) { Debug.LogException(e); }
                    }
                    loadedSceneHandles.Remove(guid);
                }
                throw;
            }
        }

        public static async Task UnloadSceneAsync(string sceneGUID)
        {
            if (loadedSceneHandles.TryGetValue(sceneGUID, out var handle) && handle.IsValid())
            {
                await AsyncOpToTask(Addressables.UnloadSceneAsync(handle));
                loadedSceneHandles.Remove(sceneGUID);
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

        static Task<T> AsyncOpToTask<T>(AsyncOperationHandle<T> handle)
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
                if (kvp.Value.IsValid() && kvp.Value.IsDone && kvp.Value.Result.Scene == scene)
                    return kvp.Key;
            }
            return null;
        }
    }
}
