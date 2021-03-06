#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BAStudio.SceneDependency
{
    public static class SceneDependencyIndexEditorAccess
    {
        private static string indexAssetGUID;
        public static string IndexAssetGUID
        {
            get
            {
                if (Instance == null);
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
                        throw new System.Exception("[SceneDependency] There are more then 1 SceneDependencyIndex scriptable object among the assets!");
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

    }

}
#endif