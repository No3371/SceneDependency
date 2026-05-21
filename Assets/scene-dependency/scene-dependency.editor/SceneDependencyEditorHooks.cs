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

            var sceneGUID = AssetDatabase.GUIDFromAssetPath(path).ToString();
            var addrSettings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var sdGroup = addrSettings.FindGroup(g => g.Name == "_SceneDependency");
            if (sdGroup == null)
                sdGroup = addrSettings.CreateGroup("_SceneDependency", false, true, true, addrSettings.DefaultGroup.Schemas);

            if (proxy.config.subject == null || proxy.config.subject.editorAsset == null)
            {
                Debug.Log("[SceneDependency] The proxy target config is not pointing to any scene, fixing...");
                proxy.config.subject = addrSettings.CreateAssetReference(sceneGUID);
            }

            if (proxy.config.subject.AssetGUID != sceneGUID)
            {
                EditorGUIUtility.PingObject(proxy.config);
                Debug.LogError("[SceneDependency] Saving scene but target config subject is not equal to this scene!");
                return;
            }

            ValidateDepsAddressable(proxy.config, addrSettings);
            EnsureConfigAddressable(proxy.config, sceneGUID, addrSettings, sdGroup);

            Debug.Log("[SceneDependency] Preprocess completed: " + scene.name);
        }

        static void EnsureConfigAddressable(SceneDependency config, string sceneGUID,
            UnityEditor.AddressableAssets.Settings.AddressableAssetSettings settings,
            UnityEditor.AddressableAssets.Settings.AddressableAssetGroup group)
        {
            var configAssetPath = AssetDatabase.GetAssetPath(config);
            if (string.IsNullOrEmpty(configAssetPath)) return;

            var configAssetGUID = AssetDatabase.GUIDFromAssetPath(configAssetPath).ToString();
            var entry = settings.FindAssetEntry(configAssetGUID);
            if (entry == null)
                entry = settings.CreateOrMoveEntry(configAssetGUID, group);

            if (entry.address != sceneGUID)
            {
                entry.address = sceneGUID;
                Debug.Log("[SceneDependency] Set config Addressable address to: " + sceneGUID);
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
