using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
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
                if (EditorApplication.isPlaying) return;

                var roots = scene.GetRootGameObjects();
                var proxy = roots.FirstOrDefault(r => r.GetComponent<SceneDependencyProxy>())?.GetComponent<SceneDependencyProxy>();
                if (proxy == null || proxy.config == null) return;
                if (proxy.config.scenes.Length == 0)
                    Debug.Log("[SceneDependency] No dependency configured, skip.");

                var sceneGUID = AssetDatabase.GUIDFromAssetPath(path).ToString();
                var addrDefaultSettings = AddressableAssetSettingsDefaultObject.GetSettings(true);
                var sdGroup = addrDefaultSettings.FindGroup(g => g.Name == "_SceneDependency");
                if (sdGroup == null)
                    sdGroup = addrDefaultSettings.CreateGroup("_SceneDependency", false, true, true, addrDefaultSettings.DefaultGroup.Schemas);

                if (proxy.config.subject == null || proxy.config.subject.editorAsset == null)
                {
                    Debug.Log("[SceneDependency] The proxy target config is not pointing to any scene, fixing...");
                    proxy.config.subject = addrDefaultSettings.CreateAssetReference(sceneGUID);
                }

                if (proxy.config.subject.AssetGUID != sceneGUID)
                {
                    EditorGUIUtility.PingObject(proxy.config);
                    throw new System.Exception("[SceneDependency] Saving scene but target config subject is not equal to this scene!");
                }

                EnsureIndexAddressable(addrDefaultSettings, sdGroup);

                if (SceneDependencyIndexEditorAccess.Instance.ContainsKey(sceneGUID))
                {
                    if (SceneDependencyIndexEditorAccess.Instance.Index[sceneGUID] != proxy.config)
                        throw new System.Exception("[SceneDependency] Saving scene but config object mismatch!");
                }
                else
                {
                    Debug.Log("[SceneDependency] Adding the dependency config to index...");
                    SceneDependencyIndexEditorAccess.Instance.Add(sceneGUID, proxy.config);
                    EditorUtility.SetDirty(SceneDependencyIndexEditorAccess.Instance);
                    AssetDatabase.SaveAssets();
                }
                Debug.Log("[SceneDependency] Preprocess completed: " + scene.name);
            };
            Debug.Log("[SceneDependency] Hooked into EditorSceneManager.sceneSaving.");
        }

        static void EnsureIndexAddressable(UnityEditor.AddressableAssets.Settings.AddressableAssetSettings settings,
            UnityEditor.AddressableAssets.Settings.AddressableAssetGroup group)
        {
            var indexGUID = SceneDependencyIndexEditorAccess.IndexAssetGUID;
            if (string.IsNullOrEmpty(indexGUID)) return;

            var entry = settings.FindAssetEntry(indexGUID);
            if (entry == null)
            {
                entry = settings.CreateOrMoveEntry(indexGUID, group);
                entry.SetLabel(SceneDependencyIndex.AddressableLabel, true, true);
                Debug.Log("[SceneDependency] Index asset added to Addressables group with label: " + SceneDependencyIndex.AddressableLabel);
            }
            else if (!entry.labels.Contains(SceneDependencyIndex.AddressableLabel))
            {
                entry.SetLabel(SceneDependencyIndex.AddressableLabel, true, true);
            }
        }

        public static string GetGUIDFromAsset(Object target)
        {
            return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target));
        }
    }
}
