using System.IO;
using LectureFPS;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
public sealed class FpsGridBuildWindow : EditorWindow
{
    FpsGameplayFlow flow;
    FpsCombatArea area;
    FpsBuildDefinition definition;
    GameObject prefab;
    Sprite icon;
    string itemName = "Barricade";
    int width = 2, depth = 1;
    Vector2 scroll;
    [MenuItem("Tools/FPS Lecture/11_Grid Build Editor", false, 11)]
    static void Open() => GetWindow<FpsGridBuildWindow>("Grid Build");
    void OnEnable()
    {
        flow = Object.FindAnyObjectByType<FpsGameplayFlow>();
        area = Selection.activeGameObject ? Selection.activeGameObject.GetComponentInParent<FpsCombatArea>() : null;
    }
    void OnGUI()
    {
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            EditorGUILayout.HelpBox("Assign a combat area, create its grid, then add Build Definition assets to Catalog. Each asset maps a Sprite icon to a 3D prefab and occupied width/depth in cells. Grid origin is its lower-left corner. Keep it horizontal, scale 1, and aligned to flat ground.",MessageType.Info);
            flow = (FpsGameplayFlow)EditorGUILayout.ObjectField("Flow",flow,typeof(FpsGameplayFlow),true);
            area = (FpsCombatArea)EditorGUILayout.ObjectField("Combat area",area,typeof(FpsCombatArea),true);
            if (area && !area.buildGrid && GUILayout.Button("Create build zone for area")) CreateGrid();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (area && area.buildGrid)
            {
                var serialized = new SerializedObject(area.buildGrid); serialized.Update();
                EditorGUILayout.PropertyField(serialized.FindProperty("columns")); EditorGUILayout.PropertyField(serialized.FindProperty("rows"));
                EditorGUILayout.PropertyField(serialized.FindProperty("cellSize"));
                EditorGUILayout.PropertyField(serialized.FindProperty("groundTolerance"));
                EditorGUILayout.PropertyField(serialized.FindProperty("groundMask")); EditorGUILayout.PropertyField(serialized.FindProperty("blockerMask"));
                EditorGUILayout.PropertyField(serialized.FindProperty("catalog"),true); serialized.ApplyModifiedProperties();
                if (GUILayout.Button("Select build zone / move origin")) { Selection.activeGameObject = area.buildGrid.gameObject; SceneView.lastActiveSceneView?.FrameSelected(); }
            }
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Build Definition asset",EditorStyles.boldLabel);
            definition = (FpsBuildDefinition)EditorGUILayout.ObjectField("Edit existing definition",definition,typeof(FpsBuildDefinition),false);
            if (definition)
            {
                var serialized = new SerializedObject(definition); serialized.Update();
                var iterator = serialized.GetIterator(); bool children = true;
                while (iterator.NextVisible(children)) { children = false; if (iterator.name != "m_Script") EditorGUILayout.PropertyField(iterator,true); }
                serialized.ApplyModifiedProperties();
                if (!definition.IsValid) EditorGUILayout.HelpBox("Assign Sprite and prefab; width, depth, collision height and power must be positive.",MessageType.Warning);
                if (area && area.buildGrid && GUILayout.Button("Add definition to area's Catalog"))
                {
                    var grid = area.buildGrid; Undo.RecordObject(grid,"Add build definition");
                    if (System.Array.IndexOf(grid.catalog,definition) < 0) { int n = grid.catalog.Length; System.Array.Resize(ref grid.catalog,n+1); grid.catalog[n] = definition; }
                    EditorUtility.SetDirty(grid); EditorSceneManager.MarkSceneDirty(grid.gameObject.scene);
                }
            }
            else
            {
                itemName = EditorGUILayout.TextField("Display name",itemName);
                prefab = (GameObject)EditorGUILayout.ObjectField("3D visual prefab / model",prefab,typeof(GameObject),false);
                icon = (Sprite)EditorGUILayout.ObjectField("UI Sprite",icon,typeof(Sprite),false);
                width = EditorGUILayout.IntSlider("Width (cells)",width,1,20); depth = EditorGUILayout.IntSlider("Depth (cells)",depth,1,20);
                using (new EditorGUI.DisabledScope(!prefab || !icon))
                    if (GUILayout.Button("Create ScriptableObject asset"))
                    {
                        Directory.CreateDirectory("Assets/FPS/BuildData");
                        definition = CreateInstance<FpsBuildDefinition>(); definition.displayName = itemName; definition.prefab = prefab; definition.icon = icon; definition.width = width; definition.depth = depth;
                        AssetDatabase.CreateAsset(definition,AssetDatabase.GenerateUniqueAssetPath("Assets/FPS/BuildData/BuildDefinition.asset")); AssetDatabase.SaveAssets(); Selection.activeObject = definition;
                    }
            }
            EditorGUILayout.EndScrollView();
            using (new EditorGUI.DisabledScope(!flow))
                if (GUILayout.Button("Add Sprite Palette and Grid Placement to Flow")) CreateBuilder();
        }
    }
    void CreateGrid()
    {
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        var root = new GameObject("Build Grid"); Undo.RegisterCreatedObjectUndo(root,"Create build grid"); root.transform.SetParent(area.transform,false); root.transform.localPosition = new Vector3(-6,0,-6);
        var grid = Undo.AddComponent<FpsBuildGrid>(root); grid.area = area;
        Undo.RecordObject(area,"Connect build grid"); area.buildGrid = grid; EditorUtility.SetDirty(area);
        EditorSceneManager.MarkSceneDirty(area.gameObject.scene); Undo.CollapseUndoOperations(group); Selection.activeGameObject = root;
    }
    void CreateBuilder()
    {
        var existing = flow.GetComponentInChildren<FpsGridBuilder>(true);
        if (existing) { Selection.activeGameObject = existing.gameObject; return; }
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        var root = New("Grid Build UGUI",flow.transform);
        var canvas = Undo.AddComponent<Canvas>(root); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 350;
        var scaler = Undo.AddComponent<CanvasScaler>(root); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
        Undo.AddComponent<GraphicRaycaster>(root);
        var builder = Undo.AddComponent<FpsGridBuilder>(root); builder.flow = flow;
        var palette = New("Sprite Palette",root.transform); var background = Undo.AddComponent<Image>(palette); background.color = new Color(.04f,.06f,.09f,.95f);
        var rect = background.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(1,.5f); rect.pivot = new Vector2(1,.5f); rect.anchoredPosition = new Vector2(-20,0); rect.sizeDelta = new Vector2(360,650);
        var scrollObject = New("Catalog Scroll",palette.transform); Stretch(scrollObject.GetComponent<RectTransform>());
        var scrollView = Undo.AddComponent<ScrollRect>(scrollObject); scrollView.horizontal = false;
        var viewport = New("Viewport",scrollObject.transform); Stretch(viewport.GetComponent<RectTransform>()); Undo.AddComponent<RectMask2D>(viewport);
        var content = New("Items",viewport.transform); var contentRect = content.GetComponent<RectTransform>(); contentRect.anchorMin = new Vector2(0,1); contentRect.anchorMax = Vector2.one; contentRect.pivot = new Vector2(.5f,1);
        var layout = Undo.AddComponent<VerticalLayoutGroup>(content); layout.childControlHeight = false; layout.childControlWidth = true; layout.childForceExpandHeight = false; layout.spacing = 8; layout.padding = new RectOffset(8,8,8,8);
        Undo.AddComponent<ContentSizeFitter>(content).verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollView.viewport = viewport.GetComponent<RectTransform>(); scrollView.content = contentRect;
        var buttonObject = New("Item Template",content.transform); var image = Undo.AddComponent<Image>(buttonObject); image.color = new Color(.18f,.25f,.32f); image.rectTransform.sizeDelta = new Vector2(330,100);
        var button = Undo.AddComponent<Button>(buttonObject); button.targetGraphic = image;
        var iconObject = New("Icon",buttonObject.transform); var iconImage = Undo.AddComponent<Image>(iconObject); iconImage.preserveAspect = true; iconImage.raycastTarget = false; iconImage.rectTransform.anchoredPosition = new Vector2(-110,0); iconImage.rectTransform.sizeDelta = new Vector2(80,80);
        var text = Label(buttonObject.transform,"Name Cost",new Vector2(45,0),new Vector2(210,90)); text.fontSize = 22;
        builder.itemTemplate = button; builder.buttonsRoot = content.transform; builder.palette = palette;
        buttonObject.SetActive(false); palette.SetActive(false);
        builder.hint = Label(root.transform,"Placement Hint",new Vector2(0,-400),new Vector2(1250,100));
        EditorUtility.SetDirty(builder); EditorSceneManager.MarkSceneDirty(flow.gameObject.scene); Undo.CollapseUndoOperations(group); Selection.activeGameObject = root;
    }
    static GameObject New(string name,Transform parent) { var go = new GameObject(name,typeof(RectTransform)); Undo.RegisterCreatedObjectUndo(go,"Create build UI"); go.transform.SetParent(parent,false); return go; }
    static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    static Text Label(Transform parent,string name,Vector2 position,Vector2 size)
    {
        var text = Undo.AddComponent<Text>(New(name,parent)); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 26; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
        text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size; return text;
    }
}
