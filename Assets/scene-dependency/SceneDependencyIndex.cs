using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace BAStudio.SceneDependency
{
    public class SceneDependencyIndex : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField]
        [HideInInspector]
        List<string> cachedAddresses;
        [SerializeField]
        List<SceneDependency> sceneDependencies;
        [NonSerialized]
        private Dictionary<string, SceneDependency> index;

        public Dictionary<string, SceneDependency> Index
        {
            get
            {
                if (index == null)
                {
                    index = new Dictionary<string, SceneDependency>();
                    PopulateIndex();
                }
                return index;
            }
        }

#if UNITY_EDITOR
        public void Add (string address, SceneDependency deps)
        {
            Index.Add(address, deps);
        }
#endif

        void PopulateIndex ()
        {
            if (sceneDependencies == null || cachedAddresses == null) return;
            for (int i = 0; i < sceneDependencies.Count; i++)
            {
                if (index.ContainsKey(cachedAddresses[i]))
                {
                    Debug.LogErrorFormat("[SceneDependency] Found duplicate SceneDependency for {0}, removing...", cachedAddresses[i]);
                    sceneDependencies.RemoveAt(i);
                    cachedAddresses.RemoveAt(i);
                    i--;
                    continue;
                }
                index.Add(cachedAddresses[i], sceneDependencies[i]);
            }
        }

        public void OnAfterDeserialize()
        {
            if (index == null) index = new Dictionary<string, SceneDependency>();
            index.Clear();
            PopulateIndex();
        }

        public void OnBeforeSerialize()
        {
            if (index == null || index.Count == 0) return;

            if (sceneDependencies == null) sceneDependencies = new List<SceneDependency>();
            else sceneDependencies.Clear();
            if (cachedAddresses == null) cachedAddresses = new List<string>();
            else cachedAddresses.Clear();
            foreach (var kvp in index)
            {
                sceneDependencies.Add(kvp.Value);
                cachedAddresses.Add(kvp.Key);
            }
        }

        static SceneDependencyIndex runtimeInstance;

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
