using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace BAStudio.SceneDependencies
{

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
}