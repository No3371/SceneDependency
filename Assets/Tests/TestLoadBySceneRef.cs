using System.Collections;
using System.Linq;
using BAStudio.SceneDependency;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class TestLoadByAddressable
{
    public GameObject runnerHost;

    [UnitySetUp]
    public IEnumerator Setup ()
    {
        if (runnerHost != null) yield break;
        runnerHost = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(go =>
                    go.GetComponents(typeof(MonoBehaviour)).Any(c => c.GetType().Name == "PlaymodeTestsController"));
        if (runnerHost != null) GameObject.DontDestroyOnLoad(runnerHost);
        yield break;
    }

    [UnityTearDown]
    public IEnumerator TearDown ()
    {
        yield break;
    }

    [UnityTest]
    public IEnumerator TestLoadSceneAsyncByGUID(
        [ValueSource("sceneGUIDs")] string sceneGUID,
        [ValueSource("modes")] LoadSceneMode mode)
    {
        var task = SceneDependencyRuntime.LoadSceneAsync(sceneGUID, mode);
        while (!task.IsCompleted)
        {
            yield return null;
        }

        Assert.IsFalse(task.IsFaulted, "LoadSceneAsync faulted: {0}", task.Exception);
        var handle = task.Result;
        Assert.AreEqual(AsyncOperationStatus.Succeeded, handle.Status, "Scene load failed for GUID {0}", sceneGUID);

        var masterScene = handle.Result.Scene;
        Assert.IsTrue(masterScene.IsValid() && masterScene.isLoaded, "Master scene should be loaded");

        if (SceneDependencyRuntime.TryGetCachedConfig(sceneGUID, out var deps)
            && deps != null && deps.scenes != null && deps.scenes.Length > 0)
        {
            int loadedCount = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isLoaded)
                    loadedCount++;
            }
            Assert.GreaterOrEqual(loadedCount, deps.scenes.Length + 1,
                "Expected at least {0} loaded scenes (master + {1} deps), but only {2} loaded",
                deps.scenes.Length + 1, deps.scenes.Length, loadedCount);
        }

        Assert.Pass();
    }

    // Populate with actual Addressable scene GUIDs from your project to enable these tests.
    public static string[] sceneGUIDs = new string[] { };
    public static LoadSceneMode[] modes = new LoadSceneMode[] { LoadSceneMode.Additive, LoadSceneMode.Single };
}
