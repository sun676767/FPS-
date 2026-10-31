using LectureFPS;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
public static class FpsTerminalSetup
{
    [MenuItem("Tools/FPS Lecture/5_Add Terminal UI", false, 5)]
    static void Build()
    {
        var game = Object.FindAnyObjectByType<FpsLectureGame>();
        var player = Object.FindAnyObjectByType<StarterAssets.FirstPersonController>();
        if (!game || !player || Application.isPlaying) { Debug.LogError("Open a configured FPS scene first."); return; }
        if (Object.FindAnyObjectByType<FpsTerminalUI>()) return;
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        var root = New("Terminal UI [TAB near terminal]", game.transform);
        var canvas = Undo.AddComponent<Canvas>(root); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 150;
        var scaler = Undo.AddComponent<CanvasScaler>(root); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080);
        Undo.AddComponent<GraphicRaycaster>(root);
        var ui = Undo.AddComponent<FpsTerminalUI>(root); ui.game = game; ui.movement = player;
        var panel = New("Terminal Panel", root.transform); var background = Undo.AddComponent<Image>(panel); background.color = new Color(.03f,.05f,.1f,.97f);
        background.rectTransform.sizeDelta = new Vector2(900,650);
        ui.panel = panel;
        ui.status = Label(panel.transform, "Mission and Credits", new Vector2(0,180), new Vector2(820,140));
        ui.mission = Button(panel.transform, "Accept Claim", 30, out ui.missionLabel);
        ui.upgrade = Button(panel.transform, "Upgrade", -90, out ui.upgradeLabel);
        ui.close = Button(panel.transform, "Close", -210, out var closeLabel); closeLabel.text = "Close [Esc]";
        Label(root.transform, "Help", new Vector2(0,400), new Vector2(800,60)).text = "TAB near terminal: Mission / Upgrade";
        AddUpgradeButtons(ui);
        panel.SetActive(false);
        if (!Object.FindAnyObjectByType<EventSystem>())
        {
            var events = New("EventSystem", game.transform); Undo.AddComponent<EventSystem>(events);
            Undo.AddComponent<InputSystemUIInputModule>(events).AssignDefaultActions();
        }
        EditorUtility.SetDirty(ui); EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        Undo.CollapseUndoOperations(group); Selection.activeGameObject = root;
    }
    [MenuItem("Tools/FPS Lecture/8_Upgrade Existing Terminal UI", false, 8)]
    static void UpgradeExisting()
    {
        if (Application.isPlaying) return;
        var ui = Object.FindAnyObjectByType<FpsTerminalUI>();
        if (!ui || !ui.panel) { Debug.LogError("Add Terminal UI first."); return; }
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        Undo.RecordObject(ui, "Add upgrade buttons");
        AddUpgradeButtons(ui);
        EditorUtility.SetDirty(ui); EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        Undo.CollapseUndoOperations(group);
    }
    static void AddUpgradeButtons(FpsTerminalUI ui)
    {
        Undo.RecordObject(ui.panel.GetComponent<RectTransform>(), "Resize terminal");
        ui.panel.GetComponent<RectTransform>().sizeDelta = new Vector2(1000,850);
        Undo.RecordObject(ui.status.rectTransform, "Arrange terminal");
        ui.status.rectTransform.anchoredPosition = new Vector2(0,300);
        Undo.RecordObject(ui.mission.GetComponent<RectTransform>(), "Arrange terminal");
        ui.mission.GetComponent<RectTransform>().anchoredPosition = new Vector2(0,150);
        Undo.RecordObject(ui.upgrade.GetComponent<RectTransform>(), "Arrange terminal");
        ui.upgrade.GetComponent<RectTransform>().anchoredPosition = new Vector2(0,40);
        if (!ui.reloadUpgrade) ui.reloadUpgrade = Button(ui.panel.transform, "Reload Upgrade", -70, out ui.reloadLabel);
        if (!ui.healthUpgrade) ui.healthUpgrade = Button(ui.panel.transform, "Health Upgrade", -180, out ui.healthLabel);
        Undo.RecordObject(ui.close.GetComponent<RectTransform>(), "Arrange terminal");
        ui.close.GetComponent<RectTransform>().anchoredPosition = new Vector2(0,-310);
    }
    static GameObject New(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); Undo.RegisterCreatedObjectUndo(go,"Create Terminal UI"); go.transform.SetParent(parent,false); return go;
    }
    static Text Label(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var text = Undo.AddComponent<Text>(New(name,parent)); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 28; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
        text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size; return text;
    }
    static Button Button(Transform parent, string name, float y, out Text label)
    {
        var go = New(name,parent); var image = Undo.AddComponent<Image>(go); image.color = new Color(.15f,.3f,.45f);
        image.rectTransform.anchoredPosition = new Vector2(0,y); image.rectTransform.sizeDelta = new Vector2(600,90);
        var button = Undo.AddComponent<Button>(go); button.targetGraphic = image;
        label = Label(go.transform,"Label",Vector2.zero,new Vector2(600,90)); return button;
    }
}
