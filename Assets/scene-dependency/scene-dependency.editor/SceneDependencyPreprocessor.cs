using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEditor.AddressableAssets;

namespace BAStudio.SceneDependency
{
    [RequireComponent(typeof(SceneDependencyProxy))]
    public class SceneDependencyPreprocessor : MonoBehaviour, IPreprocessBuildWithReport
    {
        public int callbackOrder => 10;

        public void OnPreprocessBuild(BuildReport report)
        {

            SceneDependencyProxy holder = this.GetComponent<SceneDependencyProxy>();
            holder.forceReference = SceneDependencyProxy.cachedForceReference = SceneDependencyIndexEditorAccess.Instance;
        #if SD_RES_LEGACY
            SceneDependencyIndexEditorAccess.Instance.Index.Add(holder.config.subject.ScenePath, holder.config);
        #else
            SceneDependencyIndexEditorAccess.Instance.Index.Add(
                UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(holder.config.subject.AssetGUID).address,
                holder.config
            );
        #endif
            
        }

    }
}