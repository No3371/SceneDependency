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
            // Directives:
            // - Make sure all config asset is added to the index
            // - Make sure the index asset is addressable
            // - Make sure the index use scene addresses as keys to config assets
            EditorSceneManager.sceneSaving += (scene, path) =>
            {

                var roots = scene.GetRootGameObjects();
                var proxy = roots.FirstOrDefault(r => r.GetComponent<SceneDependencyProxy>())?.GetComponent<SceneDependencyProxy>();
                if (proxy == null || proxy.config == null) return;
                if (proxy.config.scenes.Length == 0) 
                    Debug.Log("[SceneDependencies] No dependency configured, skip.");

                var sceneGUID = AssetDatabase.GUIDFromAssetPath(path).ToString();
                var addrDefaultSettings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.GetSettings(true);
                var sceneAddressable = addrDefaultSettings.FindAssetEntry(sceneGUID);
                var sdGroup = addrDefaultSettings.FindGroup((g) => {
                    return g.Name == "_SceneDependency";
                });
                if (sdGroup == null) sdGroup = addrDefaultSettings.CreateGroup("_SceneDependency", false, true, true, addrDefaultSettings.DefaultGroup.Schemas);

                if (proxy.config.subject == null || proxy.config.subject.editorAsset == null)
                {
                    Debug.Log("[SceneDependencies] The proxy target config is not pointing to any scene, fixing...");
                    proxy.config.subject = addrDefaultSettings.CreateAssetReference(sceneGUID);
                }

                if (proxy.config.subject.AssetGUID != sceneGUID)
                {
                    UnityEditor.EditorGUIUtility.PingObject(proxy.config);
                    throw new System.Exception("[SceneDependencies] Saving scene but target config subject is not equal to this scene!");
                }

                // Mapping scene -> dependency
                if (SceneDependencyIndexEditorAccess.Instance.Index.ContainsKey(sceneAddressable.address))
                {
                    if (SceneDependencyIndexEditorAccess.Instance.Index[sceneAddressable.address] != proxy.config)
                        throw new System.Exception("[SceneDependencies] Saving scene but config object mismatch!");
                }
                else
                {
                    Debug.Log("[SceneDependencies] Adding the dependency config to index...");
                    SceneDependencyIndexEditorAccess.Instance.Add(GetAddressFromAsset(proxy.config), proxy.config);
                    EditorUtility.SetDirty(SceneDependencyIndexEditorAccess.Instance);
                    AssetDatabase.SaveAssets();
                }
                Debug.Log("[SceneDependencies] Preprocess completed: " + scene.name);
            };
            Debug.Log("[SceneDependencies] Hooked into EditorSceneManager.sceneSaving.");
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
    }
}