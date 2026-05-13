using System.Collections;
using System.Linq;
using BAStudio.SceneDependency;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class TestLoadBySceneRef
{
    public GameObject runnerHost;

    [UnitySetUp]
    public IEnumerator Setup ()
    {
        if (runnerHost != null) yield break;
        runnerHost = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(go =>
                    go.GetComponents(typeof(MonoBehaviour)).Any(c => c.GetType().Name == "PlaymodeTestsController"));
        GameObject.DontDestroyOnLoad(runnerHost);
        yield break;
    }

    [UnityTearDown]
    public IEnumerator TearDown ()
    {
        yield break;
    }

    [UnityTest]
    public IEnumerator TestLoadByAddressablePasses(
        [ValueSource("accessors")] string accessor,
        [ValueSource("modes")] LoadSceneMode mode)
    {
        var handle = SceneDependencyRuntime.LoadSceneAsync(accessor, mode);
        while (!handle.IsDone)
        {
            yield return null;
        }

        Assert.AreEqual(AsyncOperationStatus.Succeeded, handle.Status, "Scene load failed for {0}", accessor);
        Assert.Pass();
    }

    public static string[] accessors = new string[] { "Assets/Scenes/A.unity" };
    public static LoadSceneMode[] modes = new LoadSceneMode[] { LoadSceneMode.Additive, LoadSceneMode.Single };
}
