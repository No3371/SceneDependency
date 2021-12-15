using System;
using System.Collections.Generic;

namespace BAStudio.SceneDependencies
{
    public class Session
    {
        public Dictionary<Type, object> Container { get; private set; }

        public Session()
        {
            Container = new Dictionary<Type, object>();
        }

        public void Set<T> (T obj) => Container[typeof(T)] = obj;
        public T Get<T>() => (T) Container[typeof(T)];
    }
}