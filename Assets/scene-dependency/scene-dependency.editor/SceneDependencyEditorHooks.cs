#define USE_ADDRESSABLES

using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
#if !USE_ADDRESSABLES
using Unity.AddressableAssets;
#endif

namespace BAStudio.SceneDependency
{
    [InitializeOnLoad]
    public static class SceneDependencyEditorHooks
    {
#if !USE_ADDRESSABLES
        [InitializeOnLoadMethod]
        public static void Hook ()
        {
            EditorSceneManager.sceneSaving += (scene, path) => {
                var roots = scene.GetRootGameObjects();
                var proxy = roots.FirstOrDefault(r => r.GetComponent<SceneDependencyProxy>())?.GetComponent<SceneDependencyProxy>();
                if (proxy == null || proxy.config == null) return;
                if (proxy.config.scenes.Length == 0 && proxy.config.prefabs.Length == 0) 
                    Debug.Log("[SceneDependency] No dependency configured, skip.");

                if (proxy.config.subject == null || string.IsNullOrEmpty(proxy.config.subject.ScenePath))
                {
                    Debug.Log("[SceneDependency] The proxy target config is not point to any scene, fixing...");
                    proxy.config.subject = new SceneReference { ScenePath = path };
                }

                if (proxy.config.subject.ScenePath != path)
                {
                    UnityEditor.EditorGUIUtility.PingObject(proxy.config);
                    throw new System.Exception("[SceneDependency] Saving scene but target config subject is not equal to this scene!");
                }

                if (SceneDependencyIndexEditorAccess.Instance.Index.ContainsKey(path))
                {
                    if (SceneDependencyIndexEditorAccess.Instance.Index[path] != proxy.config) throw new System.Exception("[SceneDependency] Saving scene but proxy mismatch!");
                }
                else
                {
                    Debug.Log("[SceneDependency] Adding the dependency config to index...");
                    SceneDependencyIndexEditorAccess.Instance.Add(path, proxy.config);
                    EditorUtility.SetDirty(SceneDependencyIndexEditorAccess.Instance);
                    AssetDatabase.SaveAssets();
                }
                Debug.Log("[SceneDependency] Preprocess completed: " + scene.name);
            };
            Debug.Log("[SceneDependency] Hooked into EditorSceneManager.sceneSaving.");
        }
#else
        [InitializeOnLoadMethod]
        public static void Hook ()
        {
            EditorSceneManager.sceneSaving += (scene, path) => {
                var roots = scene.GetRootGameObjects();
                var proxy = roots.FirstOrDefault(r => r.GetComponent<SceneDependencyProxy>())?.GetComponent<SceneDependencyProxy>();
                if (proxy == null || proxy.config == null) return;
                if (proxy.config.scenes.Length == 0 && proxy.config.prefabs.Length == 0) 
                    Debug.Log("[SceneDependency] No dependency configured, skip.");

                if (proxy.config.subject == null || string.IsNullOrEmpty(proxy.config.subject.ScenePath))
                {
                    Debug.Log("[SceneDependency] The proxy target config is not point to any scene, fixing...");
                    proxy.config.subject = new SceneReference { ScenePath = path };
                }

                if (proxy.config.subject.ScenePath != path)
                {
                    UnityEditor.EditorGUIUtility.PingObject(proxy.config);
                    throw new System.Exception("[SceneDependency] Saving scene but target config subject is not equal to this scene!");
                }

                if (SceneDependencyIndexEditorAccess.Instance.Index.ContainsKey(path))
                {
                    if (SceneDependencyIndexEditorAccess.Instance.Index[path] != proxy.config) throw new System.Exception("[SceneDependency] Saving scene but proxy mismatch!");
                }
                else
                {
                    Debug.Log("[SceneDependency] Adding the dependency config to index...");
                    SceneDependencyIndexEditorAccess.Instance.Add(path, proxy.config);
                    EditorUtility.SetDirty(SceneDependencyIndexEditorAccess.Instance);
                    AssetDatabase.SaveAssets();
                }
                Debug.Log("[SceneDependency] Preprocess completed: " + scene.name);
            };
            Debug.Log("[SceneDependency] Hooked into EditorSceneManager.sceneSaving.");
        }
#endif
    }
}