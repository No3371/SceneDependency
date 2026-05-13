## Design

### Requirements
- Must work in: Editor play mode, Fast enter play mode (no domain reload), Player builds
- Dependencies must be saved separately from scenes (can't access GameObjects until a scene is loaded)

### Architecture
- **Addressables-only**: All scene loading goes through `Addressables.LoadSceneAsync`
- **GUID-keyed index**: `SceneDependencyIndex` maps asset GUIDs to `SceneDependency` configs. GUIDs are stable across renames/moves
- **SceneDependency SO**: Holds `AssetReference subject` + `AssetReference[] scenes` (dependencies)
- **SceneDependencyProxy**: Optional MonoBehaviour placed in scenes, provides `LoadedAsDep` callback

### Index discovery
| Context          | Method                                       |
|------------------|----------------------------------------------|
| Editor (all)     | `AssetDatabase.FindAssets("t:SceneDependencyIndex")` |
| Player builds    | `Addressables.LoadAssetAsync` by label `SceneDependency.Index` |

### Runtime flow (async/await)
1. Resolve full dependency tree recursively (leaf-first, diamond-safe)
2. If Single mode: unload non-dependency scenes (respecting `NoAutoUnloadInSingleLoadMode`)
3. Load all dependencies in parallel via `Addressables.LoadSceneAsync`
4. Load master scene
5. Notify `SceneDependencyProxy.LoadedAsDep` on dep scenes, set master as active

### Handle tracking
- All loaded scenes tracked in `Dictionary<string guid, AsyncOperationHandle<SceneInstance>>`
- `UnloadSceneAsync` releases handles via `Addressables.UnloadSceneAsync`
- All static state reset via `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` for fast play mode

### Editor hooks
- `EditorSceneManager.sceneSaving` keeps the index up to date on every scene save
- Build-time `IPreprocessBuildWithReport` validates index completeness
- Index asset auto-added to `_SceneDependency` Addressable group with label
