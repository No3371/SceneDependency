using System;
using System.Collections.Generic;
using UnityEngine;

namespace BAStudio.SceneDependencies
{
    [Serializable]
    public class Session
    {
        public string MasterSceneName { get; private set; }
        public string MasterSceneAcessor { get; private set; }
        public Dictionary<Type, object> Container { get; private set; }

        public Session(string masterSceneAcessor, string masterSceneName = null)
        {
            Container = new Dictionary<Type, object>();
            MasterSceneAcessor = masterSceneAcessor;
            MasterSceneName = masterSceneName;
        }

        public void Inject<T> (T obj)
        {
            Container[typeof(T)] = obj;
            Debug.Log("Injected: " + typeof(T).Name);
        }

        public void Inject<T, E> (E obj) where E : T
        {
            Container[typeof(T)] = obj;
            Debug.Log("Injected: " + typeof(T).Name);
        }

        public T Get<T>() => (T) Container[typeof(T)];
    }
}