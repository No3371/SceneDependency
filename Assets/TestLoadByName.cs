using System.Collections;
using System.Collections.Generic;
using BAStudio.SceneDependency;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;


public class TestLoadByName : MonoBehaviour
{
    public string sceneName;
    void Start ()
    {
        SceneDependencyRuntime.LoadSceneAsync(sceneName, sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
}