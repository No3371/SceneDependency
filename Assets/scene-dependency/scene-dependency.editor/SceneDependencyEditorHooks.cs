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
#if SD_RES_LEGACY
        [InitializeOnLoadMethod]
        public static void Hook ()
        {
            EditorSceneManager.sceneSaving += (scene, path) => {
                if (EditorApplication.isPlaying) return;
                var roots = scene.GetRootGameObjects();
                var proxy = roots.FirstOrDefault(r => r.GetComponent<SceneDependencyProxy>())?.GetComponent<SceneDependencyProxy>();
                if (proxy == null || proxy.config == null) return;
                if (proxy.config.scenes.Length == 0) 
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
            // Directives:
            // - Make sure all config asset is added to the index
            // - Make sure the index asset is addressable
            // - Make sure the index use scene addresses as keys to config assets
            EditorSceneManager.sceneSaving += (scene, path) =>
            {

                var roots = scene.GetRootGameObjects();
                var proxy = roots.FirstOrDefault(r => r.GetComponent<SceneDependencyProxy>())?.GetComponent<SceneDependencyProxy>();
                if (proxy == null || proxy.config == null) return;
                if (proxy.config.scenes.Length == 0 && proxy.config.prefabs.Length == 0) 
                    Debug.Log("[SceneDependency] No dependency configured, skip.");

                var sceneGUID = AssetDatabase.GUIDFromAssetPath(path).ToString();
                var addrDefaultSettings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.GetSettings(true);
                var sceneAddressable = addrDefaultSettings.FindAssetEntry(sceneGUID);
                var sdGroup = addrDefaultSettings.FindGroup((g) => {
                    return g.Name == "_SceneDependency";
                });
                if (sdGroup == null) sdGroup = addrDefaultSettings.CreateGroup("_SceneDependency", false, true, true, addrDefaultSettings.DefaultGroup.Schemas);

                if (proxy.config.subject == null || proxy.config.subject.editorAsset == null)
                {
                    Debug.Log("[SceneDependency] The proxy target config is not point to any scene, fixing...");
                    proxy.config.subject = addrDefaultSettings.CreateAssetReference(sceneGUID);
                }

                if (proxy.config.subject.AssetGUID != sceneGUID)
                {
                    UnityEditor.EditorGUIUtility.PingObject(proxy.config);
                    throw new System.Exception("[SceneDependency] Saving scene but target config subject is not equal to this scene!");
                }

                // Mapping scene -> dependency
                if (SceneDependencyIndexEditorAccess.Instance.Index.ContainsKey(sceneAddressable.address))
                {
                    if (SceneDependencyIndexEditorAccess.Instance.Index[sceneAddressable.address] != proxy.config)
                        throw new System.Exception("[SceneDependency] Saving scene but config object mismatch!");
                }
                else
                {
                    Debug.Log("[SceneDependency] Adding the dependency config to index...");
                    SceneDependencyIndexEditorAccess.Instance.Add(GetAddressFromAsset(proxy.config), proxy.config);
                    EditorUtility.SetDirty(SceneDependencyIndexEditorAccess.Instance);
                    AssetDatabase.SaveAssets();
                }
                Debug.Log("[SceneDependency] Preprocess completed: " + scene.name);
            };
            Debug.Log("[SceneDependency] Hooked into EditorSceneManager.sceneSaving.");
        }
        
        public static string GetAddressFromAsset(Object target)
        {
            string path = AssetDatabase.GetAssetPath(target);
            string guid = AssetDatabase.AssetPathToGUID(path);
            var assetEntry = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(guid);
            return assetEntry?.address;
        }
        
        public static string GetGUIDFromAsset(Object target)
        {
            return  AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target));
        }
#endif
    }
}