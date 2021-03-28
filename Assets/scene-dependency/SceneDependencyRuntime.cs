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

        static SceneDependencyRuntime ()
        {
            Init();
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
            
            if (deps == null) return new AsyncOperationWrapper
            {
                value = SceneManager.LoadSceneAsync(accessor, mode)
            };

            AsyncOperationWrapper aow = new();
            Broker.StartCoroutine(SceneWorker(deps, accessor, id, mode, reloadLoadedDep, aow));
            return aow;
        }
        
        public static AsyncOperationWrapper LoadSceneAsync (SceneReference scene, LoadSceneMode mode, bool reloadLoadedDep)
        {
            return LoadSceneAsync(scene.ScenePath, scene.NameCache, mode, reloadLoadedDep);
        }

        static IEnumerator SceneWorker (SceneDependency deps, string accessor, string id, LoadSceneMode mode, bool reloadLoadedDep, AsyncOperationWrapper aow)
        {
            AsyncOperation[] ops = new AsyncOperation[deps.scenes.Length];
            if (mode == LoadSceneMode.Single)
            for (int i = SceneManager.sceneCount; i >= 0 ; i--)
            {
                if (reloadLoadedDep)
                {
                    SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
                    continue;
                }
                bool isDep = false;
                for (int j = 0; j < deps.scenes.Length; j++)
                {
                    if (SceneManager.GetSceneAt(i).path == deps.scenes[j].ScenePath)
                    {
                        isDep = true;
                        break;
                    }
                }
                if (!isDep)
                {
                    ops[i] = SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
                }
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

            if (ops.Length < deps.scenes.Length)
                ops = new AsyncOperation[deps.scenes.Length];
            else for (int i = 0; i < ops.Length; i++) ops[i] = null;

            for (int i = 0; i < deps.scenes.Length; i++)
            {
                if (SceneManager.GetSceneByPath(deps.scenes[i].ScenePath).isLoaded)
                {
                    continue;
                }
                ops[i] = SceneManager.LoadSceneAsync(deps.scenes[i], LoadSceneMode.Additive);
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

            aow.value = SceneManager.LoadSceneAsync(accessor, LoadSceneMode.Additive);
            aow.value.allowSceneActivation = false;

            Scene prefabScene = SceneManager.CreateScene(id + ".Dependencies");

            if (deps.prefabs.Length == 0)
            {
                aow.value.allowSceneActivation = true;
            }
            else
            {
                try
                {
                    for (int i = 0; i < deps.prefabs.Length; i++)
                    {
                        var prefab = GameObject.Instantiate(deps.prefabs[i]);
                        SceneManager.MoveGameObjectToScene(prefab, prefabScene);
                    }
                    aow.value.allowSceneActivation = true;
                }
                catch
                {
                    Debug.LogError(string.Concat("Failed to load dependencies for scene " + id));
                    throw;
                }
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