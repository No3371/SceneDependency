using UnityEngine;

namespace BAStudio.SceneDependency
{
    public class SceneDependencyProxy : MonoBehaviour
    {
        public SceneDependency config;

        public virtual void LoadedAsDep (string masterSceneName, string masterSceneGUID) {
            Debug.Log("LoadedAsDep: " + this.gameObject.scene.name);
        }
    }
}
