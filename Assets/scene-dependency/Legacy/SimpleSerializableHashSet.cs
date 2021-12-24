#define LOG
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace BAStudio.SceneDependencies
{
    public class SimpleSerializableHashSet<T> : ISerializationCallbackReceiver
    {
        HashSet<T> RuntimeSet
        {
            get;
            set;
        }

        bool Changed { get; set; }

        public bool Add(T value)
        {
            bool result = RuntimeSet.Add(value);
            if (result) Changed = true;
            return result;
        }
        public bool Remove(T value)
        {
            bool result = RuntimeSet.Remove(value);
            if (result) Changed = true;
            return result;
        }
        public bool Contains(T value)
        {
            return RuntimeSet.Contains(value);
        }

        [SerializeField]
        private T[] serialized;

        public void OnAfterDeserialize()
        {
            if (RuntimeSet == null && serialized != null) RuntimeSet = new HashSet<T>();
            RuntimeSet.Clear();
            RuntimeSet.UnionWith(serialized);
        }

        public void OnBeforeSerialize()
        {
            if (!Changed) return;
            serialized = RuntimeSet.ToArray();
        }
    }
}
