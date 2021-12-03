using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace BAStudio.SceneDependencies
{
#if !SCENE_DEP_OVERRIDE || SCENE_DEP_LEGACY
    public class SceneDependencyIndex : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField]
        [HideInInspector]
        List<string> cachedPaths;
        [SerializeField]
        List<SceneDependencies> sceneDependencies;
        [NonSerialized]
        private Dictionary<string, SceneDependencies> index;

        public Dictionary<string, SceneDependencies> Index
        {
            get 
            {
                EnsureIndexReady();
                return index;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id">Address when using Addressables, scene file path in other cases</param>
        /// <param name="deps"></param>
        public void Add (string id, SceneDependencies deps)
        {
            Index.Add(id, deps);
        }

        void PopulateIndex ()
        {
            // Debug.LogFormat("[SceneDependencies] Populating {0} entries.", sceneDependencies.Count);
            for (int i = 0; i < sceneDependencies.Count; i++)
            {
                if (index.ContainsKey(cachedPaths[i]))
                {
                    Debug.LogErrorFormat("Found duplicate SceneDependency for {0}, removing...", sceneDependencies[i].subject.ScenePath);
                    sceneDependencies.RemoveAt(i);
                    i--;
                    continue;
                }
                index.Add(cachedPaths[i], sceneDependencies[i]);
            }
        }

        void EnsureIndexReady ()
        {
            if (index == null) index = new Dictionary<string, SceneDependencies>();
            if (sceneDependencies == null) sceneDependencies = new List<SceneDependencies>();
            bool perfectMatch = true;
            if (index.Count != sceneDependencies.Count) perfectMatch = false;
            if (perfectMatch)
            {
                for (int i = 0; i < sceneDependencies.Count; i++)
                {
                    if (!index.ContainsKey(sceneDependencies[i].subject.ScenePath))
                    {
                        perfectMatch = false;
                        break;
                    }
                }
            }
            if (!perfectMatch)
            {
                index.Clear();
                PopulateIndex();
            }
        }

        bool NeedUpdateForSerialize ()
        {
            bool perfectMatch = true;
            if (index == null || index.Count != sceneDependencies.Count) perfectMatch = false;
            if (perfectMatch)
            {
                for (int i = 0; i < sceneDependencies.Count; i++)
                {
                    if (!index.ContainsKey(sceneDependencies[i].subject.ScenePath))
                    {
                        perfectMatch = false;
                        break;
                    }
                }
            }
            return !perfectMatch;
        }

        public void OnAfterDeserialize()
        {
            if (index == null) index = new Dictionary<string, SceneDependencies>();
            index.Clear();
            PopulateIndex();
        }

        public void OnBeforeSerialize()
        {
            if (index == null) return;
            if (NeedUpdateForSerialize())
            {
                if (sceneDependencies == null) sceneDependencies = new List<SceneDependencies>();
                else sceneDependencies?.Clear();
                if (cachedPaths == null) cachedPaths = new List<string>();
                else cachedPaths?.Clear();
                using (var e = index.GetEnumerator())
                {
                    while(e.MoveNext())
                    {
                        Debug.LogFormat("[SceneDependencies] Adding {0} to serializing dependencies.", e.Current.Key);
                        sceneDependencies.Add(e.Current.Value);
                        cachedPaths.Add(e.Current.Key);
                    }
                }
            }
        }

        static SceneDependencyIndex runtimeInstance;
        /// <summary>
        /// When accessed at runtime for the first time, this could return null until the index is loaded through Addressables.
        /// </summary>
        /// <value></value>
        public static SceneDependencyIndex AutoInstance
        {
            get
            {
        #if UNITY_EDITOR
                return SceneDependencyIndexEditorAccess.Instance;
        #else
                if (runtimeInstance != null) return runtimeInstance;

            #if !SCENE_DEP_OVERRIDE || !SCENE_DEP_LEGACY
                var r = Resources.LoadAsync<SceneDependencyIndex>("SceneDependencyIndex");
                r.completed += (_) =>
                {
                    runtimeInstance = r.asset as SceneDependencyIndex;
                };
            #else
                var aoh = Addressables.LoadAssetAsync<SceneDependencyIndex>("SceneDependency/Index");
                aoh.Completed += (h) =>
                {
                    runtimeInstance = h.Result;
                };
            #endif

                return runtimeInstance;
        #endif
            }
        }
    }


#elif !SCENE_DEP_OVERRIDE || SCENE_DEP_ADDRESSABLE

    public class SceneDependencyIndex : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField]
        [HideInInspector]
        List<string> cachedAddresses;
        [SerializeField]
        List<SceneDependencies> sceneDependencies;
        [NonSerialized]
        private Dictionary<string, SceneDependencies> index;

        public Dictionary<string, SceneDependencies> Index
        {
            get 
            {
                // EnsureIndexReady();
                return index;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 
        /// </summary>
        /// <param name="id">scene file path</param>
        /// <param name="deps"></param>
        public void Add (string address, SceneDependencies deps)
        {
            Index.Add(address, deps);
        }
#endif
        void PopulateIndex ()
        {
            // Debug.LogFormat("[SceneDependencies] Populating {0} entries.", sceneDependencies.Count);
            for (int i = 0; i < sceneDependencies.Count; i++)
            {
                if (index.ContainsKey(cachedAddresses[i]))
                {
                    Debug.LogErrorFormat("Found duplicate SceneDependencies for {0}, removing...", sceneDependencies[i].subject.Asset.name);
                    sceneDependencies.RemoveAt(i);
                    i--;
                    continue;
                }
                index.Add(cachedAddresses[i], sceneDependencies[i]);
            }
        }

        // void EnsureIndexReady ()
        // {
        //     if (index == null) index = new Dictionary<string, SceneDependency>();
        //     if (sceneDependencies == null) sceneDependencies = new List<SceneDependency>();
        //     bool perfectMatch = true;
        //     if (index.Count != sceneDependencies.Count) perfectMatch = false;
        //     if (perfectMatch)
        //     {
        //         for (int i = 0; i < sceneDependencies.Count; i++)
        //         {
        //             if (!index.ContainsKey(cachedAddresses[i]))
        //             {
        //                 perfectMatch = false;
        //                 break;
        //             }
        //         }
        //     }
        //     if (!perfectMatch)
        //     {
        //         index.Clear();
        //         PopulateIndex();
        //     }
        // }

        public void OnAfterDeserialize()
        {
            if (index == null) index = new Dictionary<string, SceneDependencies>();
            index.Clear();
            PopulateIndex();
        }

        public void OnBeforeSerialize()
        {
            if (index == null || index.Count == 0) return;

            if (sceneDependencies == null) sceneDependencies = new List<SceneDependencies>();
            else sceneDependencies?.Clear();
            if (cachedAddresses == null) cachedAddresses = new List<string>();
            else cachedAddresses?.Clear();
            using (var e = index.GetEnumerator())
            {
                while(e.MoveNext())
                {
                    Debug.LogFormat("[SceneDependencies] Adding {0} to serializing dependencies.", e.Current.Key);
                    sceneDependencies.Add(e.Current.Value);
                    cachedAddresses.Add(e.Current.Key);
                }
            }
        }

        static SceneDependencyIndex runtimeInstance;
        /// <summary>
        /// When accessed at runtime for the first time, this could return null until the index is loaded through Addressables.
        /// </summary>
        /// <value></value>
        public static SceneDependencyIndex AutoInstance
        {
            get
            {
#if UNITY_EDITOR
                return SceneDependencyIndexEditorAccess.Instance;
#else
                if (runtimeInstance != null) return runtimeInstance;

                var aoh = Addressables.LoadAssetAsync<SceneDependencyIndex>(".SceneDependencyIndex");
                aoh.Completed += (h) =>
                {
                    runtimeInstance = h.Result;
                };

                return runtimeInstance;
#endif
            }
        }

    }
#endif
}