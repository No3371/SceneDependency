using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;


namespace BAStudio.SceneDependency
{
    public static class SceneDependencyRuntime
    {
        static GameObject broker;
        static GameObject Broker
        {
            get
            {
                if (broker != null) return broker;
                
                broker = new GameObject("SceneDependencyBroker");
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

        public static AsyncOperation LoadSceneAsync (string address, LoadSceneMode mode)
        {
            if (SceneDependencyIndex.AutoInstance == null) throw new System.Exception("Please make sure SceneDependency is initialized.");
            SceneDependency deps = SceneDependencyIndex.AutoInstance.Index[address];
            Addressables.LoadSceneAsync(address, mode, false)
        }

        public static AsyncOperation LoadSceneAsync (string fullpath, string id, LoadSceneMode mode)
        {
            if (SceneDependencyIndex.AutoInstance == null) throw new System.Exception("Please make sure SceneDependency is initialized.");
            SceneDependency deps = SceneDependencyIndex.AutoInstance.Index[fullpath];
            if (deps == null) return SceneManager.LoadSceneAsync(fullpath, mode);
            
            var masterAO = SceneManager.LoadSceneAsync(fullpath, mode);
            masterAO.allowSceneActivation = false;
            int depCount = 0;

            MonoBehaviour.StartCoroutine()

            void SceneDepCompleted (AsyncOperation ao)
            {
                if (depCount++ == deps.scenes.Length + deps.prefabs.Length) masterAO.allowSceneActivation = true;
                Debug.Log("Dependency scene loaded: " + ao.)
            }

            for (int i = 0; i < deps.scenes.Length; i++)
            {
                if (SceneManager.GetSceneByPath(deps.scenes[i].ScenePath).isLoaded)
                {
                    depCount++;
                    continue;
                }
                var depAO = SceneManager.LoadSceneAsync(deps.scenes[i], LoadSceneMode.Additive);
                depAO.completed += SceneDepCompleted;
            }
            
            Scene prefabScene = SceneManager.CreateScene(id + ".Dependencies");

            if (deps.prefabs.Length > 0)
            {
                void PrefabDepCompleted (GameObject loaded)
                {
                    SceneManager.MoveGameObjectToScene(loaded, prefabScene);
                    if (depCount++ == deps.scenes.Length + deps.prefabs.Length) masterAO.allowSceneActivation = true;
                }

                var prefabAO = Addressables.LoadAssetsAsync<GameObject>(deps.prefabs, PrefabDepCompleted);
                prefabAO.Completed += (h) => {
                    if (h.Status == AsyncOperationStatus.Failed)
                    {
                        throw new System.Exception("Failed to load dependencies for scene " + id, h.OperationException);
                    }
                };
            }

            return masterAO;
        }

        /// <summary>
        /// If no dependency configured, this act as a no-op wrapper to SceneManager.LoadSceneAsync.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        public static AsyncOperation LoadSceneAsync (SceneReference scene, LoadSceneMode mode) => LoadSceneAsync(scene.ScenePath, scene.NameCache, mode);

        static System.Collections.IEnumerator Watchman (ref int depCounter, )
        {

        }
    }
}