using System.Collections;
using System.Collections.Generic;
using BAStudio.SceneDependencies;
using UnityEngine;

public class HUDController : MonoBehaviour, ISceneDependencyListener
{
    // public  pauseButton;
    PauseMenu pauseMenu;
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
