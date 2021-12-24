#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class NewTestScript
{
    // A Test behaves as an ordinary method
    [Test]
    public void NewTestScriptSimplePasses()
    {
        // Use the Assert class to test conditions
    }

    string activeScenePath, latestLoaded, latestUnloaded;

    void LoadedCallback (Scene s, LoadSceneMode mode)
    {
        Assert.IsTrue(s.path == activeScenePath);
        latestLoaded = s.path;
    }

    void UnloadedCallback (Scene s)
    {
        Assert.IsTrue(s.path == activeScenePath);
        latestUnloaded = s.path;
    }
    // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
    // `yield return null;` to skip a frame.
    [UnityTest]
    public IEnumerator TestSceneManagerCallback(
        [ValueSource("keys")] string key,
        [ValueSource("modes")] LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= LoadedCallback;
        SceneManager.sceneLoaded += LoadedCallback;
        SceneManager.sceneUnloaded -= UnloadedCallback;
        SceneManager.sceneUnloaded += UnloadedCallback;

        var addrDefaultSettings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
        var sceneAddressable = addrDefaultSettings.FindAssetEntry(key);
        activeScenePath = sceneAddressable.AssetPath;

        var op = Addressables.LoadSceneAsync(key, mode, true);
        while (!op.IsDone) yield return null;
        yield return new WaitForSeconds(0.1f);
        yield break;
    }

    [UnityTearDown]
    public void TearDown ()
    {
        SceneManager.sceneLoaded -= LoadedCallback;
        SceneManager.sceneUnloaded -= UnloadedCallback;
    }
    public static string[] keys = new string[] { "Dev/DummyScene" };
    public static LoadSceneMode[] modes = new LoadSceneMode[] { LoadSceneMode.Additive, LoadSceneMode.Single };
    public static bool[] reloadOrNot = new bool[] { true, false };

}
#endif