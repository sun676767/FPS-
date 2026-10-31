using System.Collections.Generic;
using System.IO;
using LectureFPS;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public sealed class FpsEnemySetupWindow : EditorWindow
{
    GameObject model;
    bool animationEvents;
    float hitDelay = .35f, attackWindow = 1.2f;
    Transform player;
    readonly string[] slots = { "Idle", "Move", "Attack", "Hit", "Death" };
    readonly AnimationClip[] clips = new AnimationClip[5];
    const string Folder = "Assets/FPS/Animations";
    [MenuItem("Tools/FPS Lecture/2_Enemy AI Setup", false, 2)]
    static void Open() => GetWindow<FpsEnemySetupWindow>("Enemy AI Setup");
    void OnGUI()
    {
        EditorGUILayout.LabelField("Shared Humanoid Animator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("모델과 Mixamo 클립은 Rig > Humanoid로 임포트하세요. Idle/Move는 Loop Time을 사용합니다. NavMeshAgent가 이동하므로 Root Motion은 끕니다. 모델이 없으면 캡슐로 생성합니다. NavMesh를 먼저 Bake해야 추적합니다.", MessageType.Info);
        player = (Transform)EditorGUILayout.ObjectField("Player root", player, typeof(Transform), true);
        model = (GameObject)EditorGUILayout.ObjectField("Humanoid model", model, typeof(GameObject), false);
        for (int i = 0; i < slots.Length; i++) clips[i] = (AnimationClip)EditorGUILayout.ObjectField(slots[i], clips[i], typeof(AnimationClip), false);
        animationEvents = EditorGUILayout.Toggle("Use AttackHit event", animationEvents);
        hitDelay = EditorGUILayout.Slider("Fallback hit delay", hitDelay, 0, 2);
        attackWindow = EditorGUILayout.Slider("Attack window", attackWindow, hitDelay + .05f, 4);
        EditorGUILayout.HelpBox("Event mode: add AttackHit to the Attack clip at the contact frame. The event receiver is attached to the Animator object. Without events, leave event mode off to use delayed damage.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!player || EditorApplication.isPlayingOrWillChangePlaymode))
            if (GUILayout.Button("Create Enemy + Override Animator")) CreateEnemy();
    }
    static AnimatorController SharedController()
    {
        Directory.CreateDirectory(Folder);
        string path = Folder + "/EnemyShared.controller";
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (existing) return existing;
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        var machine = controller.layers[0].stateMachine;
        var states = new AnimatorState[5];
        var names = new[] { "Idle", "Move", "Attack", "Hit", "Death" };
        for (int i = 0; i < names.Length; i++)
        {
            var clip = new AnimationClip { name = names[i] + "Slot" };
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Constant(0, 1, 0));
            AssetDatabase.AddObjectToAsset(clip, controller);
            states[i] = machine.AddState(names[i], new Vector3(250, i * 65));
            states[i].motion = clip;
        }
        machine.defaultState = states[0];
        var move = states[0].AddTransition(states[1]); move.hasExitTime = false; move.duration = .15f; move.AddCondition(AnimatorConditionMode.Greater, .1f, "Speed");
        var idle = states[1].AddTransition(states[0]); idle.hasExitTime = false; idle.duration = .15f; idle.AddCondition(AnimatorConditionMode.Less, .1f, "Speed");
        // Death gets priority and cannot transition out until the actor is reset.
        var death = machine.AddAnyStateTransition(states[4]); death.hasExitTime = false; death.duration = .1f; death.canTransitionToSelf = false; death.AddCondition(AnimatorConditionMode.If, 0, "Dead");
        for (int i = 2; i <= 3; i++)
        {
            var enter = machine.AddAnyStateTransition(states[i]); enter.hasExitTime = false; enter.duration = .08f; enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0, names[i]); enter.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            var exit = states[i].AddTransition(states[0]); exit.hasExitTime = true; exit.exitTime = .9f; exit.duration = .1f; exit.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
        }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }
    void CreateEnemy()
    {
        if (model)
        {
            var a = model.GetComponentInChildren<Animator>();
            if (!a || !a.avatar || !a.avatar.isHuman || !a.avatar.isValid)
            { Debug.LogError("Model requires a valid Humanoid Avatar."); return; }
        }
        foreach (var clip in clips) if (clip && !clip.isHumanMotion) { Debug.LogError("Use Humanoid animation clips."); return; }
        var controller = SharedController();
        var over = new AnimatorOverrideController(controller);
        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        over.GetOverrides(pairs);
        for (int i = 0; i < pairs.Count; i++)
            for (int j = 0; j < slots.Length; j++)
                if (pairs[i].Key.name == slots[j] + "Slot" && clips[j]) pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, clips[j]);
        over.ApplyOverrides(pairs);
        AssetDatabase.CreateAsset(over, AssetDatabase.GenerateUniqueAssetPath(Folder + "/Enemy.overrideController"));
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        var root = new GameObject("Enemy");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, player.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(root, "Create Enemy");
        root.transform.position = player.position + player.forward * 8;
        if (Physics.Raycast(root.transform.position + Vector3.up * 3, Vector3.down, out var ground, 20)) root.transform.position = ground.point;
        var collider = Undo.AddComponent<CapsuleCollider>(root); collider.height = 1.8f; collider.center = Vector3.up * .9f; collider.radius = .35f;
        var agent = Undo.AddComponent<NavMeshAgent>(root); agent.height = 1.8f; agent.radius = .35f; agent.speed = 2.5f; agent.stoppingDistance = 1.5f;
        var target = Undo.AddComponent<FpsTarget>(root);
        var ai = Undo.AddComponent<FpsEnemyAI>(root); ai.player = player; ai.useAnimationEvents = animationEvents && model; ai.hitDelay = hitDelay; ai.attackWindow = attackWindow;
        if (model)
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            Undo.RegisterCreatedObjectUndo(visual, "Enemy visual");
            visual.transform.localPosition = Vector3.zero; visual.transform.localRotation = Quaternion.identity;
            foreach (var c in visual.GetComponentsInChildren<Collider>()) c.enabled = false;
            ai.animator = visual.GetComponentInChildren<Animator>();
            var receiver = Undo.AddComponent<FpsEnemyAnimationEvents>(ai.animator.gameObject);
            receiver.owner = ai;
            ai.animator.runtimeAnimatorController = over; ai.animator.applyRootMotion = false;
        }
        else
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Undo.RegisterCreatedObjectUndo(visual, "Enemy placeholder");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.up * .9f;
            visual.transform.localScale = new Vector3(.7f, .9f, .7f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
        }
        if (!player.GetComponent<FpsPlayerHealth>()) Undo.AddComponent<FpsPlayerHealth>(player.gameObject);
        EditorUtility.SetDirty(ai);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Undo.CollapseUndoOperations(group);
        AssetDatabase.SaveAssets(); Selection.activeGameObject = root;
    }
}
