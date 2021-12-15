using System.Collections;
using System.Collections.Generic;
using BAStudio.SceneDependencies;
using UnityEngine;
using UnityEngine.UIElements;

public class PauseMenu : MonoBehaviour, ISceneDependencyListener
{
    public CanvasGroup canvasGroup;


    public void Toggle () => Toggle(!canvasGroup.interactable);

    public void Toggle (bool state)
    {
        canvasGroup.alpha = state? 1 : 0;
        canvasGroup.interactable = state? true : false;
    }

    // Start is called before the first frame update
    void Start()
    {
        Toggle(false);
    }

    public void Loaded(Session session)
    {
    }

    public void LoadedAsDep(Session session)
    {
        session.Inject(this);
    }

    public void AllDepsReady(Session session)
    {
    }
}
