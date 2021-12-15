#define LOG
using System;
using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BAStudio.SceneDependencies
{
    [Serializable]
    public class DictSD : SerializableDictionary<string, SceneDependencies> {}
    public partial class SceneDependencyIndex : ScriptableObject, IPreprocessBuildWithReport
    {
        [SerializeField]
        DictSD index;
        public DictSD Index { get => index; private set => index = value; }


        /// <summary>
        ///
        /// </summary>
        /// <param name="id">Address when using Addressables, scene file path in other cases</param>
        /// <param name="deps"></param>

        // void PopulateIndex ()
        // {
        //     Debug.LogFormat("[SceneDependencies] Populating {0} entries.", sceneDependencies.Count);
        //     for (int i = 0; i < sceneDependencies.Count; i++)
        //     {
        //         if (index.ContainsKey(cachedPaths[i]))
        //         {
        //             Debug.LogErrorFormat("Found duplicate SceneDependency for {0}, removing...", sceneDependencies[i].subject.ScenePath);
        //             sceneDependencies.RemoveAt(i);
        //             i--;
        //             continue;
        //         }
        //         index.Add(cachedPaths[i], sceneDependencies[i]);
        //         Debug.LogFormat("[SceneDependencies] Populated {0} to the Index.", cachedPaths[i]);
        //     }
        // }

        // void EnsureIndexReady ()
        // {
        //     if (index == null) index = new Dictionary<string, SceneDependencies>();
        //     if (sceneDependencies == null) sceneDependencies = new List<SceneDependencies>();
        //     bool perfectMatch = true;
        //     if (index.Count != sceneDependencies.Count) perfectMatch = false;
        //     if (perfectMatch)
        //     {
        //         for (int i = 0; i < sceneDependencies.Count; i++)
        //         {
        //             if (!index.ContainsKey(sceneDependencies[i].subject.ScenePath))
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

        // bool NeedUpdateForSerialize ()
        // {
        //     bool perfectMatch = true;
        //     if (index == null || index.Count != sceneDependencies.Count) perfectMatch = false;
        //     if (perfectMatch)
        //     {
        //         for (int i = 0; i < sceneDependencies.Count; i++)
        //         {
        //             if (!index.ContainsKey(sceneDependencies[i].subject.ScenePath))
        //             {
        //                 perfectMatch = false;
        //                 break;
        //             }
        //         }
        //     }
        //     return !perfectMatch;
        // }

        // public void OnAfterDeserialize()
        // {
        //     if (index == null) index = new Dictionary<string, SceneDependencies>();
        //     index.Clear();
        //     PopulateIndex();
        // }

        // public void OnBeforeSerialize()
        // {
        //     if (index == null) return;
        //     if (NeedUpdateForSerialize())
        //     {
        //         if (sceneDependencies == null) sceneDependencies = new List<SceneDependencies>();
        //         else sceneDependencies?.Clear();
        //         if (cachedPaths == null) cachedPaths = new List<string>();
        //         else cachedPaths?.Clear();
        //         using (var e = index.GetEnumerator())
        //         {
        //             while(e.MoveNext())
        //             {
        //                 Debug.LogFormat("[SceneDependencies] OnBeforeSerialize: Adding {0} to serializing dependencies.", e.Current.Key);
        //                 sceneDependencies.Add(e.Current.Value);
        //                 cachedPaths.Add(e.Current.Key);
        //             }
        //         }
        //     }
        // }


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

        int IOrderedCallback.callbackOrder => throw new NotImplementedException();

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.LogFormat("SceneDependencies - OnPreprocessBuild");
            SceneDependencyIndexEditorAccess.Instance.RebuildIndex();
            if (!SceneDependencyIndexEditorAccess.Verify())
                throw new BuildFailedException("===Scnen Dependencies Invalid===");
        }
    }

}