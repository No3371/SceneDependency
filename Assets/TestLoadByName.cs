using System.Collections;
using System.Collections.Generic;
using BAStudio.SceneDependency;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;


public class TestLoadByName : MonoBehaviour
{
    public string sceneName, scenePath;
    void Start ()
    {
        SceneDependencyRuntime.LoadSceneAsync(scenePath, sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single, true);
    }
}