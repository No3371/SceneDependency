using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BAStudio.SceneDependencies
{
    public class SceneDependencyProxy : MonoBehaviour
    {
        public SceneDependencies config;
        #if UNITY_EDITOR
        [HideInInspector]
        public SceneDependencyIndex forceReference;
        public static SceneDependencyIndex cachedForceReference;
        #endif
        /// <summary>
        /// 
        /// </summary>
        /// <param name="masterSceneName"></param>
        /// <param name="masterSceneAcessor">Scene path or address</param>
        public virtual void LoadedAsDep (string masterSceneName, string masterSceneAcessor, Session session) {
            Debug.Log("LoadedAsDep: " + this.gameObject.scene.name);
        }
        
    }
}