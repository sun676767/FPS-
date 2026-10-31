using System.Collections.Generic;
using UnityEngine;

namespace GameLecture.Framework
{
    public class ObjectBase : MonoBehaviour
    {
        public bool Pooling { get; internal set; }
        protected virtual void Awake() => Initialize();
        public virtual void Initialize() { }
    }

    public sealed class ObjectManager : Singleton<ObjectManager>
    {
        private readonly HashSet<ObjectBase> _objects = new();

        public T Spawn<T>(string prefabName, Transform parent = null, bool pooling = false)
            where T : ObjectBase
        {
            var go = pooling
                ? PoolManager.Instance.Pop(prefabName)
                : ResourceManager.Instance.Instantiate(prefabName);

            if (go == null)
                return null;

            go.name = prefabName;
            go.transform.SetParent(parent);
            var instance = go.GetOrAddComponent<T>();
            instance.Pooling = pooling;
            _objects.Add(instance);
            return instance;
        }

        public void Despawn(ObjectBase instance)
        {
            if (instance == null)
                return;

            _objects.Remove(instance);
            if (instance.Pooling)
            {
                instance.Initialize();
                PoolManager.Instance.Push(instance.gameObject);
            }
            else
            {
                ResourceManager.Instance.Destroy(instance.gameObject);
            }
        }

        public void DespawnAll()
        {
            foreach (var instance in new List<ObjectBase>(_objects))
                Despawn(instance);
        }
    }
}
