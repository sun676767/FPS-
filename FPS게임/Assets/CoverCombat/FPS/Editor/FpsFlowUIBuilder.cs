using LectureFPS;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
public static class FpsFlowUIBuilder
{
    public static FpsFlowUI Create(FpsGameplayFlow flow)
    {
        var root = New("Gameplay Flow UGUI",flow.transform);
        var canvas = Undo.AddComponent<Canvas>(root); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 300;
        var scaler = Undo.AddComponent<CanvasScaler>(root); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
        Undo.AddComponent<GraphicRaycaster>(root);
        var ui = Undo.AddComponent<FpsFlowUI>(root); ui.flow = flow;
        var panel = New("Preparation Result Panel",root.transform); var image = Undo.AddComponent<Image>(panel); image.color = new Color(.025f,.04f,.07f,.96f); image.rectTransform.sizeDelta = new Vector2(1100,940); ui.panel = panel;
        ui.title = Label(panel.transform,"Phase",new Vector2(0,390),new Vector2(1020,70),30);
        ui.description = Label(panel.transform,"Description",new Vector2(0,290),new Vector2(1020,130),24);
        ui.damage = Button(panel.transform,"Damage",170,out ui.damageLabel);
        ui.reload = Button(panel.transform,"Reload",80,out ui.reloadLabel);
        ui.health = Button(panel.transform,"Health",-10,out ui.healthLabel);
        var dropdownObject = DefaultControls.CreateDropdown(new DefaultControls.Resources());
        dropdownObject.name = "Build Site Selection"; Undo.RegisterCreatedObjectUndo(dropdownObject,"Build site selector"); dropdownObject.transform.SetParent(panel.transform,false);
        foreach (var t in dropdownObject.GetComponentsInChildren<Text>(true)) { t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.fontSize = 22; }
        var rect = dropdownObject.GetComponent<RectTransform>(); rect.anchoredPosition = new Vector2(0,-110); rect.sizeDelta = new Vector2(950,60);
        ui.sites = dropdownObject.GetComponent<Dropdown>();
        ui.build = Button(panel.transform,"Install Upgrade Defense",-210,out ui.buildLabel);
        ui.primary = Button(panel.transform,"Continue Start Retry",-370,out ui.primaryLabel);
        ui.waypoint = Label(root.transform,"Next Objective",new Vector2(0,470),new Vector2(1500,60),26);
        panel.SetActive(false);
        if (!Object.FindAnyObjectByType<EventSystem>())
        {
            var system = New("EventSystem",flow.transform); Undo.AddComponent<EventSystem>(system); Undo.AddComponent<InputSystemUIInputModule>(system).AssignDefaultActions();
        }
        if (!flow.player.healthLabel)
        {
            Undo.RecordObject(flow.player,"Connect health HUD");
            flow.player.healthLabel = Label(root.transform,"Player Health",new Vector2(-700,-470),new Vector2(400,60),26);
            flow.player.healthLabel.text = "HP " + flow.player.maxHealth;
            EditorUtility.SetDirty(flow.player);
        }
        EditorUtility.SetDirty(ui); return ui;
    }
    static GameObject New(string name,Transform parent)
    {
        var go = new GameObject(name,typeof(RectTransform)); Undo.RegisterCreatedObjectUndo(go,"Create Flow UI"); go.transform.SetParent(parent,false); return go;
    }
    static Text Label(Transform parent,string name,Vector2 position,Vector2 size,int fontSize)
    {
        var text = Undo.AddComponent<Text>(New(name,parent)); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
        text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size; return text;
    }
    static Button Button(Transform parent,string name,float y,out Text label)
    {
        var go = New(name,parent); var image = Undo.AddComponent<Image>(go); image.color = new Color(.13f,.28f,.4f);
        image.rectTransform.anchoredPosition = new Vector2(0,y); image.rectTransform.sizeDelta = new Vector2(950,70);
        var button = Undo.AddComponent<Button>(go); button.targetGraphic = image;
        label = Label(go.transform,"Label",Vector2.zero,new Vector2(930,65),25); return button;
    }
}
