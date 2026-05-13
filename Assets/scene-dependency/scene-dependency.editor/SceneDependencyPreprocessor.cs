using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BAStudio.SceneDependency
{
    public class SceneDependencyPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 10;

        public void OnPreprocessBuild(BuildReport report)
        {
            var index = SceneDependencyIndexEditorAccess.Instance;
            if (index == null)
            {
                Debug.LogWarning("[SceneDependency] Index not found at build time.");
                return;
            }

            if (index.Index.Count == 0)
            {
                Debug.LogWarning("[SceneDependency] Index is empty at build time. Save scenes with SceneDependencyProxy to populate it.");
            }
        }
    }
}
