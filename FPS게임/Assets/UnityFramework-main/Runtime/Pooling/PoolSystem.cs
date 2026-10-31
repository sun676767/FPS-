using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace GameLecture.Framework
{
    internal sealed class GameObjectPool
    {
        private readonly GameObject _prefab;
        private readonly ObjectPool<GameObject> _pool;
        private Transform _root;

        public GameObjectPool(GameObject prefab)
        {
            _prefab = prefab;
            _pool = new ObjectPool<GameObject>(Create, Get, Release, Destroy);
        }

        public GameObject Pop() => _pool.Get();
        public void Push(GameObject instance) => _pool.Release(instance);

        public void Reserve(int count)
        {
            var instances = new List<GameObject>(count);
            for (var i = 0; i < count; i++)
                instances.Add(Pop());
            foreach (var instance in instances)
                Push(instance);
        }

        private GameObject Create()
        {
            var instance = ResourceManager.Instance.Instantiate(_prefab.name);
            instance.name = _prefab.name;
            return instance;
        }

        private void Get(GameObject instance)
        {
            instance.transform.SetParent(null);
            instance.SetActive(true);
        }

        private void Release(GameObject instance)
        {
            instance.transform.SetParent(FrameworkUtils.GetOrCreateRoot(ref _root, $"@{_prefab.name}Pool"));
            instance.SetActive(false);
        }

        private void Destroy(GameObject instance) => ResourceManager.Instance.Destroy(instance);
    }

    public sealed class PoolManager : Singleton<PoolManager>
    {
        private readonly Dictionary<string, GameObjectPool> _pools = new();

        public void Reserve(string prefabName, int count) => Reserve(GetPrefab(prefabName), count);

        public void Reserve(GameObject prefab, int count) => GetPool(prefab).Reserve(count);

        public GameObject Pop(string prefabName) => Pop(GetPrefab(prefabName));

        public GameObject Pop(GameObject prefab) => GetPool(prefab).Pop();

        public bool Push(GameObject instance)
        {
            if (instance == null || !_pools.TryGetValue(instance.name, out var pool))
                return false;
            pool.Push(instance);
            return true;
        }

        public void Clear() => _pools.Clear();

        private GameObject GetPrefab(string prefabName) =>
            ResourceManager.Instance.Get<GameObject>(prefabName);

        private GameObjectPool GetPool(GameObject prefab)
        {
            if (prefab == null)
                throw new MissingReferenceException("Pool prefab was not loaded.");
            if (!_pools.TryGetValue(prefab.name, out var pool))
                _pools.Add(prefab.name, pool = new GameObjectPool(prefab));
            return pool;
        }
    }
}
