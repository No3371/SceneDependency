# SceneDependency (WIP)
- Automatically build dependencies index everytime a scene is saved, and pack all scenes' dependencies configuration into one single asset.
- The saved dependencies is used when `SceneDependencyRuntime.LoadSceneAsync` is called, all the dependencies will be prepared before the subject scene is loaded.
- Should be highly compatible to all kinds of project setups.
- Supports both Unity's built-in system (SceneManager) and Addressables.

![](Docs/uk1ukKWEsY.gif)