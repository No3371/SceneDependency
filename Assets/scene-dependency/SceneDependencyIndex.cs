using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;

namespace BAStudio.SceneDependency
{
    public class SceneDependencyIndex : ScriptableObject, ISerializationCallbackReceiver
    {
        public const string AddressableLabel = "SceneDependency.Index";

        [SerializeField]
        [HideInInspector]
        [FormerlySerializedAs("cachedAddresses")]
        List<string> cachedGUIDs;
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

        public bool TryGet(string guid, out SceneDependency deps)
        {
            return Index.TryGetValue(guid, out deps);
        }

        public bool ContainsKey(string guid)
        {
            return Index.ContainsKey(guid);
        }

#if UNITY_EDITOR
        public void Add (string guid, SceneDependency deps)
        {
            Index.Add(guid, deps);
        }
#endif

        void PopulateIndex ()
        {
            if (sceneDependencies == null || cachedGUIDs == null) return;
            for (int i = 0; i < sceneDependencies.Count; i++)
            {
                if (index.ContainsKey(cachedGUIDs[i]))
                {
                    Debug.LogErrorFormat("[SceneDependency] Found duplicate SceneDependency for GUID {0}, removing...", cachedGUIDs[i]);
                    sceneDependencies.RemoveAt(i);
                    cachedGUIDs.RemoveAt(i);
                    i--;
                    continue;
                }
                index.Add(cachedGUIDs[i], sceneDependencies[i]);
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
            if (cachedGUIDs == null) cachedGUIDs = new List<string>();
            else cachedGUIDs.Clear();
            foreach (var kvp in index)
            {
                sceneDependencies.Add(kvp.Value);
                cachedGUIDs.Add(kvp.Key);
            }
        }

        static SceneDependencyIndex runtimeInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            runtimeInstance = null;
        }

        public static SceneDependencyIndex AutoInstance
        {
            get
            {
#if UNITY_EDITOR
                return SceneDependencyIndexEditorAccess.Instance;
#else
                if (runtimeInstance != null) return runtimeInstance;

                var aoh = Addressables.LoadAssetAsync<SceneDependencyIndex>(AddressableLabel);
                aoh.Completed += (h) =>
                {
                    if (h.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                        runtimeInstance = h.Result;
                    else
                        Debug.LogError("[SceneDependency] Failed to load index via Addressables label: " + AddressableLabel);
                };

                return runtimeInstance;
#endif
            }
        }
    }
}
