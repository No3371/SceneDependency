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
    [CreateAssetMenu(menuName = "SceneDependencies")]
    public class SceneDependencies : ScriptableObject
    {
        public SceneReference subject;
        public SceneReference[] scenes;
        public bool NoAutoUnloadInSingleLoadMode;
    }
}
