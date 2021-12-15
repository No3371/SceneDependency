#if !SCENE_DEP_OVERRIDE || SCENE_DEP_LEGACY

using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BAStudio.SceneDependencies
{

    [InitializeOnLoad]
    public static class SceneDependencyEditorHooks
    {
        [InitializeOnLoadMethod]
        public static void Hook ()
        {
            EditorSceneManager.sceneSaving += (scene, path) => {
                if (EditorApplication.isPlaying) return;
                Debug.Log("[SceneDependencies] Detected scene saving...");
                var roots = scene.GetRootGameObjects();
                var proxy = roots.FirstOrDefault(r => r.GetComponent<SceneDependencyProxy>())?.GetComponent<SceneDependencyProxy>();
                if (proxy == null || proxy.config == null) return;
                if (proxy.config.scenes.Length == 0) 
                    Debug.Log("[SceneDependencies] No dependency configured, skip.");

                if (proxy.config.subject == null || string.IsNullOrEmpty(proxy.config.subject.ScenePath))
                {
                    Debug.Log("[SceneDependencies] The proxy target config is not pointing to any scene, fixing...");
                    proxy.config.subject = new SceneReference { ScenePath = path };
                }

                if (proxy.config.subject.ScenePath != path)
                {
                    UnityEditor.EditorGUIUtility.PingObject(proxy.config);
                    throw new System.Exception("[SceneDependencies] Saving scene but target config subject is not equal to this scene!");
                }

                if (SceneDependencyIndexEditorAccess.Instance.Index.ContainsKey(path))
                {
                    if (SceneDependencyIndexEditorAccess.Instance.Index[path] != proxy.config) throw new System.Exception("[SceneDependencies] Saving scene but proxy mismatch!");
                }
                else
                {
                    Debug.Log("[SceneDependencies] Adding the dependency config to index...");
                    SceneDependencyIndexEditorAccess.Instance.Index.Add(path, proxy.config);
                    EditorUtility.SetDirty(SceneDependencyIndexEditorAccess.Instance);
                    AssetDatabase.SaveAssets();
                }
                Debug.Log("[SceneDependencies] Preprocess completed: " + scene.name);
            };
            Debug.Log("[SceneDependencies] Hooked into EditorSceneManager.sceneSaving.");
        }
    }
}
#endif