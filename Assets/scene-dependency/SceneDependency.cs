using UnityEngine;
using UnityEngine.AddressableAssets;

namespace BAStudio.SceneDependency
{
    [CreateAssetMenu(menuName = "SceneDependency")]
    public class SceneDependency : ScriptableObject
    {
        public AssetReference subject;
        public AssetReference[] scenes;
        public bool NoAutoUnloadInSingleLoadMode;
    }
}
