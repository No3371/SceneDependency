using System.Collections;
using System.Collections.Generic;
using BAStudio.SceneDependencies;
using UnityEngine;

public class MainMenu : MonoBehaviour
{
    public SceneReference GameScene;
    public void New ()
    {
        SceneDependencyRuntime.LoadSceneAsync(GameScene, "GAME", UnityEngine.SceneManagement.LoadSceneMode.Single, false);
    }
}
