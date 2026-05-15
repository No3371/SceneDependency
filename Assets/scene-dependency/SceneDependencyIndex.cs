using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

        Dictionary<string, SceneDependency> EnsureIndex()
        {
            if (index == null)
            {
                index = new Dictionary<string, SceneDependency>();
                PopulateIndex();
            }
            return index;
        }

        public int Count => EnsureIndex().Count;

        public bool TryGet(string guid, out SceneDependency deps)
        {
            return EnsureIndex().TryGetValue(guid, out deps);
        }

        public bool ContainsKey(string guid)
        {
            return EnsureIndex().ContainsKey(guid);
        }

#if UNITY_EDITOR
        public void Add (string guid, SceneDependency deps)
        {
            EnsureIndex().Add(guid, deps);
        }
#endif

        void PopulateIndex ()
        {
            if (sceneDependencies == null || cachedGUIDs == null) return;
            if (sceneDependencies.Count != cachedGUIDs.Count)
                Debug.LogWarningFormat("[SceneDependency] Index data mismatch: {0} configs vs {1} GUIDs. Asset may be corrupt.",
                    sceneDependencies.Count, cachedGUIDs.Count);
            int count = Mathf.Min(sceneDependencies.Count, cachedGUIDs.Count);
            for (int i = 0; i < count; i++)
            {
                if (index.ContainsKey(cachedGUIDs[i]))
                {
                    Debug.LogErrorFormat("[SceneDependency] Found duplicate SceneDependency for GUID {0}, skipping.", cachedGUIDs[i]);
                    continue;
                }
                index.Add(cachedGUIDs[i], sceneDependencies[i]);
            }
        }

        public void OnAfterDeserialize()
        {
            index = new Dictionary<string, SceneDependency>();
            PopulateIndex();
        }

        public void OnBeforeSerialize()
        {
            if (sceneDependencies == null) sceneDependencies = new List<SceneDependency>();
            else sceneDependencies.Clear();
            if (cachedGUIDs == null) cachedGUIDs = new List<string>();
            else cachedGUIDs.Clear();

            if (index == null) return;
            foreach (var kvp in index)
            {
                sceneDependencies.Add(kvp.Value);
                cachedGUIDs.Add(kvp.Key);
            }
        }

        static SceneDependencyIndex runtimeInstance;
        static Task<SceneDependencyIndex> initTask;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            runtimeInstance = null;
            initTask = null;
        }

        public static SceneDependencyIndex AutoInstance
        {
            get
            {
#if UNITY_EDITOR
                return SceneDependencyIndexEditorAccess.Instance;
#else
                return runtimeInstance;
#endif
            }
        }

        public static async Task<SceneDependencyIndex> EnsureInitializedAsync()
        {
#if UNITY_EDITOR
            return SceneDependencyIndexEditorAccess.Instance;
#else
            if (runtimeInstance != null) return runtimeInstance;
            if (initTask != null && !initTask.IsFaulted) return await initTask;

            initTask = SceneDependencyRuntime.AsyncOpToTask(
                Addressables.LoadAssetAsync<SceneDependencyIndex>(AddressableLabel));
            runtimeInstance = await initTask;
            return runtimeInstance;
#endif
        }
    }
}
