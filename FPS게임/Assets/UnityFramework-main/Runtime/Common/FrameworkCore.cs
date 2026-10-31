using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameLecture.Framework
{
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static bool _quitting;

        public static T Instance
        {
            get
            {
                if (_quitting)
                    return null;

                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<T>();
                    if (_instance == null)
                    {
                        var go = new GameObject($"@{typeof(T).Name}");
                        _instance = go.AddComponent<T>();
                    }

                    DontDestroyOnLoad(_instance.gameObject);
                }

                return _instance;
            }
        }

        protected virtual void OnApplicationQuit() => _quitting = true;
    }

    public static class FrameworkUtils
    {
        public static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            return go.TryGetComponent(out T component) ? component : go.AddComponent<T>();
        }

        public static GameObject FindChild(GameObject root, string childName, bool recursive = false)
        {
            if (root == null)
                return null;

            if (!recursive)
            {
                for (var i = 0; i < root.transform.childCount; i++)
                {
                    var child = root.transform.GetChild(i);
                    if (child.name == childName)
                        return child.gameObject;
                }

                return null;
            }

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName)
                    return child.gameObject;
            }

            return null;
        }

        public static T FindChildComponent<T>(GameObject root, string childName, bool recursive = false)
            where T : Component
        {
            var child = FindChild(root, childName, recursive);
            return child == null ? null : child.GetComponent<T>();
        }

        public static Transform GetOrCreateRoot(ref Transform root, string rootName, Transform parent = null)
        {
            if (root != null)
                return root;

            var go = GameObject.Find(rootName) ?? new GameObject(rootName);
            root = go.transform;
            root.SetParent(parent);
            return root;
        }
    }

    public static class FrameworkExtensions
    {
        public static T GetOrAddComponent<T>(this GameObject go) where T : Component =>
            FrameworkUtils.GetOrAddComponent<T>(go);

        public static void DestroyChildren(this Transform transform)
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(transform.GetChild(i).gameObject);
        }
    }

    public sealed class EventManager : Singleton<EventManager>
    {
        private readonly Dictionary<Enum, Action> _events = new();

        public void Add<TEvent>(TEvent eventType, Action listener) where TEvent : Enum
        {
            _events.TryGetValue(eventType, out var current);
            _events[eventType] = current + listener;
        }

        public void Remove<TEvent>(TEvent eventType, Action listener) where TEvent : Enum
        {
            if (!_events.TryGetValue(eventType, out var current))
                return;

            current -= listener;
            if (current == null)
                _events.Remove(eventType);
            else
                _events[eventType] = current;
        }

        public void Trigger<TEvent>(TEvent eventType) where TEvent : Enum
        {
            if (_events.TryGetValue(eventType, out var action))
                action?.Invoke();
        }

        public void Clear() => _events.Clear();
    }
}
