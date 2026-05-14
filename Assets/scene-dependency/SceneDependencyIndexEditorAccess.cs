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
                _ = Instance;
                return indexAssetGUID;
            }
        }

        public static SceneDependencyIndex instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            indexAssetGUID = null;
        }

        public static SceneDependencyIndex Instance
        {
            get
            {
                if (instance == null)
                {
                    var lookup = AssetDatabase.FindAssets("t:SceneDependencyIndex");
                    if (lookup.Length == 0)
                    {
                        instance = ScriptableObject.CreateInstance<SceneDependencyIndex>();
                        AssetDatabase.CreateAsset(instance, "Assets/SceneDependencyIndex.asset");
                        AssetDatabase.SaveAssets();
                        indexAssetGUID = AssetDatabase.GUIDFromAssetPath("Assets/SceneDependencyIndex.asset").ToString();
                    }
                    else if (lookup.Length > 1)
                    {
                        throw new System.Exception("[SceneDependency] There are more than 1 SceneDependencyIndex scriptable object among the assets!");
                    }
                    else
                    {
                        instance = (SceneDependencyIndex)AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(lookup[0]));
                        indexAssetGUID = lookup[0];
                    }
                }
                return instance;
            }
        }
    }
}
#endif
