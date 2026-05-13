using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace BAStudio.SceneDependency
{
    public static class SceneDependencyRuntime
    {
        [RuntimeInitializeOnLoadMethod]
        static void Init ()
        {
            if (SceneDependencyIndex.AutoInstance == null) Debug.Log("[SceneDependency] Index not yet loaded.");
        }

        public static AsyncOperationHandle<SceneInstance> LoadSceneAsync (string accessor, LoadSceneMode mode)
        {
            if (SceneDependencyIndex.AutoInstance == null) throw new System.Exception("[SceneDependency] Please make sure SceneDependency is initialized.");
            if (!SceneDependencyIndex.AutoInstance.Index.TryGetValue(accessor, out SceneDependency deps) || deps == null)
            {
                return Addressables.LoadSceneAsync(accessor, mode);
            }

            void SceneDepCompleted (AsyncOperationHandle<SceneInstance> ao) { }

            for (int i = 0; i < deps.scenes.Length; i++)
            {
                var depAO = Addressables.LoadSceneAsync(deps.scenes[i].AssetGUID, LoadSceneMode.Additive);
                depAO.Completed += SceneDepCompleted;
            }

            var aoh = Addressables.LoadSceneAsync(accessor, LoadSceneMode.Additive, false);
            aoh.Completed += h => h.Result.ActivateAsync();
            return aoh;
        }

        public static AsyncOperationHandle<SceneInstance> LoadSceneAsync (AssetReference sceneRef, LoadSceneMode mode)
        {
            return LoadSceneAsync(sceneRef.AssetGUID, mode);
        }
    }
}
