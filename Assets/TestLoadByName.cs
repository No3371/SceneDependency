#if !SCENE_DEP_OVERRIDE || SCENE_DEP_LEGACY
using System.Collections;
using System.Collections.Generic;
using BAStudio.SceneDependencies;
using UnityEditor;
using UnityEngine;

public class TestLoadByName : MonoBehaviour
{
    public string sceneName, scenePath;
    void Start ()
    {
        SceneDependencyRuntime.LoadSceneAsync(scenePath, sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single, true);
    }
}

#elif !SCENE_DEP_OVERRIDE || SCENE_DEP_ADDRESSABLES
using System.Collections;
using System.Collections.Generic;
using BAStudio.SceneDependencies;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;


public class TestLoadByAddress : MonoBehaviour
{
    public string sceneAddress;
    void Start ()
    {
        SceneDependencyRuntime.LoadSceneAsync(sceneAddress, UnityEngine.SceneManagement.LoadSceneMode.Single, true);
    }
}
#endif