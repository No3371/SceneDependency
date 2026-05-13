using BAStudio.SceneDependency;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class TestLoadByName : MonoBehaviour
{
    public AssetReference scene;
    void Start ()
    {
        SceneDependencyRuntime.LoadSceneAsync(scene, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
}
