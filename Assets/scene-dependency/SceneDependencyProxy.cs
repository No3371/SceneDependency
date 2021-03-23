using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BAStudio.SceneDependency
{
    public class SceneDependencyProxy : MonoBehaviour
    {
        public SceneDependency config;
        #if UNITY_EDITOR
        [HideInInspector]
        public SceneDependencyIndex forceReference;
        public static SceneDependencyIndex cachedForceReference;
        #endif
        
    }
}