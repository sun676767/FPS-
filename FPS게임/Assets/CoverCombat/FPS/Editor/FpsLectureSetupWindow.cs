using System.IO;
using LectureFPS;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public sealed class FpsLectureSetupWindow : EditorWindow
{
    Camera playerCamera;
    int targetCount = 5;
    float distance = 10;
    Vector2 scroll;
    readonly GameObject[] models = new GameObject[3];
    [MenuItem("Tools/FPS Lecture/1_Scene Setup", false, 1)]
    static void Open() => GetWindow<FpsLectureSetupWindow>("FPS Lecture Setup");
    void OnEnable()
    {
        playerCamera = Camera.main;
        for (int i = 0; i < 3; i++)
            models[i] = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/GameAssets/Models/Kenney Blaster Kit/blaster{(char)('A' + i)}.fbx");
    }
    static void ApplyFeedback(FpsLectureGame game)
    {
        Undo.RecordObject(game, "Connect FPS feedback");
        if (!game.shotSource) game.shotSource = Undo.AddComponent<AudioSource>(game.gameObject);
        game.shotSource.playOnAwake = false;
        game.shotSource.spatialBlend = 0;
        game.shotClips = new[] {
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/GameAssets/Audio/SFX/shot (pistol).wav"),
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/GameAssets/Audio/SFX/shot (machinegun).wav"),
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/GameAssets/Audio/SFX/shot (rifle).wav") };
        game.impactClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/GameAssets/Audio/SFX/impact (hard).wav");
        game.impactTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GameAssets/Art/Bullet Impact.png");
        if (!game.hitIndicator && game.hud)
        {
            var go = Create("Asset Hit Indicator", game.hud.transform.parent);
            var image = Undo.AddComponent<RawImage>(go);
            image.texture = game.impactTexture;
            image.color = new Color(1, .4f, .3f, .9f);
            image.raycastTarget = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f,.5f);
            image.rectTransform.sizeDelta = new Vector2(48,48);
            image.enabled = false;
            game.hitIndicator = image;
        }
        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
    }
    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("현재 씬에 FPS 강의 샘플 추가", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("카메라 앞에 표적과 임무 단말을 배치합니다. 생성 후 위치와 무기 모델 크기를 조정하세요. 씬은 자동 저장하지 않습니다. Undo로 생성 작업을 취소할 수 있습니다.", MessageType.Info);
        playerCamera = (Camera)EditorGUILayout.ObjectField("플레이어 카메라", playerCamera, typeof(Camera), true);
        targetCount = EditorGUILayout.IntSlider("표적 수", targetCount, 1, 12);
        distance = EditorGUILayout.Slider("배치 거리", distance, 4, 30);
        for (int i = 0; i < 3; i++) models[i] = (GameObject)EditorGUILayout.ObjectField($"무기 {i + 1} 모델", models[i], typeof(GameObject), false);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("포함 기능", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("JSON 무기 데이터 / 사격·재장전·교체 / UGUI HUD");
        EditorGUILayout.LabelField("표적 임무 / 보상 / 피해 업그레이드 / JSON 저장");
        using (new EditorGUI.DisabledScope(!playerCamera || EditorApplication.isPlayingOrWillChangePlaymode || FindFirstObjectByType<FpsLectureGame>() != null))
            if (GUILayout.Button("현재 씬에 기본 콘텐츠 생성", GUILayout.Height(36))) Build();
        if (FindFirstObjectByType<FpsLectureGame>() != null)
            EditorGUILayout.HelpBox("현재 씬에 샘플이 이미 있습니다. Hierarchy의 FPS Lecture Sample에서 설정하세요. 재생성하려면 먼저 해당 루트를 삭제하세요.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            if (GUILayout.Button("?? ??? GameAssets ??? / ?? ?? ??"))
            {
                var game = FindFirstObjectByType<FpsLectureGame>();
                if (game) ApplyFeedback(game);
            }
        EditorGUILayout.EndScrollView();
    }
    static GameObject Create(string name, Transform parent = null)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create FPS Lecture Sample");
        if (parent) go.transform.SetParent(parent, false);
        return go;
    }
    // Shared authoring path for FPS and cover-combat scenes.
    public static void ConfigureWeapons(FpsLectureGame game, Camera camera, GameObject[] weaponModels = null)
    {
        Undo.RecordObject(game, "Configure FPS weapons");
        game.view = camera;
        if (weaponModels == null)
        {
            weaponModels = new GameObject[3];
            for (int i = 0; i < 3; i++) weaponModels[i] = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/GameAssets/Models/Kenney Blaster Kit/blaster{(char)('A' + i)}.fbx");
        }
        if (!game.socket)
        {
            // Socket follows the camera without changing the existing player hierarchy.
            var socket = Create("Weapon Socket", game.transform);
            var follow = Undo.AddComponent<UnityEngine.Animations.ParentConstraint>(socket);
            follow.AddSource(new UnityEngine.Animations.ConstraintSource { sourceTransform = camera.transform, weight = 1 });
            follow.SetTranslationOffset(0, new Vector3(.3f, -.25f, .55f));
            follow.constraintActive = true;
            follow.locked = true;
            game.socket = socket.transform;
            game.weaponModels = weaponModels;
        }
        if (game.weaponModels == null || game.weaponModels.Length == 0) game.weaponModels = weaponModels;
        if (!game.hud)
        {
            var canvasObject = Create("PlayerHUD", game.transform);
            var canvas = Undo.AddComponent<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = Undo.AddComponent<CanvasScaler>(canvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var textObject = Create("Crosshair and Status", canvasObject.transform);
            var text = Undo.AddComponent<Text>(textObject);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.alignment = TextAnchor.LowerLeft;
            text.color = Color.white;
            text.raycastTarget = false;
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(40, 40); rect.offsetMax = new Vector2(-40, -40);
            game.hud = text;
            var crosshairObject = Create("Crosshair", canvasObject.transform);
            var crosshair = Undo.AddComponent<Text>(crosshairObject);
            crosshair.font = text.font;
            crosshair.fontSize = 30;
            crosshair.alignment = TextAnchor.MiddleCenter;
            crosshair.text = "+";
            crosshair.raycastTarget = false;
            crosshair.rectTransform.anchorMin = crosshair.rectTransform.anchorMax = new Vector2(.5f, .5f);
            crosshair.rectTransform.sizeDelta = new Vector2(40, 40);
            game.crosshair = crosshair;
        }
        ApplyFeedback(game);
    }
    void Build()
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create FPS Lecture Sample");
        var root = Create("FPS Lecture Sample");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, playerCamera.gameObject.scene);
        var game = Undo.AddComponent<FpsLectureGame>(root);
        game.view = playerCamera;
        ConfigureWeapons(game, playerCamera, (GameObject[])models.Clone());
        Vector3 forward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 origin = playerCamera.transform.position;
        game.targets = new FpsTarget[targetCount];
        for (int i = 0; i < targetCount; i++)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(target, "Create target");
            target.name = $"Target {i + 1}";
            target.transform.SetParent(root.transform);
            target.transform.position = origin + forward * distance + right * ((i - (targetCount - 1) * .5f) * 2);
            target.transform.localScale = new Vector3(1, 1.5f, .5f);
            game.targets[i] = Undo.AddComponent<FpsTarget>(target);
        }
        var terminal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(terminal, "Create terminal");
        terminal.name = "Mission Terminal [E: Mission U: Upgrade]";
        terminal.transform.SetParent(root.transform);
        terminal.transform.position = origin + forward * 2 + right * 1.5f - Vector3.up * .5f;
        game.terminal = terminal.transform;
        Directory.CreateDirectory("Assets/FPS/Resources/Data/JsonData");
        const string path = "Assets/FPS/Resources/Data/JsonData/FPSWeapons.json";
        if (!File.Exists(path))
        {
            var table = new WeaponTable();
            table.weapons.Add(new WeaponData { name = "Pistol", damage = 25, interval = .2f, reload = 1.2f, magazine = 12, pellets = 1, spread = .2f });
            table.weapons.Add(new WeaponData { name = "Rifle", damage = 12, interval = .1f, reload = 1.8f, magazine = 30, pellets = 1, spread = 1 });
            table.weapons.Add(new WeaponData { name = "Shotgun", damage = 8, interval = .8f, reload = 2.4f, magazine = 6, pellets = 8, spread = 5 });
            File.WriteAllText(path, JsonUtility.ToJson(table, true));
            AssetDatabase.Refresh();
        }
        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = root;
    }
}
