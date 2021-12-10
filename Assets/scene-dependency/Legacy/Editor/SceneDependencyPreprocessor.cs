using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

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
            SceneDependencyIndexEditorAccess.Instance.Index.Add(holder.config.subject.ScenePath, holder.config);
            
        }

    }
}