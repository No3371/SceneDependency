using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Events;

namespace BAStudio.SceneDependencies
{
    public interface ISceneDependencyListener
    {
        void Loaded (Session session);

        void LoadedAsDep (Session session);

        void AllDepsReady (Session session);
    }

    public class SceneDependencyProxy : MonoBehaviour
    {
        public MonoBehaviour[] listeners;
        public SceneDependencies config;
        #if UNITY_EDITOR
        [HideInInspector]
        public SceneDependencyIndex forceReference;
        public static SceneDependencyIndex cachedForceReference;
        #endif

        public UnityEvent<Session> OnLoaded, OnLoadedAsDep, OnAllDepsReady;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="masterSceneName"></param>
        /// <param name="masterSceneAcessor">Scene path or address</param>
        public virtual void Loaded (Session session) {
            OnLoaded?.Invoke(session);
            Debug.Log("Loaded: " + this.gameObject.scene.name);
            if (listeners != null) foreach (var l in listeners) (l as ISceneDependencyListener)?.Loaded(session);
        }

        public virtual void LoadedAsDep (Session session) {
            OnLoadedAsDep?.Invoke(session);
            Debug.Log("LoadedAsDep: " + this.gameObject.scene.name);
            if (listeners != null) foreach (var l in listeners) (l as ISceneDependencyListener)?.LoadedAsDep(session);
        }
        
        public virtual void AllDepsReady (Session session) {
            OnAllDepsReady?.Invoke(session);
            Debug.Log("AllDepsReady: " + this.gameObject.scene.name);
            if (listeners != null) foreach (var l in listeners) (l as ISceneDependencyListener)?.AllDepsReady(session);
        }
    }
}