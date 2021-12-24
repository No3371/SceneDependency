#define LOG
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;


namespace BAStudio.SceneDependencies
{

    public static class SceneDependencyRuntime
    {
        static Dictionary<Scene, HashSet<SceneDependencies>> DependenciesMap { get; set; }
        static Dictionary<SceneDependencies, HashSet<Scene>> DependentsMap { get; set; }
        static SceneDependenciesMono broker;
        static SceneDependenciesMono Broker
        {
            get
            {
                if (broker != null) return broker;

                broker = new GameObject("SceneDependenciesMono", typeof(SceneDependenciesMono)).GetComponent<SceneDependenciesMono>();
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
            SceneManager.sceneUnloaded += (s) => {
                if (DependenciesMap.TryGetValue(s, out var deps))
                foreach (var sd in deps)
                {
                    if (DependentsMap.TryGetValue(sd, out var dependents))
                    {
                        
                    }
                }
            };
        }

        [RuntimeInitializeOnLoadMethod]
        static void Init ()
        {
            if (SceneDependencyIndex.AutoInstance == null) Debug.Log("[SceneDependencies] Index not yet loaded.");
            if (DependenciesMap == null) DependenciesMap = new Dictionary<Scene, HashSet<SceneDependencies>>();
            if (DependentsMap   == null) DependentsMap   = new Dictionary<SceneDependencies, HashSet<Scene>>();
        }

        public class AsyncOperationWrapper
        {
            public AsyncOperation value;
        }

        public static bool zeroTimeScaleWhenLoading = true;

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
            SceneDependencies deps;
            SceneDependencyIndex.AutoInstance.Index.TryGetValue(accessor, out deps);

            if (deps == null || deps.scenes.Length == 0)
                return new AsyncOperationWrapper
                {
                    value = SceneManager.LoadSceneAsync(accessor, mode)
                };

            AsyncOperationWrapper aow = new AsyncOperationWrapper();
            Broker.StartCoroutine(SceneWorker(deps, accessor, id, mode, reloadLoadedDep, aow));
            return aow;
        }

        public static AsyncOperationWrapper LoadSceneAsync (SceneReference scene, LoadSceneMode mode, bool reloadLoadedDep)
            => LoadSceneAsync(scene.ScenePath, scene.NameCache, mode, reloadLoadedDep);

        static IEnumerator SceneWorker (SceneDependencies deps, string accessor, string id, LoadSceneMode mode, bool reloadLoadedDep, AsyncOperationWrapper aow)
        {
            Session session = new Session(accessor, id);
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

            WaitForSecondsRealtime wfsr = new WaitForSecondsRealtime(0.05f);
            int opCheck = ops.Length;
            while (opCheck > 0)
            {
                foreach (var op in ops)
                    if (op == null || op.isDone) opCheck--;
                yield return wfsr;
                opCheck = ops.Length;
            }

            for (int i = 0; i < ops.Length; i++) ops[i] = null;

            for (int i = 0; i < depScenes.Count; i++)
            {
                if (SceneManager.GetSceneByPath(depScenes[i]).isLoaded)
                {
        #if LOG
                    Debug.Log(string.Concat("Dep scene is already loaded, skip: ", depScenes[i]));
        #endif
                    continue;
                }
        #if LOG
                Debug.Log(string.Concat("Loading scene: ", depScenes[i]));
        #endif
                ops[i] = SceneManager.LoadSceneAsync(depScenes[i], LoadSceneMode.Additive);
            }

            opCheck = ops.Length;
            while (opCheck > 0)
            {
                foreach (var op in ops)
                    if (op == null || op.isDone) opCheck--;
                yield return wfsr;
                opCheck = ops.Length;
            }

            // Unity refuse unloading last loaded scene, so we unload it here.
            if (previousActiveScene.IsValid() && !depScenes.Contains(previousActiveScene.path)) SceneManager.UnloadSceneAsync(previousActiveScene);

            aow.value = SceneManager.LoadSceneAsync(accessor, LoadSceneMode.Additive);
            aow.value.allowSceneActivation = true;
            aow.value.completed += (h) => {
                List<GameObject> cache = new List<GameObject>(32);
                List<SceneDependencyProxy> proxies = new List<SceneDependencyProxy>(32);
                Scene scene;
                for (int i = 0; i < depScenes.Count; i++)
                {
                    scene = SceneManager.GetSceneByPath(depScenes[i]);
                    cache.Clear();
                    scene.GetRootGameObjects(cache);
                    for (int j = 0; j < cache.Count; j++)
                    {
                        if (cache[j] == null) break;
                        var p = cache[j].GetComponent<SceneDependencyProxy>();
                        if (p == null) continue;
                        p.LoadedAsDep(session);
                        proxies.Add(p);
                    }
                }
                foreach (var p in proxies) p.AllDepsReady(session);
                SceneManager.SetActiveScene(SceneManager.GetSceneByPath(accessor));

                scene = SceneManager.GetSceneByPath(accessor);
                cache.Clear();
                scene.GetRootGameObjects(cache);
                for (int j = 0; j < cache.Count; j++)
                {
                    if (cache[j] == null) break;
                    var p = cache[j].GetComponent<SceneDependencyProxy>();
                    if (p == null) continue;
                    p.Loaded(session);
                    proxies.Add(p);
                }
            };
            
            if (zeroTimeScaleWhenLoading) Time.timeScale = cacheTimeScale;
        }

        public static List<string> ResolveDependencyTree (SceneDependencies root)
        {
            HashSet<string> required = new HashSet<string>();
            ResolveRequired(root, required);
            List<string> result = new List<string>(required);
            // result.Reverse();
#if UNITY_EDITOR
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("Dependencies in order:");
            for (int i = 0; i < result.Count; i++)
            {
                sb.AppendLine("- " + result[i]);
            }
            Debug.Log(sb.ToString());
#endif
            return result;
        }

        static void ResolveRequired (SceneDependencies subject, HashSet<string> results)
        {
            for (int i = 0; i < subject.scenes.Length; i++)
            {
                if (results.Contains(subject.scenes[i].ScenePath)) continue;
                if (SceneDependencyIndex.AutoInstance.Index.TryGetValue(subject.scenes[i].ScenePath, out SceneDependencies resolving))
                {
                    ResolveRequired(resolving, results);
                }
                results.Add(subject.scenes[i].ScenePath);
            }
        }

    }
}
