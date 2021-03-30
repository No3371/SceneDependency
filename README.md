# SceneDependency (WIP)
- Automatically build dependencies index everytime a scene is saved, and pack all scenes' dependencies configuration into one single asset.
- The saved dependencies is used when `SceneDependencyRuntime.LoadSceneAsync` is called, all the dependencies will be prepared before the subject scene is loaded.
- Should be highly compatible to all kinds of project setups.
- Supports both Unity's built-in system (SceneManager) and Addressables.

# Example

Depedendency diagram:

> A
> - A.Dep1
>   - A.Dep1.Dep
>   - A.Dep1+2.Dep (Depended by both A.Dep1 and A.Dep2)
> - A.Dep2
>   - A.Dep2.Dep
>   - A.Dep1+2.Dep (Depended by both A.Dep1 and A.Dep2)

We execute `SceneDependencyRuntime.LoadSceneAsync`, targeting scene `A`.

![](Docs/uk1ukKWEsY.gif)

## Usage
- After importing this library, it hooks into scene saving whenever Unity recompiles.
- When a scene is saved, it checks if there's a `SceneDependencyProxy` and does the proxy pointing to a valid `SceneDependency` scriptable object.
- If so, it will make sure this scene is indexed and saved into the `SceneDependencyIndex` scriptable object, the object is project level Singleton.
- As long as the `SceneDependencyIndex` asset is included in build/addressables, all `SceneDependency` should be included as well.
- When `SceneDependencyRuntime.LoadSceneAsync` is called, it lookup all the scenes that needs to be loaded (includnig deps of deps of deps... etc.)
- So it's very easy, create `SceneDependency` scriptable object for every scene that you want to specify its dependencies and add a `SceneDependencyProxy` with the `SceneDependency` referenced. Save. Done.
