using System.Collections;
using System.Collections.Generic;
using BAStudio.SceneDependency;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;


public class TestLoad : MonoBehaviour
{
    public SceneReference scene;
    void Start ()
    {
        // Debug.Log("SceneReference path: " + scene.ScenePath);
        SceneDependencyRuntime.LoadSceneAsync(scene, UnityEngine.SceneManagement.LoadSceneMode.Single, true);
    }
}