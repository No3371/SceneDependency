#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEngine;

namespace BAStudio.SceneDependencies
{
    public static class SceneDependencyIndexEditorAccess
    {
        private static string indexAssetGUID;
        public static string IndexAssetGUID
        {
            get
            {
                if (Instance == null); // NO WORRY
                return indexAssetGUID;
            }
            private set => indexAssetGUID = value;
        }

        public static SceneDependencyIndex instance;

        public static SceneDependencyIndex Instance
        {
            get
            {
                if (instance == null)
                {
                    var lookup = AssetDatabase.FindAssets("t:SceneDependencyIndex");
                    if (lookup.Length == 0)
                    {
                        instance = SceneDependencyProxy.cachedForceReference = ScriptableObject.CreateInstance<SceneDependencyIndex>();
                        AssetDatabase.CreateAsset(instance, "Assets/SceneDependencyIndex.asset");
                        AssetDatabase.SaveAssets();
                        IndexAssetGUID = AssetDatabase.GUIDFromAssetPath("Assets/SceneDependencyIndex.asset").ToString();
                    }
                    else if (lookup.Length > 1)
                    {
                        throw new System.Exception("[SceneDependencies] There are more then 1 SceneDependencyIndex scriptable object among the assets!");
                    }
                    else
                    {
                        instance = SceneDependencyProxy.cachedForceReference = (SceneDependencyIndex)AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(lookup[0]));
                        IndexAssetGUID = lookup[0];
                    }
                }
                return instance;
            }
        }

        public static bool Verify ()
        {
            bool valid = true;
            HashSet<string> allScenePaths = new HashSet<string>();
            foreach (var kvp in Instance.Index)
            {
                foreach (var p in SceneDependencyRuntime.ResolveDependencyTree(kvp.Value)) allScenePaths.Add(p);
            }
            foreach (var p in allScenePaths)
            {
                if (!UnityEditor.EditorBuildSettings.scenes.Any(s => s.path == p && s.enabled))
                {
                    Debug.LogErrorFormat("  Not included in build: " + p);
                    valid = false;
                }
            }
            return valid;
        }
    }

}
#endif