using System;
using CoverCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CoverCombatLayoutWindow : EditorWindow
{
    CoverCombatFlow flow;
    Transform anchor;
    Vector2 scroll;
    [MenuItem("Tools/Cover Combat/Gameplay Flow Layout")]
    static void Open() => GetWindow<CoverCombatLayoutWindow>("Cover Combat Layout");
    void OnGUI()
    {
        EditorGUILayout.HelpBox("Create an additive sample scene, or bind a flow in your own world. Areas run in array order. Select an area to edit its trigger and anchors in Scene View. Save your scene after editing.",MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Create Sample Scene (additive)")) Sample();
            flow = (CoverCombatFlow)EditorGUILayout.ObjectField("Flow",flow,typeof(CoverCombatFlow),true);
            anchor = (Transform)EditorGUILayout.ObjectField("New area location",anchor,typeof(Transform),true);
            if (GUILayout.Button("Create Flow in Active Scene"))
            {
                var root = new GameObject("Cover Combat Flow"); Undo.RegisterCreatedObjectUndo(root,"Create cover flow"); flow = Undo.AddComponent<CoverCombatFlow>(root);
                flow.player = UnityEngine.Object.FindAnyObjectByType<CoverCombatPlayer>(); Selection.activeGameObject = root;
            }
            if (!flow) return;
            if (GUILayout.Button("Connect FPS Weapons to Flow Player"))
            {
                if (flow.player && flow.player.view) { ConnectWeapons(flow.player); EditorSceneManager.MarkSceneDirty(flow.gameObject.scene); }
                else Debug.LogError("Assign Flow Player and its Camera first.",flow);
            }
            if (GUILayout.Button("Add Area + Cover + 3 Fixed Enemies"))
            {
                Undo.RecordObject(flow,"Append cover area");
                var area = BuildArea(flow,anchor ? anchor.position : SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView.pivot : Vector3.zero,flow.areas.Length+1);
                Array.Resize(ref flow.areas,flow.areas.Length+1); flow.areas[flow.areas.Length-1] = area;
                EditorUtility.SetDirty(flow); EditorSceneManager.MarkSceneDirty(flow.gameObject.scene); Selection.activeGameObject = area.gameObject;
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var serialized = new SerializedObject(flow); serialized.Update();
            EditorGUILayout.PropertyField(serialized.FindProperty("player"));
            EditorGUILayout.PropertyField(serialized.FindProperty("areas"),true);
            EditorGUILayout.PropertyField(serialized.FindProperty("onComplete")); EditorGUILayout.PropertyField(serialized.FindProperty("onFailed"));
            serialized.ApplyModifiedProperties();
            foreach (var area in flow.areas)
            {
                if (!area) continue;
                EditorGUILayout.LabelField(area.title,EditorStyles.boldLabel);
                var data = new SerializedObject(area); data.Update();
                foreach (string field in new[]{"title","playerAnchor","playerCover","enemies","exitGates","onEntered","onCleared"}) EditorGUILayout.PropertyField(data.FindProperty(field),true);
                data.ApplyModifiedProperties();
                if (GUILayout.Button("Select "+area.title)) { Selection.activeGameObject = area.gameObject; SceneView.lastActiveSceneView?.FrameSelected(); }
            }
            if (GUILayout.Button("Validate Setup")) { string error = flow.ValidateSetup(); if (error == null) Debug.Log("Cover combat setup is valid.",flow); else Debug.LogError(error,flow); }
            EditorGUILayout.EndScrollView();
        }
    }
    static GameObject Node(string name, Transform parent, Vector3 local)
    {
        var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go,"Create cover layout");
        if (parent) go.transform.SetParent(parent,false); go.transform.localPosition = local; return go;
    }
    static void ConnectWeapons(CoverCombatPlayer player)
    {
        Undo.RecordObject(player,"Bind FPS weapons");
        var game = player.weapons;
        if (!game)
        {
            // Reuse an existing FPS component associated with this camera, when available.
            // 괄호 안에 FindObjectsSortMode.None을 추가했습니다.
            foreach (var candidate in UnityEngine.Object.FindObjectsByType<LectureFPS.FpsLectureGame>(FindObjectsSortMode.None))
                if (candidate.view == player.view) { game = candidate; break; }

        }
        if (!game) game = Undo.AddComponent<LectureFPS.FpsLectureGame>(Node("FPS Weapons",player.transform,Vector3.zero));
        FpsLectureSetupWindow.ConfigureWeapons(game,player.view);
        Undo.RecordObject(game,"Bind cover firing rules"); game.combatDriver = player; game.FlowManaged = true;
        player.weapons = game; EditorUtility.SetDirty(player); EditorUtility.SetDirty(game);
    }
    static GameObject Cube(string name, Transform parent, Vector3 local, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        Undo.RegisterCreatedObjectUndo(go,"Create cover geometry"); go.transform.SetParent(parent,false); go.transform.localPosition = local; go.transform.localScale = scale; return go;
    }
    static CoverCombatCover Barrier(string name, Transform parent, Vector3 local, Vector3 scale)
    {
        var go = Cube(name,parent,local,scale); var cover = go.AddComponent<CoverCombatCover>(); cover.visuals = go.GetComponentsInChildren<Renderer>(); return cover;
    }
    static CoverCombatArea BuildArea(CoverCombatFlow flow, Vector3 position, int index)
    {
        var root = Node("Area "+index,flow.transform,Vector3.zero); root.transform.position = position;
        var area = root.AddComponent<CoverCombatArea>(); area.flow = flow; area.title = "Area "+index;
        var box = root.GetComponent<BoxCollider>(); box.isTrigger = true; box.center = new Vector3(0,1,0); box.size = new Vector3(6,3,3);
        var rigid = root.AddComponent<Rigidbody>(); rigid.isKinematic = true; rigid.useGravity = false;
        area.playerAnchor = Node("Player Anchor",root.transform,Vector3.zero).transform;
        // Camera is above the solid cover: aiming rays clear it while its graphic occludes the lower screen.
        area.playerCover = Barrier("Player Cover",root.transform,new Vector3(0,.7f,1.5f),new Vector3(5,1.4f,.5f));
        area.enemies = new CoverCombatEnemy[3];
        for (int i=0;i<3;i++)
        {
            var enemyRoot = Node("Enemy "+(i+1),root.transform,new Vector3((i-1)*4,0,12));
            var enemy = enemyRoot.AddComponent<CoverCombatEnemy>(); area.enemies[i] = enemy;
            var body = Cube("Actor + Hitbox",enemyRoot.transform,new Vector3(0,1,0),new Vector3(.8f,2,.8f)); enemy.actor = body.transform;
            enemy.muzzle = Node("Muzzle",body.transform,new Vector3(0,.3f,-.6f)).transform;
            if (i==0) { CoverCombatModelSetup.Apply(enemy,CoverEnemyAnimationMode.CoverShootIdle); continue; }
            enemy.style = CoverEnemyStyle.SidePeek;
            enemy.hiddenPoint = Node("Hidden Point",enemyRoot.transform,new Vector3(0,1,0)).transform;
            enemy.exposedPoint = Node("Exposed Point",enemyRoot.transform,new Vector3(1.6f,1,0)).transform;
            enemy.assignedCover = Barrier("Enemy Cover",root.transform,new Vector3((i-1)*4,1.2f,10.8f),new Vector3(2,2.4f,.6f));
            if (i==2)
            {
                enemy.assignedCover.transform.localScale = new Vector3(2,1.1f,.6f);
                enemy.assignedCover.transform.localPosition = new Vector3((i-1)*4,.55f,10.8f);
            }
            CoverCombatModelSetup.Apply(enemy,i==2 ? CoverEnemyAnimationMode.Gunplay : CoverEnemyAnimationMode.CoverShootIdle);
        }
        var gate = Cube("Exit Gate",root.transform,new Vector3(0,1.5f,17),new Vector3(18,3,.5f)); area.exitGates = new[]{gate};
        return area;
    }
    void Sample()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
        var root = Node("Cover Combat Sample",null,Vector3.zero); flow = root.AddComponent<CoverCombatFlow>();
        Cube("Ground",root.transform,new Vector3(0,-.25f,18),new Vector3(20,.5f,60));
        Cube("Left Wall",root.transform,new Vector3(-10,2,18),new Vector3(.5f,4,60));
        Cube("Right Wall",root.transform,new Vector3(10,2,18),new Vector3(.5f,4,60));
        var playerGo = Node("Player",root.transform,new Vector3(0,.05f,-4));
        var controller = playerGo.AddComponent<CharacterController>(); controller.height = 1.8f; controller.center = Vector3.up*.9f; controller.radius = .3f;
        var cameraGo = Node("Camera",playerGo.transform,new Vector3(0,1.65f,0)); var camera = cameraGo.AddComponent<Camera>(); camera.nearClipPlane = .05f; cameraGo.AddComponent<AudioListener>();
        flow.player = playerGo.AddComponent<CoverCombatPlayer>(); flow.player.view = camera;
        ConnectWeapons(flow.player);
        flow.areas = new[]{BuildArea(flow,Vector3.zero,1),BuildArea(flow,new Vector3(0,0,24),2)};
        var light = Node("Sun",root.transform,Vector3.zero).AddComponent<Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(50,-30,0);
        EditorSceneManager.MarkSceneDirty(scene); Selection.activeGameObject = root;
        Debug.Log("Sample created in an additive unsaved scene. Save as SampleScene.unity. For isolated play, close other loaded scenes first.",root);
    }
}
