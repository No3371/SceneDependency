using System.Collections;
using System.Collections.Generic;
using BAStudio.SceneDependencies;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;


public class TestLoad : MonoBehaviour
{
    public SceneReference scene;
    void Start ()
    {
        // Debug.Log("SceneReference path: " + scene.ScenePath);
    }
}