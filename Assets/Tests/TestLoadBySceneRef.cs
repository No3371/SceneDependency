using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BAStudio.SceneDependency;
using NUnit.Framework;
using UnityEngine;
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

    // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
    // `yield return null;` to skip a frame.
    [UnityTest]
    // [Timeout(5000)]
    public IEnumerator TestLoadBySceneRefWithEnumeratorPasses(
        [ValueSource("names")] string name,
        [ValueSource("paths")] string path,
        [ValueSource("modes")] LoadSceneMode mode,
        [ValueSource("reloadOrNot")] bool reloadLoadedScenes)
    {
        var aow = SceneDependencyRuntime.LoadSceneAsync(path, name, UnityEngine.SceneManagement.LoadSceneMode.Single, true);
        while (aow.value == null || !aow.value.isDone)
        {
            yield return null;
        }

        var required = SceneDependencyRuntime.ResolveDependencyTree(SceneDependencyIndex.AutoInstance.Index[path]);
        foreach (string s in required)
        {
            Assert.IsTrue(SceneManager.GetSceneByPath(s).isLoaded, "Required scene {0} is not loaded!", s);
        }
        Assert.IsTrue(SceneManager.GetSceneByPath(path).isLoaded, "Master scene {0} is not loaded!", path);
        Assert.Pass();

    }

    public static string[] names = new string[] { "A" };
    public static string[] paths = new string[] { "Assets/Scenes/A.unity" };
    public static LoadSceneMode[] modes = new LoadSceneMode[] { LoadSceneMode.Additive, LoadSceneMode.Single };
    public static bool[] reloadOrNot = new bool[] { true, false }; 
}
