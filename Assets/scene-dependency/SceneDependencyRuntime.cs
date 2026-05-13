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
        static Scene lastLoadedScene;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            loadedSceneHandles = new Dictionary<string, AsyncOperationHandle<SceneInstance>>();
            lastLoadedScene = default;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (SceneDependencyIndex.AutoInstance == null)
                Debug.Log("[SceneDependency] Index not yet loaded.");
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            lastLoadedScene = scene;
        }

        public static async Task<AsyncOperationHandle<SceneInstance>> LoadSceneAsync(
            AssetReference sceneRef, LoadSceneMode mode, bool reloadLoadedDep = false)
        {
            return await LoadSceneAsync(sceneRef.AssetGUID, mode, reloadLoadedDep);
        }

        public static async Task<AsyncOperationHandle<SceneInstance>> LoadSceneAsync(
            string sceneGUID, LoadSceneMode mode, bool reloadLoadedDep = false)
        {
            var index = SceneDependencyIndex.AutoInstance;
            if (index == null)
                throw new InvalidOperationException("[SceneDependency] Index not loaded. Ensure SceneDependencyIndex is available.");

            if (!index.TryGet(sceneGUID, out SceneDependency deps) || deps == null || deps.scenes.Length == 0)
            {
                var directHandle = Addressables.LoadSceneAsync(sceneGUID, mode);
                loadedSceneHandles[sceneGUID] = directHandle;
                await directHandle.Task;
                return directHandle;
            }

            var depGUIDs = ResolveDependencyTree(deps);

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
                    if (isDep && !reloadLoadedDep)
                    {
                        if (loadedGUID != null && index.TryGet(loadedGUID, out var depConfig)
                            && depConfig != null && depConfig.NoAutoUnloadInSingleLoadMode) continue;
                        continue;
                    }

                    if (loadedGUID != null && index.TryGet(loadedGUID, out var loadedConfig)
                        && loadedConfig != null && loadedConfig.NoAutoUnloadInSingleLoadMode) continue;

                    if (loadedGUID != null && loadedSceneHandles.TryGetValue(loadedGUID, out var existingHandle))
                    {
                        if (existingHandle.IsValid())
                            unloadTasks.Add(Addressables.UnloadSceneAsync(existingHandle).Task);
                        loadedSceneHandles.Remove(loadedGUID);
                    }
                    else
                    {
                        var op = SceneManager.UnloadSceneAsync(scene);
                        if (op != null)
                            unloadTasks.Add(ToTask(op));
                    }
                }
                if (unloadTasks.Count > 0)
                    await Task.WhenAll(unloadTasks);
            }

            // Phase 2: Load dependencies in parallel
            var depLoadTasks = new List<Task>();
            foreach (var guid in depGUIDs)
            {
                if (loadedSceneHandles.ContainsKey(guid) && loadedSceneHandles[guid].IsValid())
                    continue;

                var depHandle = Addressables.LoadSceneAsync(guid, LoadSceneMode.Additive);
                loadedSceneHandles[guid] = depHandle;
                depLoadTasks.Add(depHandle.Task);
            }
            if (depLoadTasks.Count > 0)
                await Task.WhenAll(depLoadTasks);

            // Phase 3: Load master scene
            var masterHandle = Addressables.LoadSceneAsync(sceneGUID, LoadSceneMode.Additive);
            loadedSceneHandles[sceneGUID] = masterHandle;
            await masterHandle.Task;

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
                    if (go == null) break;
                    go.GetComponent<SceneDependencyProxy>()?.LoadedAsDep(masterScene.name, sceneGUID);
                }
            }
            if (masterScene.IsValid())
                SceneManager.SetActiveScene(masterScene);

            return masterHandle;
        }

        public static async Task UnloadSceneAsync(string sceneGUID)
        {
            if (loadedSceneHandles.TryGetValue(sceneGUID, out var handle) && handle.IsValid())
            {
                await Addressables.UnloadSceneAsync(handle).Task;
                loadedSceneHandles.Remove(sceneGUID);
            }
        }

        public static async Task UnloadSceneAsync(AssetReference sceneRef)
        {
            await UnloadSceneAsync(sceneRef.AssetGUID);
        }

        // --- Dependency resolution ---

        public static List<string> ResolveDependencyTree(SceneDependency root)
        {
            HashSet<string> visited = new HashSet<string>();
            List<string> result = new List<string>();
            ResolveRequired(root, visited, result);
#if UNITY_EDITOR
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[SceneDependency] Loading dependencies in order:");
            for (int i = 0; i < result.Count; i++)
                sb.AppendLine(result[i]);
            Debug.Log(sb.ToString());
#endif
            return result;
        }

        static void ResolveRequired(SceneDependency subject, HashSet<string> visited, List<string> result)
        {
            if (subject.scenes == null) return;
            for (int i = 0; i < subject.scenes.Length; i++)
            {
                string guid = subject.scenes[i].AssetGUID;
                if (string.IsNullOrEmpty(guid) || visited.Contains(guid)) continue;
                visited.Add(guid);

                var index = SceneDependencyIndex.AutoInstance;
                if (index != null && index.TryGet(guid, out SceneDependency subDeps) && subDeps != null)
                {
                    ResolveRequired(subDeps, visited, result);
                }
                result.Add(guid);
            }
        }

        // --- Utility ---

        static string FindGUIDForLoadedScene(Scene scene)
        {
            foreach (var kvp in loadedSceneHandles)
            {
                if (kvp.Value.IsValid() && kvp.Value.Result.Scene == scene)
                    return kvp.Key;
            }
            return null;
        }

        static Task ToTask(AsyncOperation op)
        {
            var tcs = new TaskCompletionSource<bool>();
            op.completed += _ => tcs.SetResult(true);
            return tcs.Task;
        }
    }
}
