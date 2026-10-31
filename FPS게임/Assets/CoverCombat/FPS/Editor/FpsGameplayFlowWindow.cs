using System;
using LectureFPS;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public sealed class FpsGameplayFlowWindow : EditorWindow
{
    FpsGameplayFlow flow;
    Transform anchor;
    FpsEncounterDefinition encounterTemplate;
    GameObject enemyPrefab;
    int count = 5;
    string areaTitle = "Defense Area";
    Vector2 scroll;
    [MenuItem("Tools/FPS Lecture/10_Gameplay Flow Editor", false, 10)]
    static void Open() => GetWindow<FpsGameplayFlowWindow>("Gameplay Flow");
    void OnEnable() => flow = UnityEngine.Object.FindAnyObjectByType<FpsGameplayFlow>();
    void OnGUI()
    {
        EditorGUILayout.HelpBox("Create a flow, then add ordered combat areas at selected scene locations. Orange = entry trigger, cyan = combat planning bounds. Bind a wave encounter or generate one from an enemy prefab. Preparation starts only when the expected area's trigger is reached. Scene changes support Undo; save the scene after review.",MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            flow = (FpsGameplayFlow)EditorGUILayout.ObjectField("Flow",flow,typeof(FpsGameplayFlow),true);
            if (!flow)
            {
                if (GUILayout.Button("Create Flow + Preparation / Result UI")) CreateFlow();
                return;
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var serialized = new SerializedObject(flow); serialized.Update();
            EditorGUILayout.PropertyField(serialized.FindProperty("game"));
            EditorGUILayout.PropertyField(serialized.FindProperty("player"));
            EditorGUILayout.PropertyField(serialized.FindProperty("startingCredits"));
            EditorGUILayout.PropertyField(serialized.FindProperty("startInPreparation"));
            EditorGUILayout.HelpBox("Starting credits and area rewards are granted once per campaign ID in the save. Flow order and installations last for the current play session. Retry keeps purchased installations and restarts the current area's preparation.",MessageType.Info);
            EditorGUILayout.PropertyField(serialized.FindProperty("onTravel"));
            EditorGUILayout.PropertyField(serialized.FindProperty("onEnding"));
            serialized.ApplyModifiedProperties();
            for (int i = 0; i < flow.areas.Length; i++)
            {
                var area = flow.areas[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                if (!area) { EditorGUILayout.LabelField($"Area {i+1}: Missing reference"); }
                else
                {
                    EditorGUILayout.LabelField($"{i+1}. {area.title}",EditorStyles.boldLabel);
                    var data = new SerializedObject(area); data.Update();
                    EditorGUILayout.PropertyField(data.FindProperty("title"));

                    EditorGUILayout.PropertyField(data.FindProperty("clearReward"));
                    EditorGUILayout.PropertyField(data.FindProperty("autoStartCombat"));
                    EditorGUILayout.PropertyField(data.FindProperty("preparationSeconds"));
                    EditorGUILayout.PropertyField(data.FindProperty("countdownSeconds"));
                    EditorGUILayout.PropertyField(data.FindProperty("waveStartPoint"));
                    EditorGUILayout.PropertyField(data.FindProperty("spawnBindings"),true);
                    EditorGUILayout.PropertyField(data.FindProperty("combatSize"));
                    EditorGUILayout.PropertyField(data.FindProperty("buildGrid"));
                    EditorGUILayout.PropertyField(data.FindProperty("buildSites"),true);
                    EditorGUILayout.PropertyField(data.FindProperty("onPreparation"));
                    EditorGUILayout.PropertyField(data.FindProperty("onCombat"));
                    EditorGUILayout.PropertyField(data.FindProperty("onCleared"));
                    data.ApplyModifiedProperties();
                    if (GUILayout.Button("Create regional wave start at selected location"))
                    {
                        var point = new GameObject("Wave Start"); Undo.RegisterCreatedObjectUndo(point,"Create wave start"); point.transform.SetParent(area.transform,false); point.transform.position = Location();
                        Undo.RecordObject(area,"Assign wave start"); area.waveStartPoint = point.transform; EditorUtility.SetDirty(area); Dirty(); Selection.activeGameObject = point;
                    }
                    FpsEncounterAuthoring.DrawData(area);
                    if (GUILayout.Button("Select / Frame area and edit trigger")) { Selection.activeGameObject = area.gameObject; SceneView.lastActiveSceneView?.FrameSelected(); }
                    if (GUILayout.Button("Add Barricade Site at selected location")) AddSite(area,FpsDefenseKind.Barricade);
                    if (GUILayout.Button("Add Trap Site at selected location")) AddSite(area,FpsDefenseKind.Trap);
                }
                if (i > 0 && GUILayout.Button("Move area earlier"))
                { Undo.RecordObject(flow,"Reorder areas"); (flow.areas[i-1],flow.areas[i]) = (flow.areas[i],flow.areas[i-1]); EditorUtility.SetDirty(flow); }
                if (GUILayout.Button("Remove from flow order (keep scene objects)"))
                {
                    Undo.RecordObject(flow,"Remove area reference"); var list = new System.Collections.Generic.List<FpsCombatArea>(flow.areas); list.RemoveAt(i); flow.areas = list.ToArray(); EditorUtility.SetDirty(flow); EditorGUILayout.EndVertical(); break;
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Add next combat area",EditorStyles.boldLabel);
            anchor = (Transform)EditorGUILayout.ObjectField("Location anchor (scene)",anchor,typeof(Transform),true);
            areaTitle = EditorGUILayout.TextField("Area title",areaTitle);
            encounterTemplate = (FpsEncounterDefinition)EditorGUILayout.ObjectField("Encounter Definition",encounterTemplate,typeof(FpsEncounterDefinition),false);
            enemyPrefab = (GameObject)EditorGUILayout.ObjectField("Enemy prefab (new wave)",enemyPrefab,typeof(GameObject),false);
            count = EditorGUILayout.IntSlider("Enemy count (new wave)",count,1,100);
            if (GUILayout.Button("Add area at anchor / Scene View pivot")) AddArea();
            if (GUILayout.Button("Validate all areas"))
            {
                string error = flow.ValidateSetup();
                if (error == null) Debug.Log("Gameplay Flow configuration is valid. Bake NavMesh and check spawn positions in Play Mode.",flow);
                else Debug.LogError(error,flow);
            }
            EditorGUILayout.EndScrollView();
        }
    }
    void CreateFlow()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<FpsLectureGame>();
        var controller = UnityEngine.Object.FindAnyObjectByType<StarterAssets.FirstPersonController>();
        if (!game || !controller) { Debug.LogError("Create the FPS sample in the scene first."); return; }
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        var health = controller.GetComponent<FpsPlayerHealth>();
        if (!health) health = Undo.AddComponent<FpsPlayerHealth>(controller.gameObject);
        Undo.RecordObject(health,"Connect player sounds");
        if (!health.hurtSound) health.hurtSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/GameAssets/Audio/SFX/impact (player).wav");
        if (!health.deathSound) health.deathSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/GameAssets/Audio/SFX/player death.wav");
        EditorUtility.SetDirty(health);
        var root = new GameObject("Gameplay Flow"); Undo.RegisterCreatedObjectUndo(root,"Create flow");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,game.gameObject.scene);
        flow = Undo.AddComponent<FpsGameplayFlow>(root); flow.game = game; flow.player = health; flow.campaignId = Guid.NewGuid().ToString("N");
        flow.ui = FpsFlowUIBuilder.Create(flow);
        Undo.CollapseUndoOperations(group); Dirty(); Selection.activeGameObject = root;
    }
    Vector3 Location()
    {
        var source = anchor ? anchor : Selection.activeTransform;
        Vector3 position = source && !EditorUtility.IsPersistent(source) && source.gameObject.scene == flow.gameObject.scene ? source.position : SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView.pivot : flow.transform.position;
        if (Physics.Raycast(position+Vector3.up*2,Vector3.down,out var hit,100,~0,QueryTriggerInteraction.Ignore)) position = hit.point;
        return position;
    }
    void AddArea()
    {
        if (!encounterTemplate && (!enemyPrefab || !PrefabUtility.IsPartOfPrefabAsset(enemyPrefab) || !enemyPrefab.GetComponent<FpsEnemyAI>() || !enemyPrefab.GetComponent<FpsTarget>() || !enemyPrefab.GetComponent<NavMeshAgent>()))
        { Debug.LogError("Assign an Encounter Definition or an enemy prefab to generate data."); return; }
        if (encounterTemplate && encounterTemplate.data.Validate() != null) { Debug.LogError(encounterTemplate.data.Validate(),encounterTemplate); return; }
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
        var root = new GameObject($"Area {flow.areas.Length+1} - {areaTitle}"); Undo.RegisterCreatedObjectUndo(root,"Add combat area"); root.transform.SetParent(flow.transform,false); root.transform.position = Location();
        var area = Undo.AddComponent<FpsCombatArea>(root); area.areaId = Guid.NewGuid().ToString("N"); area.title = areaTitle;
        var trigger = root.GetComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = new Vector3(6,3,6); trigger.center = Vector3.up*1.5f;
        var body = Undo.AddComponent<Rigidbody>(root); body.isKinematic = true; body.useGravity = false;
        var marker = new GameObject("Wave Start (move onto NavMesh)"); Undo.RegisterCreatedObjectUndo(marker,"Create wave start"); marker.transform.SetParent(root.transform,false); marker.transform.localPosition = new Vector3(0,0,10); area.waveStartPoint = marker.transform;
        area.encounterSource = FpsCombatArea.EncounterSource.ScriptableObject;
        area.encounterDefinition = encounterTemplate;
        if (!area.encounterDefinition)
        {
            System.IO.Directory.CreateDirectory("Assets/FPS/Data");
            var asset = CreateInstance<FpsEncounterDefinition>();
            asset.data.waves = new[] { new FpsWaveData { title = areaTitle + " Wave 1", groups = new[] { new FpsSpawnGroupData { prefab = enemyPrefab, count = count } } } };
            AssetDatabase.CreateAsset(asset,AssetDatabase.GenerateUniqueAssetPath("Assets/FPS/Data/EncounterDefinition.asset"));
            AssetDatabase.SaveAssets(); area.encounterDefinition = asset;
        }
        Undo.RecordObject(flow,"Append combat area"); int n = flow.areas.Length; Array.Resize(ref flow.areas,n+1); flow.areas[n] = area;
        CreateSite(area,FpsDefenseKind.Barricade,root.transform.TransformPoint(new Vector3(-2,0,4)));
        CreateSite(area,FpsDefenseKind.Trap,root.transform.TransformPoint(new Vector3(2,0,5)));
        EditorUtility.SetDirty(area); Dirty(); Undo.CollapseUndoOperations(undo); Selection.activeGameObject = root;
    }
    void AddSite(FpsCombatArea area,FpsDefenseKind kind)
    {
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        CreateSite(area,kind,Location()); Dirty(); Undo.CollapseUndoOperations(group);
    }
    static void CreateSite(FpsCombatArea area,FpsDefenseKind kind,Vector3 position)
    {
        var root = new GameObject(kind + " Site " + (area.buildSites.Length+1)); Undo.RegisterCreatedObjectUndo(root,"Add defense site"); root.transform.SetParent(area.transform,false); root.transform.position = position;
        var site = Undo.AddComponent<FpsBuildSite>(root); site.kind = kind; site.basePower = kind == FpsDefenseKind.Barricade ? 100 : 20;
        Undo.RecordObject(area,"Register defense site"); int n = area.buildSites.Length; Array.Resize(ref area.buildSites,n+1); area.buildSites[n] = site;
        EditorUtility.SetDirty(area); Selection.activeGameObject = root;
    }
    void Dirty() { EditorUtility.SetDirty(flow); EditorSceneManager.MarkSceneDirty(flow.gameObject.scene); }
}
