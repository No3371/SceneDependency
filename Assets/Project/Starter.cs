using System.Collections;
using System.Collections.Generic;
using BAStudio.SceneDependencies;
using UnityEngine;

public class Starter : MonoBehaviour
{
    public SceneReference MainMenuScene;
    public void Start ()
    {
        SceneDependencyRuntime.LoadSceneAsync(MainMenuScene, "MainMenu", UnityEngine.SceneManagement.LoadSceneMode.Single, false);
    }
}
