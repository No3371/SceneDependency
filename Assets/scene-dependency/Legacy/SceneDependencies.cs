using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace BAStudio.SceneDependencies
{
    [CreateAssetMenu(menuName = "SceneDependencies")]
    public class SceneDependencies : ScriptableObject
    {
        public bool enabled = true;
        public SceneReference subject = null;
        public SceneReference[] scenes  = null;
        public bool NoAutoUnloadInSingleLoadMode;
    }
}
