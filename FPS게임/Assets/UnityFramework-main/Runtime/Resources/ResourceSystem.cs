using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameLecture.Framework
{
    public interface IResourceLoader
    {
        void LoadAll(Action<float> onProgress = null, Action onComplete = null);
        T Get<T>(string key) where T : UnityEngine.Object;
        GameObject Instantiate(string key, Transform parent = null);
        void Destroy(GameObject instance);
        void ReleaseAll();
    }

    public sealed class ResourceManager : Singleton<ResourceManager>
    {
        private IResourceLoader _loader;
        public IResourceLoader Loader => _loader ??= new ResourcesLoader();

        public void SetLoader(IResourceLoader loader) =>
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));

        public void LoadAll(Action<float> onProgress = null, Action onComplete = null) =>
            Loader.LoadAll(onProgress, onComplete);

        public T Get<T>(string key) where T : UnityEngine.Object => Loader.Get<T>(key);
        public GameObject Instantiate(string key, Transform parent = null) => Loader.Instantiate(key, parent);
        public void Destroy(GameObject instance) => Loader.Destroy(instance);
        public void ReleaseAll() => Loader.ReleaseAll();
    }

    public sealed class ResourcesLoader : IResourceLoader
    {
        private readonly string[] _paths;
        private readonly Dictionary<string, UnityEngine.Object> _resources = new();

        public ResourcesLoader(params string[] paths)
        {
            _paths = paths is { Length: > 0 }
                ? paths
                : new[] { "Prefabs", "Config", "Data/JsonData", "Sounds" };
        }

        public void LoadAll(Action<float> onProgress = null, Action onComplete = null)
        {
            for (var i = 0; i < _paths.Length; i++)
            {
                foreach (var resource in Resources.LoadAll(_paths[i]))
                    _resources.TryAdd(Key(resource.name, resource.GetType()), resource);

                onProgress?.Invoke((i + 1f) / _paths.Length);
            }

            onComplete?.Invoke();
        }

        public T Get<T>(string key) where T : UnityEngine.Object =>
            _resources.TryGetValue(Key(key, typeof(T)), out var resource) ? resource as T : null;

        public GameObject Instantiate(string key, Transform parent = null)
        {
            var prefab = Get<GameObject>(key);
            if (prefab == null)
                return null;

            var instance = UnityEngine.Object.Instantiate(prefab, parent);
            instance.name = prefab.name;
            return instance;
        }

        public void Destroy(GameObject instance)
        {
            if (instance != null)
                UnityEngine.Object.Destroy(instance);
        }

        public void ReleaseAll()
        {
            foreach (var resource in _resources.Values)
                Resources.UnloadAsset(resource);

            _resources.Clear();
            Resources.UnloadUnusedAssets();
        }

        private static string Key(string name, Type type) => $"{name}_{type.Name}";
    }
}
