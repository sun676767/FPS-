using LectureFPS;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class FpsRetrySetup
{
    [MenuItem("Tools/FPS Lecture/3_Add Health and Retry UI", false, 3)]
    static void Build()
    {
        var game = Object.FindFirstObjectByType<FpsLectureGame>();
        var player = Object.FindFirstObjectByType<StarterAssets.FirstPersonController>();
        if (!game || !player || Application.isPlaying) { Debug.LogError("Open the FPS scene with a generated sample and Starter Assets player first."); return; }
        var health = player.GetComponent<FpsPlayerHealth>();
        if (health && health.resultPanel) { Selection.activeGameObject = health.resultPanel; return; }
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        if (!health) health = Undo.AddComponent<FpsPlayerHealth>(player.gameObject);
        Undo.RecordObject(health, "Configure retry");
        var root = New("Health and Retry UI", game.transform);
        var canvas = Undo.AddComponent<Canvas>(root); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
        var scaler = Undo.AddComponent<CanvasScaler>(root); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080);
        Undo.AddComponent<GraphicRaycaster>(root);
        health.healthLabel = Text(root.transform, "Health", "HP 100 / 100", new Vector2(0,1), new Vector2(30,-30), new Vector2(400,60));
        var panel = New("Defeated", root.transform);
        var background = Undo.AddComponent<Image>(panel); background.color = new Color(0,0,0,.85f);
        var rect = background.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        Text(panel.transform, "Title", "DEFEATED", new Vector2(.5f,.5f), new Vector2(0,80), new Vector2(500,80));
        var buttonObject = New("Retry", panel.transform);
        var image = Undo.AddComponent<Image>(buttonObject); image.color = new Color(.2f,.35f,.5f);
        var button = Undo.AddComponent<Button>(buttonObject); button.targetGraphic = image;
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f,.5f); image.rectTransform.sizeDelta = new Vector2(300,70);
        Text(buttonObject.transform, "Label", "RETRY", new Vector2(.5f,.5f), Vector2.zero, new Vector2(300,70));
        health.resultPanel = panel; health.retryButton = button;
        health.hurtSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/GameAssets/Audio/SFX/impact (player).wav");
        health.deathSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/GameAssets/Audio/SFX/player death.wav");
        panel.SetActive(false);
        if (!Object.FindFirstObjectByType<EventSystem>())
        {
            var events = New("EventSystem", game.transform);
            Undo.AddComponent<EventSystem>(events);
            var module = Undo.AddComponent<InputSystemUIInputModule>(events); module.AssignDefaultActions();
        }
        EditorUtility.SetDirty(health); EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        Undo.CollapseUndoOperations(group); Selection.activeGameObject = root;
    }
    static GameObject New(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); Undo.RegisterCreatedObjectUndo(go, "Create retry UI"); go.transform.SetParent(parent, false); return go;
    }
    static Text Text(Transform parent, string name, string value, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var go = New(name, parent); var text = Undo.AddComponent<Text>(go);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 30; text.alignment = TextAnchor.MiddleCenter; text.text = value; text.raycastTarget = false;
        var r = text.rectTransform; r.anchorMin = r.anchorMax = anchor; r.anchoredPosition = position; r.sizeDelta = size; return text;
    }
}
