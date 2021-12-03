using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;


namespace BAStudio.SceneDependencies
{
#if !SCENE_DEP_OVERRIDE || SCENE_DEP_LEGACY
    [CreateAssetMenu(menuName = "SceneDependencies")]
    public class SceneDependencies : ScriptableObject
    {
        public SceneReference subject;
        public SceneReference[] scenes;
        public bool NoAutoUnloadInSingleLoadMode;
    }
#elif !SCENE_DEP_OVERRIDE || SCENE_DEP_ADDRESSABLE

    [CreateAssetMenu(menuName = "SceneDependencies")]
    public class SceneDependencies : ScriptableObject
    {
        public AssetReference subject;
        public AssetReference[] scenes;
        public bool NoAutoUnloadInSingleLoadMode;

    }
#endif
}
