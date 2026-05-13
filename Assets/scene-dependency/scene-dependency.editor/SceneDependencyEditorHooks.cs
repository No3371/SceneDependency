using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BAStudio.SceneDependency
{
    [InitializeOnLoad]
    public static class SceneDependencyEditorHooks
    {
        [InitializeOnLoadMethod]
        public static void Hook ()
        {
            EditorSceneManager.sceneSaving += (scene, path) =>
            {
                var roots = scene.GetRootGameObjects();
                var proxy = roots.FirstOrDefault(r => r.GetComponent<SceneDependencyProxy>())?.GetComponent<SceneDependencyProxy>();
                if (proxy == null || proxy.config == null) return;
                if (proxy.config.scenes.Length == 0)
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
                    EditorGUIUtility.PingObject(proxy.config);
                    throw new System.Exception("[SceneDependency] Saving scene but target config subject is not equal to this scene!");
                }

                if (SceneDependencyIndexEditorAccess.Instance.Index.ContainsKey(sceneAddressable.address))
                {
                    if (SceneDependencyIndexEditorAccess.Instance.Index[sceneAddressable.address] != proxy.config)
                        throw new System.Exception("[SceneDependency] Saving scene but config object mismatch!");
                }
                else
                {
                    Debug.Log("[SceneDependency] Adding the dependency config to index...");
                    SceneDependencyIndexEditorAccess.Instance.Add(sceneAddressable.address, proxy.config);
                    EditorUtility.SetDirty(SceneDependencyIndexEditorAccess.Instance);
                    AssetDatabase.SaveAssets();
                }
                Debug.Log("[SceneDependency] Preprocess completed: " + scene.name);
            };
            Debug.Log("[SceneDependency] Hooked into EditorSceneManager.sceneSaving.");
        }

        public static string GetGUIDFromAsset(Object target)
        {
            return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target));
        }
    }
}
