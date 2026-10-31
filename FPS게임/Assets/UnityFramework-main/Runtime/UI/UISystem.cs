using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GameLecture.Framework
{
    public class UIBase : MonoBehaviour
    {
        private readonly Dictionary<Type, Object[]> _objects = new();

        protected virtual void Awake()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
                ResourceManager.Instance.Instantiate("EventSystem");
        }

        protected virtual void Start() => RefreshUI();
        public virtual void RefreshUI() { }

        protected void BindObjects(Type type)
        {
            var names = Enum.GetNames(type);
            var objects = new Object[names.Length];
            for (var i = 0; i < names.Length; i++)
                objects[i] = FrameworkUtils.FindChild(gameObject, names[i], true);
            _objects[typeof(GameObject)] = objects;
        }
        protected void BindImages(Type type) => Bind<Image>(type);
        protected void BindTexts(Type type) => Bind<TMP_Text>(type);
        protected void BindButtons(Type type) => Bind<Button>(type);
        protected GameObject GetObject(int index) => Get<GameObject>(index);
        protected Image GetImage(int index) => Get<Image>(index);
        protected TMP_Text GetText(int index) => Get<TMP_Text>(index);
        protected Button GetButton(int index) => Get<Button>(index);

        private void Bind<T>(Type namesType) where T : Component
        {
            var names = Enum.GetNames(namesType);
            var objects = new Object[names.Length];
            for (var i = 0; i < names.Length; i++)
            {
                objects[i] = FrameworkUtils.FindChildComponent<T>(gameObject, names[i], true);
            }
            _objects[typeof(T)] = objects;
        }

        private T Get<T>(int index) where T : Object =>
            _objects.TryGetValue(typeof(T), out var objects) && index >= 0 && index < objects.Length
                ? objects[index] as T
                : null;
    }

    public class SceneUI : UIBase { }

    [RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
    public class PopupUI : UIBase { }

    public sealed class UIManager : Singleton<UIManager>
    {
        private Transform _root;
        private Transform _popupRoot;
        private SceneUI _sceneUI;
        private int _popupOrder = 100;
        private readonly Stack<PopupUI> _popupStack = new();
        private readonly Dictionary<string, PopupUI> _popups = new();

        private Transform Root => FrameworkUtils.GetOrCreateRoot(ref _root, "@UI_Root");
        private Transform PopupRoot => FrameworkUtils.GetOrCreateRoot(ref _popupRoot, "@PopupRoot", Root);

        public T ShowScene<T>(string resourceKey = null) where T : SceneUI
        {
            if (_sceneUI != null)
                return _sceneUI as T;
            resourceKey ??= typeof(T).Name;
            var scene = FindAnyObjectByType<T>();
            if (scene == null)
                scene = ResourceManager.Instance.Instantiate(resourceKey).GetOrAddComponent<T>();
            scene.transform.SetParent(Root);
            _sceneUI = scene;
            return scene;
        }

        public T ShowPopup<T>(string resourceKey = null) where T : PopupUI
        {
            resourceKey ??= typeof(T).Name;
            if (!_popups.TryGetValue(resourceKey, out var popup))
            {
                popup = ResourceManager.Instance.Instantiate(resourceKey).GetOrAddComponent<T>();
                _popups.Add(resourceKey, popup);
            }
            popup.transform.SetParent(PopupRoot);
            popup.gameObject.SetActive(true);
            popup.GetComponent<Canvas>().sortingOrder = ++_popupOrder;
            _popupStack.Push(popup);
            return popup as T;
        }

        public void ClosePopup()
        {
            if (_popupStack.Count == 0)
                return;
            _popupStack.Pop().gameObject.SetActive(false);
            _popupOrder--;
        }

        public void CloseAllPopups()
        {
            while (_popupStack.Count > 0)
                ClosePopup();
        }

        public void Clear()
        {
            CloseAllPopups();
            _popups.Clear();
            Root.DestroyChildren();
            _sceneUI = null;
        }
    }
}
