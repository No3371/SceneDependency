using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
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
            if (initTask != null) return await initTask;

            var tcs = new TaskCompletionSource<SceneDependencyIndex>();
            initTask = tcs.Task;

            var aoh = Addressables.LoadAssetAsync<SceneDependencyIndex>(AddressableLabel);
            aoh.Completed += h =>
            {
                if (h.Status == AsyncOperationStatus.Succeeded)
                {
                    runtimeInstance = h.Result;
                    tcs.SetResult(h.Result);
                }
                else
                {
                    Debug.LogError("[SceneDependency] Failed to load index via Addressables label: " + AddressableLabel);
                    tcs.SetException(h.OperationException ??
                        new Exception("[SceneDependency] Failed to load index."));
                }
            };

            return await tcs.Task;
#endif
        }
    }
}
