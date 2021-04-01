// #define LOG
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;


namespace BAStudio.SceneDependency
{


#if SD_RES_LEGACY
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

        static Scene LastLoadedScene { get; set; }

        static SceneDependencyRuntime ()
        {
            Init();
            SceneManager.sceneLoaded += (s, mode) => {
                LastLoadedScene = s;
            };
        }

        [RuntimeInitializeOnLoadMethod]
        static void Init ()
        {
            if (SceneDependencyIndex.AutoInstance == null) Debug.Log("[SceneDependency] Index not yet loaded.");
        }

        public class AsyncOperationWrapper
        {
            public AsyncOperation value;
        }

        public static bool zeroTimeScaleWhenLoading;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="accesor"></param>
        /// <param name="id"></param>
        /// <param name="mode"></param>
        /// <param name="reloadLoadedDep">By default, dep scene that already loaded will be kept. This option forces all scenes get unloaded then reloaded.</param>
        /// <returns></returns>
        public static AsyncOperationWrapper LoadSceneAsync (string accessor, string id, LoadSceneMode mode, bool reloadLoadedDep)
        {
            if (SceneDependencyIndex.AutoInstance == null) throw new System.Exception("Please make sure SceneDependency is initialized.");
            SceneDependency deps = SceneDependencyIndex.AutoInstance.Index[accessor];
            
            if (deps == null || deps.scenes.Length == 0) return new AsyncOperationWrapper
            {
                value = SceneManager.LoadSceneAsync(accessor, mode)
            };

            AsyncOperationWrapper aow = new AsyncOperationWrapper();
            Broker.StartCoroutine(SceneWorker(deps, accessor, id, mode, reloadLoadedDep, aow));
            return aow;
        }
        
        public static AsyncOperationWrapper LoadSceneAsync (SceneReference scene, LoadSceneMode mode, bool reloadLoadedDep)
            => LoadSceneAsync(scene.ScenePath, scene.NameCache, mode, reloadLoadedDep);

        static IEnumerator SceneWorker (SceneDependency deps, string accessor, string id, LoadSceneMode mode, bool reloadLoadedDep, AsyncOperationWrapper aow)
        {
            float cacheTimeScale = Time.timeScale;
            if (zeroTimeScaleWhenLoading) Time.timeScale = 0;
            var depScenes = ResolveDependencyTree(deps);
            AsyncOperation[] ops = new AsyncOperation[depScenes.Count > SceneManager.sceneCount ? depScenes.Count : SceneManager.sceneCount];
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
                    
                    if (reloadLoadedDep && depScenes.Contains(iteratingScenePath))
                    {
                        #if LOG
                        Debug.Log(string.Concat("Reloading scene: ", iteratingScenePath));
                        #endif
                        ops[i] = SceneManager.UnloadSceneAsync(iteratingScene);
                        continue;
                    }
                    bool isDep = false;
                    for (int j = 0; j < deps.scenes.Length; j++)
                    {
                        if (iteratingScene.path == deps.scenes[j].ScenePath)
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
                        ops[i] = SceneManager.UnloadSceneAsync(iteratingScene);
                    }
                }
                previousActiveScene = SceneManager.GetActiveScene();
            }


            float lastCheck = Time.realtimeSinceStartup - 0.2f;
            while (true)
            {
                if (Time.realtimeSinceStartup - lastCheck < 0.1) yield return null;
                lastCheck = Time.realtimeSinceStartup;
                int check = ops.Length;
                for (int i = 0; i < ops.Length; i++)
                {
                    if (ops[i] == null || ops[i].isDone) check--;
                }
                if (check == 0) break;
                else yield return null;
            }

            for (int i = 0; i < ops.Length; i++) ops[i] = null;

            for (int i = 0; i < depScenes.Count; i++)
            {
                if (SceneManager.GetSceneByPath(depScenes[i]).isLoaded)
                {
                    #if LOG
                    Debug.Log(string.Concat("Dep scene is loaded, skip: ", depScenes[i]));
                    #endif
                    continue;
                }
                #if LOG
                Debug.Log(string.Concat("Loading scene: ", depScenes[i]));
                #endif
                ops[i] = SceneManager.LoadSceneAsync(depScenes[i], LoadSceneMode.Additive);
            }

            lastCheck = Time.realtimeSinceStartup - 0.2f;
            while (true)
            {
                if (Time.realtimeSinceStartup - lastCheck < 0.1) yield return null;
                lastCheck = Time.realtimeSinceStartup;
                int check = ops.Length;
                for (int i = 0; i < ops.Length; i++)
                {
                    if (ops[i] == null || ops[i].isDone) check--;
                }
                if (check == 0) break;
                else yield return null;
            }

            if (previousActiveScene.IsValid() && !depScenes.Contains(previousActiveScene.path)) SceneManager.UnloadSceneAsync(previousActiveScene);

            aow.value = SceneManager.LoadSceneAsync(accessor, LoadSceneMode.Additive);
            aow.value.allowSceneActivation = true;
            aow.value.completed += (h) => {
                List<GameObject> cache = new List<GameObject>(32);
                for (int i = 0; i < depScenes.Count; i++)
                {
                    Scene scene = SceneManager.GetSceneByPath(depScenes[i]);
                    cache.Clear();
                    scene.GetRootGameObjects(cache);
                    for (int j = 0; j < cache.Count; j++)
                    {
                        if (cache[j] == null) break;
                        cache[j].GetComponent<SceneDependencyProxy>()?.LoadedAsDep(scene.name, scene.path);
                    }
                }
                SceneManager.SetActiveScene(SceneManager.GetSceneByPath(accessor));
            };
            
            if (zeroTimeScaleWhenLoading) Time.timeScale = cacheTimeScale;
        }

        public static List<string> ResolveDependencyTree (SceneDependency root)
        {
            HashSet<string> required = new HashSet<string>();
            ResolveRequired(root, required);
            List<string> result = new List<string>(required);
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

        static void ResolveRequired (SceneDependency subject, HashSet<string> results)
        {
            for (int i = 0; i < subject.scenes.Length; i++)
            {
                if (results.Contains(subject.scenes[i].ScenePath)) continue;
                if (SceneDependencyIndex.AutoInstance.Index.TryGetValue(subject.scenes[i].ScenePath, out SceneDependency resolving))
                {
                    ResolveRequired(resolving, results);
                }
                results.Add(subject.scenes[i].ScenePath);
            }
        }
    }
#else

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
        static SceneDependencyRuntime ()
        {
            Init();
        }

        [RuntimeInitializeOnLoadMethod]
        static void Init ()
        {
            if (SceneDependencyIndex.AutoInstance == null) Debug.Log("[SceneDependency] Index not yet loaded.");
        }

            #if SD_RES_LEGACY
        public static AsyncOperation LoadSceneAsync (string accesor, string id, LoadSceneMode mode)
            #else
        public static AsyncOperationHandle<SceneInstance> LoadSceneAsync (string accesor, string id, LoadSceneMode mode)
            #endif
        {
            if (SceneDependencyIndex.AutoInstance == null) throw new System.Exception("Please make sure SceneDependency is initialized.");
            SceneDependency deps = SceneDependencyIndex.AutoInstance.Index[accesor];
            
        #if SD_RES_LEGACY
            if (deps == null) return SceneManager.LoadSceneAsync(accesor, mode);
        #else
            if (deps == null) return Addressables.LoadSceneAsync(accesor, mode);
        #endif

            if (mode == LoadSceneMode.Single)
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                for (int j = 0; j < deps.scenes.Length; j++)
                {
                    if (SceneManager.GetSceneAt(i).path == deps.scenes[j].ScenePath)
                }

            }

            int depCount = 0;
            // broker.StartCoroutine(Watchman(ref depCount, accesor));
            #if SD_RES_LEGACY
                void SceneDepCompleted (AsyncOperation ao) => depCount++;
            #else
                void SceneDepCompleted (AsyncOperationHandle<SceneInstance> ao) => depCount++;
            #endif

            for (int i = 0; i < deps.scenes.Length; i++)
            {
                if (SceneManager.GetSceneByPath(deps.scenes[i].ScenePath).isLoaded)
                {
                    depCount++;
                    continue;
                }
            #if SD_RES_LEGACY
                var depAO = SceneManager.LoadSceneAsync(deps.scenes[i], LoadSceneMode.Additive);
                depAO.completed += SceneDepCompleted;
            #else
                var depAO = Addressables.LoadSceneAsync(deps.scenes[i].ScenePath, LoadSceneMode.Additive);
                depAO.Completed += SceneDepCompleted;
            #endif

            }
            
        #if SD_RES_LEGACY
            var masterAO = SceneManager.LoadSceneAsync(accesor, LoadSceneMode.Additive);
            masterAO.allowSceneActivation = false;
        #else
            var aoh = Addressables.LoadSceneAsync(accesor, LoadSceneMode.Additive, false);
        #endif

            Scene prefabScene = SceneManager.CreateScene(id + ".Dependencies");

            if (deps.prefabs.Length == 0)
            {
            #if SD_RES_LEGACY
                masterAO.allowSceneActivation = true;
            #else
                aoh.Result.ActivateAsync();
            #endif
            }
            else
            {
                void PrefabDepCompleted (GameObject loaded)
                {
                    SceneManager.MoveGameObjectToScene(loaded, prefabScene);
                    depCount++;
                    if (depCount == deps.scenes.Length + deps.prefabs.Length)
                    {
                    #if SD_RES_LEGACY
                        masterAO.allowSceneActivation = true;
                    #else
                        aoh.Result.ActivateAsync();
                    #endif
                    }
                }

            #if SD_RES_LEGACY
                for (int i = 0; i < deps.prefabs.Length; i++)
                {
                    var prefab = GameObject.Instantiate(deps.prefabs[i]);
                    PrefabDepCompleted(prefab);
                }
            #else
                var prefabAO = Addressables.LoadAssetsAsync<GameObject>(deps.prefabs, PrefabDepCompleted);
                prefabAO.Completed += (h) => {
                    if (h.Status == AsyncOperationStatus.Failed)
                    {
                        throw new System.Exception("Failed to load dependencies for scene " + id, h.OperationException);
                    }
                };
            #endif
            }

        #if SD_RES_LEGACY
            return masterAO;
            
        #else
            return aoh;
        #endif
        }

        #if SD_RES_LEGACY
        public static AsyncOperation LoadSceneAsync (SceneReference scene, LoadSceneMode mode)
        {
            return LoadSceneAsync(scene.ScenePath, scene.NameCache, mode);
        }
            
        #else
        public static AsyncOperationHandle<SceneInstance> LoadSceneAsync (SceneReference scene, LoadSceneMode mode)
        {
            return LoadSceneAsync(scene.ScenePath, scene.NameCache, mode);
        }
        #endif


    }
#endif
}