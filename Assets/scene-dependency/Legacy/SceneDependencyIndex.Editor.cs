#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BAStudio.SceneDependencies
{
    public partial class SceneDependencyIndex : ScriptableObject
    {
        [ContextMenu("Rebuild")]
        public void RebuildIndex ()
        {
            var found = AssetDatabase.FindAssets("t:SceneDependencies");
            Debug.LogFormat("=== SceneDependencies ===");
            SceneDependencyIndexEditorAccess.Instance.Index.Clear();
            foreach (var entry in found)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(entry);
                Debug.LogFormat("Found {0}", assetPath);
                var sd = AssetDatabase.LoadAssetAtPath<SceneDependencies>(assetPath);
                SceneDependencyIndexEditorAccess.Instance.Index.Add(sd.subject.ScenePath, sd);
            }
            Debug.LogFormat("=== SceneDependencies ===");
        }

        
        [ContextMenu("Verify")]
        public void Verify ()
        {
            if (SceneDependencyIndexEditorAccess.Verify())
                Debug.LogFormat("<color=green>[SceneDependencies] ALL GOOD</color>");
            else
                Debug.LogFormat("<color=red>[SceneDependencies] INVALID</color>");
        }
    }

}
#endif