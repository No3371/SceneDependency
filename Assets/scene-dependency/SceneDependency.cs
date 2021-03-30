using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;


namespace BAStudio.SceneDependency
{
    #if SD_RES_LEGACY
    [CreateAssetMenu(menuName = "SceneDependency")]
    public class SceneDependency : ScriptableObject
    {
        public SceneReference subject;
        public SceneReference[] scenes;
        public bool NoAutoUnloadInSingleLoadMode;
    }
    #else
    [CreateAssetMenu(menuName = "SceneDependency")]
    public class SceneDependency : ScriptableObject
    {
        public AssetReference subject;
        public AssetReference[] scenes;
        public bool NoAutoUnloadInSingleLoadMode;

    }
    #endif
}
