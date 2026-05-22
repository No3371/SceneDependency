using System.Collections.Generic;
using BAStudio.SceneDependency;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class TestResolveDependencyTree
{
    [Test]
    public void FlatDependencies_AllResolved()
    {
        var root = CreateConfig("root-guid", "dep1-guid", "dep2-guid");
        try
        {
            var result = SceneDependencyRuntime.ResolveDependencyTree(root,
                new Dictionary<string, SceneDependency>());

            Assert.AreEqual(2, result.Count);
            Assert.Contains("dep1-guid", result);
            Assert.Contains("dep2-guid", result);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void DiamondDependencies_NoDuplicates()
    {
        // A -> B, C
        // B -> D
        // C -> D
        var configD = CreateConfig("d-guid");
        var configB = CreateConfig("b-guid", "d-guid");
        var configC = CreateConfig("c-guid", "d-guid");
        var configA = CreateConfig("a-guid", "b-guid", "c-guid");

        var configs = new Dictionary<string, SceneDependency>
        {
            ["b-guid"] = configB,
            ["c-guid"] = configC,
            ["d-guid"] = configD,
        };

        try
        {
            var result = SceneDependencyRuntime.ResolveDependencyTree(configA, configs);

            Assert.AreEqual(3, result.Count, "Should have exactly 3 deps (B, C, D)");
            Assert.Contains("b-guid", result);
            Assert.Contains("c-guid", result);
            Assert.Contains("d-guid", result);

            var uniqueCheck = new HashSet<string>(result);
            Assert.AreEqual(result.Count, uniqueCheck.Count, "Should have no duplicates");

            int indexD = result.IndexOf("d-guid");
            int indexB = result.IndexOf("b-guid");
            int indexC = result.IndexOf("c-guid");
            Assert.Less(indexD, indexB, "D should come before B (leaf-first)");
            Assert.Less(indexD, indexC, "D should come before C (leaf-first)");
        }
        finally
        {
            Object.DestroyImmediate(configA);
            Object.DestroyImmediate(configB);
            Object.DestroyImmediate(configC);
            Object.DestroyImmediate(configD);
        }
    }

    [Test]
    public void EmptyScenes_ReturnsEmpty()
    {
        var root = CreateConfig("root-guid");
        try
        {
            var result = SceneDependencyRuntime.ResolveDependencyTree(root,
                new Dictionary<string, SceneDependency>());
            Assert.AreEqual(0, result.Count);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void NullScenes_ReturnsEmpty()
    {
        var root = ScriptableObject.CreateInstance<SceneDependency>();
        root.subject = new AssetReference("root-guid");
        root.scenes = null;

        var result = SceneDependencyRuntime.ResolveDependencyTree(root,
            new Dictionary<string, SceneDependency>());
        Assert.AreEqual(0, result.Count);

        Object.DestroyImmediate(root);
    }

    static SceneDependency CreateConfig(string subjectGUID, params string[] depGUIDs)
    {
        var config = ScriptableObject.CreateInstance<SceneDependency>();
        config.subject = new AssetReference(subjectGUID);
        config.scenes = new AssetReference[depGUIDs.Length];
        for (int i = 0; i < depGUIDs.Length; i++)
            config.scenes[i] = new AssetReference(depGUIDs[i]);
        return config;
    }
}
