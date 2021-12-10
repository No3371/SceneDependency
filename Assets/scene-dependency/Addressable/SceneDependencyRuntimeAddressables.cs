// #define LOG
#if !SCENE_DEP_OVERRIDE || SCENE_DEP_ADDRESSABLES
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;


namespace BAStudio.SceneDependencies
{
    public static class SceneDependencyRuntime
    {
        static SceneDependencyBroker broker;
        static SceneDependencyBroker Broker
        {
            get
            {
                if (broker != null) return broker;

                broker = new GameObject("SceneDependencyBroker", typeof(SceneDependencyBroker)).GetComponent<SceneDependencyBroker>();
                GameObject.DontDestroyOnLoad(broker);
                return broker;
            }
        }
        static Dictionary<string, string> ScenePathToAddressableGUID { get; set; }
        static Scene LastLoadedScene { get; set; }
        public static bool ZeroTimeScaleWhenLoading { get; set; }
        static SceneDependencyRuntime ()
        {
            Init();
        }

        [RuntimeInitializeOnLoadMethod]
        static void Init ()
        {
            if (SceneDependencyIndex.AutoInstance == null) Debug.Log("[SceneDependencies] Index not yet loaded.");
        }
        public static AsyncOperationWrapper LoadSceneAsync (string key, LoadSceneMode mode, bool reloadLoadedDep)
        {
            AsyncOperationWrapper aow = new AsyncOperationWrapper();
            LoadSceneAsync(key, mode, reloadLoadedDep, aow);
            return aow;
        }

        public static IEnumerator LoadSceneAsync (string key, LoadSceneMode mode, bool reloadLoadedDep, AsyncOperationWrapper aow)
        {
            if (SceneDependencyIndex.AutoInstance == null) throw new System.Exception("Please make sure SceneDependency is initialized.");
            SceneDependencies deps = SceneDependencyIndex.AutoInstance.Index[key];
            
            if (deps == null || deps.scenes.Length == 0)
            { 
                aow.value = Addressables.LoadSceneAsync(key, mode);
                yield break;
            }

            float cacheTimeScale = UnityEngine.Time.timeScale;
            if (ZeroTimeScaleWhenLoading) UnityEngine.Time.timeScale = 0;
            var depSceneGUIDs = ResolveDependencyTree(deps);
            
            var loc = Addressables.LoadResourceLocationsAsync(depSceneGUIDs as IEnumerable, Addressables.MergeMode.Union);
            var locations = loc.Result;
            var depSceneInternalPaths = locations.Select(l => l.InternalId).ToArray();
            AsyncOperation[] unloadOps = new AsyncOperation[SceneManager.sceneCount];
            Scene previousActiveScene = LastLoadedScene; // Latest loaded scene can not be unloaded (it just fail), we unload it again later
            if (mode == LoadSceneMode.Single)
            {
                for (int i = SceneManager.sceneCount - 1; i >= 0 ; i--)
                {
                    Scene iteratingScene = SceneManager.GetSceneAt(i);
                    if (iteratingScene == LastLoadedScene) continue;
                    string iteratingScenePath = iteratingScene.path;
                    if (iteratingScene.name == "DontDestroyOnLoad"
                     || (SceneDependencyIndex.AutoInstance.Index.ContainsKey(iteratingScenePath)
                     && SceneDependencyIndex.AutoInstance.Index[iteratingScenePath].NoAutoUnloadInSingleLoadMode)) continue;

                    if (reloadLoadedDep && depSceneGUIDs.Contains(iteratingScenePath))
                    {
                        #if LOG
                        Debug.Log(string.Concat("Reloading scene: ", iteratingScenePath));
                        #endif
                        unloadOps[i] = SceneManager.UnloadSceneAsync(iteratingScene);
                        if (ScenePathToAddressableGUID.ContainsKey(iteratingScenePath))
                            ScenePathToAddressableGUID.Remove(iteratingScenePath);
                        continue;
                    }

                    bool isDep = false;
                    for (int j = 0; j < deps.scenes.Length; j++)
                    {
                        if (ScenePathToAddressableGUID.TryGetValue(iteratingScenePath, out var itGUID)
                         && itGUID == deps.scenes[i].AssetGUID)
                        {
                            isDep = true;
                            break;
                        }
                    }
                    if (!isDep)
                    {
                        #if LOG
                        Debug.Log(string.Concat("Unloading scene: ", iteratingScenePath));
                        #endif
                        unloadOps[i] = SceneManager.UnloadSceneAsync(iteratingScene);
                        if (ScenePathToAddressableGUID.ContainsKey(iteratingScenePath))
                            ScenePathToAddressableGUID.Remove(iteratingScenePath);
                    }
                }
                previousActiveScene = SceneManager.GetActiveScene();
            }


            WaitForSecondsRealtime wfsr = new WaitForSecondsRealtime(0.1f);
            while (true)
            {
                yield return wfsr;
                int check = unloadOps.Length;
                for (int i = 0; i < unloadOps.Length; i++)
                {
                    if (unloadOps[i] == null || unloadOps[i].isDone) check--;
                }
                if (check == 0) break;
            }

            // Finished unloading
            for (int i = 0; i < unloadOps.Length; i++) unloadOps[i] = null;

            var loadOps = new AsyncOperationHandle<SceneInstance>[depSceneInternalPaths.Length];
            for (int i = 0; i < depSceneInternalPaths.Length; i++)
            {
                string capturedPath = depSceneInternalPaths[i];
                string capturedGUID = depSceneGUIDs[i];
                if (SceneManager.GetSceneByPath(capturedPath).isLoaded)
                {
        #if LOG
                    Debug.Log(string.Concat("Dep scene is loaded, skip: ", depScenes[i]));
        #endif
                    continue;
                }
        #if LOG
                Debug.Log(string.Concat("Loading scene: ", depScenes[i]));
        #endif
                loadOps[i] = Addressables.LoadSceneAsync(capturedGUID, LoadSceneMode.Additive);
                loadOps[i].Completed += (aoh) => {
                    ScenePathToAddressableGUID.Add(capturedPath, capturedGUID);
                };
            }

            while (true)
            {
                yield return wfsr;
                int check = loadOps.Length;
                for (int i = 0; i < unloadOps.Length; i++)
                {
                    if (unloadOps[i] == null || unloadOps[i].isDone) check--;
                }
                if (check == 0) break;
            }

            if (previousActiveScene.IsValid() && !depSceneInternalPaths.Contains(previousActiveScene.path)) SceneManager.UnloadSceneAsync(previousActiveScene);

            aow.value = Addressables.LoadSceneAsync(key, LoadSceneMode.Additive);
            aow.value.Completed += (h) => {
                List<GameObject> cache = new List<GameObject>(32);
                for (int i = 0; i < depSceneInternalPaths.Length; i++)
                {
                    Scene scene = SceneManager.GetSceneByPath(depSceneInternalPaths[i]);
                    cache.Clear();
                    scene.GetRootGameObjects(cache);
                    for (int j = 0; j < cache.Count; j++)
                    {
                        if (cache[j] == null) break;
                        cache[j].GetComponent<SceneDependencyProxy>()?.LoadedAsDep(scene.name, scene.path);
                    }
                }
                SceneManager.SetActiveScene(h.Result.Scene);
            };
            
            if (ZeroTimeScaleWhenLoading) Time.timeScale = cacheTimeScale;
        }

        public static List<string> ResolveDependencyTree (SceneDependencies root)
        {
            HashSet<string> resolved = new HashSet<string>();
            ResolveRequired(root, resolved);
            List<string> result = new List<string>(resolved);
            // result.Reverse();
#if UNITY_EDITOR
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("Loading dependencies in order:");
            for (int i = 0; i < result.Count; i++)
            {
                sb.AppendLine(result[i]);
            }
            Debug.Log(sb.ToString());
#endif
            return result;
        }

        static void ResolveRequired (SceneDependencies subject, HashSet<string> results)
        {
            for (int i = 0; i < subject.scenes.Length; i++)
            {
                if (results.Contains(subject.scenes[i].AssetGUID)) continue;
                if (SceneDependencyIndex.AutoInstance.Index.TryGetValue(subject.scenes[i].AssetGUID, out SceneDependencies resolving))
                {
                    ResolveRequired(resolving, results);
                }
                results.Add(subject.scenes[i].AssetGUID);
            }
        }

        public class AsyncOperationWrapper
        {
            public AsyncOperationHandle<SceneInstance> value;
        }
    }
}
#endif