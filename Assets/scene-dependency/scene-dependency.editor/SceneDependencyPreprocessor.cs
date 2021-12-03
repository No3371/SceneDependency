using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEditor.AddressableAssets;

namespace BAStudio.SceneDependencies
{
    [RequireComponent(typeof(SceneDependencyProxy))]
    public class SceneDependencyPreprocessor : MonoBehaviour, IPreprocessBuildWithReport
    {
        public int callbackOrder => 10;

        public void OnPreprocessBuild(BuildReport report)
        {

            SceneDependencyProxy holder = this.GetComponent<SceneDependencyProxy>();
            holder.forceReference = SceneDependencyProxy.cachedForceReference = SceneDependencyIndexEditorAccess.Instance;
    #if !SCENE_DEP_OVERRIDE || SCENE_DEP_LEGACY
            SceneDependencyIndexEditorAccess.Instance.Index.Add(holder.config.subject.ScenePath, holder.config);
    #elif !SCENE_DEP_OVERRIDE || SCENE_DEP_ADDRESSABLE
            SceneDependencyIndexEditorAccess.Instance.Index.Add(
                UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(holder.config.subject.AssetGUID).address,
                holder.config
            );
    #endif
            
        }

    }
}