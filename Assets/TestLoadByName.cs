using BAStudio.SceneDependency;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class TestLoadByName : MonoBehaviour
{
    public AssetReference scene;
    async void Start ()
    {
        try
        {
            await SceneDependencyRuntime.LoadSceneAsync(scene, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e, this);
        }
    }
}
