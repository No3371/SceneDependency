// using System.Collections;
// using System.Linq;
// using BAStudio.SceneDependencies;
// using NUnit.Framework;
// using UnityEngine;
// using UnityEngine.SceneManagement;
// using UnityEngine.TestTools;
// using Unity.Addressables;

// public class TestLoadByAddress
// {
//     public GameObject runnerHost;

//     [UnitySetUp]
//     public IEnumerator Setup ()
//     {
//         if (runnerHost != null) yield break;
//         runnerHost = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(go =>
//                     go.GetComponents(typeof(MonoBehaviour)).Any(c => c.GetType().Name == "PlaymodeTestsController"));
//         GameObject.DontDestroyOnLoad(runnerHost);
//         yield break;
//     }

//     [UnityTearDown]
//     public IEnumerator TearDown ()
//     {
//         yield break;
//     }

//     // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
//     // `yield return null;` to skip a frame.
//     [UnityTest]
//     // [Timeout(5000)]
//     public IEnumerator TestLoadByAddressWithEnumeratorPasses(
//         [ValueSource("addresses")] string addr,
//         [ValueSource("modes")] LoadSceneMode mode,
//         [ValueSource("reloadOrNot")] bool reloadLoadedScenes)
//     {
//         // var aow = SceneDependencyRuntime.LoadSceneAsync(addr, mode, reloadLoadedScenes);
//         // while (aow.value == null || !aow.value.isDone)
//         // {
//         //     yield return null;
//         // }

//         // var required = SceneDependencyRuntime.ResolveDependencyTree(SceneDependencyIndex.AutoInstance.Index[addr]);
//         // var loc = Addressables.LoadResourceLocationsAsync(required as IEnumerable, Addressables.MergeMode.Union);
//         // var locations = loc.Result;
//         // var depSceneInternalPaths = locations.Select(l => l.InternalId).ToArray();
//         // foreach (string s in depSceneInternalPaths)
//         // {
//         //     Assert.IsTrue(SceneManager.GetSceneByPath(s).isLoaded, "Required scene {0} is not loaded!", s);
//         // }
//         // Assert.IsTrue(SceneManager.GetSceneByPath(path).isLoaded, "Master scene {0} is not loaded!", path);
//         // yield return new WaitForSeconds(1);
//         // Assert.Pass();
//         yield break;

//     }

//     public static string[] addresses = new string[] { "SceneA" };
//     public static LoadSceneMode[] modes = new LoadSceneMode[] { LoadSceneMode.Additive, LoadSceneMode.Single };
//     public static bool[] reloadOrNot = new bool[] { true, false }; 
// }