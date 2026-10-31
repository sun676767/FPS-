using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace GameLecture.Framework
{
    public interface IValidate
    {
        bool Validate();
    }

    public interface IDataLoader<TKey, TValue> : IValidate
    {
        Dictionary<TKey, TValue> MakeDictionary();
    }

    public sealed class DataManager : Singleton<DataManager>
    {
        public T LoadConfig<T>(string resourceKey) where T : ScriptableObject
        {
            var config = ResourceManager.Instance.Get<T>(resourceKey);
            if (config == null)
                Debug.LogError($"Config not found: {resourceKey}");
            return config;
        }

        public TLoader LoadJson<TLoader>(string resourceKey) where TLoader : IValidate
        {
            var asset = ResourceManager.Instance.Get<TextAsset>(resourceKey);
            if (asset == null)
                throw new InvalidOperationException($"JSON resource not found: {resourceKey}");

            var loader = JsonConvert.DeserializeObject<TLoader>(asset.text);
            if (loader == null || !loader.Validate())
                throw new InvalidDataException($"JSON validation failed: {resourceKey}");
            return loader;
        }
    }

    public sealed class JsonSaveStore<T> where T : class
    {
        public string SavePath { get; }

        public JsonSaveStore(string fileName)
        {
            SavePath = Path.Combine(Application.persistentDataPath, fileName);
        }

        public void Save(T value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            File.WriteAllText(SavePath, JsonConvert.SerializeObject(value, Formatting.Indented));
        }

        public T Load() => File.Exists(SavePath)
            ? JsonConvert.DeserializeObject<T>(File.ReadAllText(SavePath))
            : null;

        public bool Delete()
        {
            if (!File.Exists(SavePath))
                return false;
            File.Delete(SavePath);
            return true;
        }
    }
}
