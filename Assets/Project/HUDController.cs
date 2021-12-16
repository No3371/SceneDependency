using System.Collections;
using System.Collections.Generic;
using BAStudio.SceneDependencies;
using UnityEngine;
using UnityEngine.UI;

public class HUDController : MonoBehaviour, ISceneDependencyListener
{
    public Button pauseButton;
    PauseMenu pauseMenu;

    void Start ()
    {
        Debug.Log("Start: " + this.GetType().Name);
        pauseButton.onClick.AddListener(() => pauseMenu.Toggle(true));
    }
    public void AllDepsReady(Session session)
    {
        pauseMenu = session.Get<PauseMenu>();
    }

    public void Loaded(Session session)
    {
    }

    public void LoadedAsDep(Session session)
    {
    }
}
