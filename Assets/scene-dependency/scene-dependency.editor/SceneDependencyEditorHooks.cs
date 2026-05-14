using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BAStudio.SceneDependency
{
    public static class SceneDependencyEditorHooks
    {
        [InitializeOnLoadMethod]
        public static void Hook ()
        {
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            Debug.Log("[SceneDependency] Hooked into EditorSceneManager.sceneSaving.");
        }

        static void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
        {
            if (EditorApplication.isPlaying) return;

            SceneDependencyProxy proxy = null;
            foreach (var go in scene.GetRootGameObjects())
            {
                proxy = go.GetComponent<SceneDependencyProxy>();
                if (proxy != null) break;
            }
            if (proxy == null || proxy.config == null) return;
            if (proxy.config.scenes == null || proxy.config.scenes.Length == 0)
            {
                Debug.Log("[SceneDependency] No dependency configured, skip.");
                return;
            }

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
                Debug.LogError("[SceneDependency] Saving scene but target config subject is not equal to this scene!");
                return;
            }

            ValidateDepsAddressable(proxy.config, addrDefaultSettings);
            EnsureIndexAddressable(addrDefaultSettings, sdGroup);

            if (SceneDependencyIndexEditorAccess.Instance.TryGet(sceneGUID, out var existing))
            {
                if (existing != proxy.config)
                {
                    Debug.LogError("[SceneDependency] Saving scene but config object mismatch!");
                    return;
                }
            }
            else
            {
                Debug.Log("[SceneDependency] Adding the dependency config to index...");
                SceneDependencyIndexEditorAccess.Instance.Add(sceneGUID, proxy.config);
                EditorUtility.SetDirty(SceneDependencyIndexEditorAccess.Instance);
                AssetDatabase.SaveAssets();
            }
            Debug.Log("[SceneDependency] Preprocess completed: " + scene.name);
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

        static void ValidateDepsAddressable(SceneDependency config,
            UnityEditor.AddressableAssets.Settings.AddressableAssetSettings settings)
        {
            if (config.scenes == null) return;
            for (int i = 0; i < config.scenes.Length; i++)
            {
                var depRef = config.scenes[i];
                if (depRef == null || string.IsNullOrEmpty(depRef.AssetGUID)) continue;
                var entry = settings.FindAssetEntry(depRef.AssetGUID);
                if (entry == null)
                {
                    var depPath = AssetDatabase.GUIDToAssetPath(depRef.AssetGUID);
                    Debug.LogWarning(string.Format(
                        "[SceneDependency] Dependency scene '{0}' (GUID: {1}) is not marked as Addressable. " +
                        "It will fail to load at runtime. Mark it Addressable in the Addressables Groups window.",
                        depPath, depRef.AssetGUID));
                }
            }
        }
    }
}
