using System;
using System.IO;
using CoverCombat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CoverCombatModelSetup
{
    const string Models = "Assets/CoverCombat/Runtime/Model/";
    const string Output = "Assets/CoverCombat/Animations";
    [MenuItem("Tools/Cover Combat/Models/Apply Cover - Shoot Idle to Selected Enemies")]
    static void CoverIdle() => ApplySelection(CoverEnemyAnimationMode.CoverShootIdle);
    [MenuItem("Tools/Cover Combat/Models/Apply Idle - Gunplay to Selected Enemies")]
    static void Gunplay() => ApplySelection(CoverEnemyAnimationMode.Gunplay);
    [MenuItem("Tools/Cover Combat/Models/Enable Root Motion on Selected Enemies")]
    static void EnableRootMotion()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        foreach (var root in Selection.gameObjects)
            foreach (var enemy in root.GetComponentsInChildren<CoverCombatEnemy>(true))
            {
                if (!enemy.animator) continue;
                Undo.RecordObject(enemy.animator,"Enable enemy root motion");
                enemy.animator.applyRootMotion = true;
                UpdateRootMotionClips(enemy.animator.runtimeAnimatorController);
                EditorUtility.SetDirty(enemy.animator); EditorSceneManager.MarkSceneDirty(enemy.gameObject.scene);
            }
        AssetDatabase.SaveAssets();
    }
    static void RootMotionSettings(AnimationClip clip)
    {
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopBlendOrientation = false;
        settings.loopBlendPositionY = false;
        settings.loopBlendPositionXZ = false;
        AnimationUtility.SetAnimationClipSettings(clip,settings);
        EditorUtility.SetDirty(clip);
    }
    static void UpdateRootMotionClips(RuntimeAnimatorController controller)
    {
        if (!controller) return;
        foreach (var clip in controller.animationClips)
        {
            // Only modify generated copies; leave source FBX and custom controllers untouched.
            string path = AssetDatabase.GetAssetPath(clip);
            if (!path.StartsWith(Output+"/",StringComparison.Ordinal)) continue;
            Undo.RecordObject(clip,"Enable root motion extraction"); RootMotionSettings(clip);
        }
    }
    static void ApplySelection(CoverEnemyAnimationMode mode)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        foreach (var root in Selection.gameObjects)
            foreach (var enemy in root.GetComponentsInChildren<CoverCombatEnemy>(true))
                try { Apply(enemy,mode); EditorSceneManager.MarkSceneDirty(enemy.gameObject.scene); }
                catch (Exception e) { Debug.LogException(e,enemy); }
    }
    static AnimationClip Clip(string file, string name, bool loop, UnityEngine.Object owner)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(Models+file+".fbx"))
        {
            if (!(asset is AnimationClip source) || source.name.StartsWith("__preview__")) continue;
            var clip = UnityEngine.Object.Instantiate(source); clip.name = name;
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop; settings.loopBlend = loop;
            AnimationUtility.SetAnimationClipSettings(clip,settings);
            RootMotionSettings(clip);
            // Damage is timed by the combat mode, not imported animation events.
            AnimationUtility.SetAnimationEvents(clip,Array.Empty<AnimationEvent>());
            AssetDatabase.AddObjectToAsset(clip,owner); return clip;
        }
        throw new InvalidOperationException("No animation clip in "+Models+file+".fbx");
    }
    static AnimatorController Controller(CoverEnemyAnimationMode mode)
    {
        Directory.CreateDirectory(Output);
        string path = Output+"/"+mode+".controller";
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (existing) { UpdateRootMotionClips(existing); AssetDatabase.SaveAssets(); return existing; }
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        var machine = controller.layers[0].stateMachine;
        var hideClip = Clip("Taking Cover Idle","Cover Idle",true,controller);
        foreach (var name in new[]{"Idle","Hide","Shoot","Gunplay","Death"})
        {
            var state = machine.AddState(name);
            state.motion = name == "Shoot" ? Clip("Shooting Pistol","Shoot",true,controller)
                : name == "Gunplay" ? Clip("Gunplay","Gunplay",false,controller)
                : name == "Death" ? Clip("Swat@Death From The Back","Death",false,controller) : hideClip;
            state.writeDefaultValues = true;
            if (name == "Hide") machine.defaultState = state;
        }
        // Runtime owns transitions; Death has no outgoing transition and holds its final frame.
        EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets(); return controller;
    }
    public static void Apply(CoverCombatEnemy enemy, CoverEnemyAnimationMode mode)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Models+"Swat.fbx");
        if (!model) throw new InvalidOperationException("Missing Swat model.");
        var sourceAnimator = model.GetComponentInChildren<Animator>();
        if (!sourceAnimator || !sourceAnimator.avatar || !sourceAnimator.avatar.isValid || !sourceAnimator.avatar.isHuman)
            throw new InvalidOperationException("Swat requires a valid Humanoid Avatar. Check Rig import settings.");
        var controller = Controller(mode);
        Undo.RecordObject(enemy,"Apply cover enemy model");
        if (enemy.actor) Undo.DestroyObjectImmediate(enemy.actor.gameObject);
        // Disable old static hitboxes: only the animated body should be hittable.
        foreach (var collider in enemy.GetComponentsInChildren<Collider>()) Undo.DestroyObjectImmediate(collider);
        var actor = new GameObject("Animated Actor"); Undo.RegisterCreatedObjectUndo(actor,"Create animated actor"); actor.transform.SetParent(enemy.transform,false);
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model,actor.transform); Undo.RegisterCreatedObjectUndo(visual,"Create Swat model");
        visual.transform.localRotation = Quaternion.Euler(0,180,0);
        var animator = visual.GetComponentInChildren<Animator>(); animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = true; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        enemy.actor = actor.transform; enemy.animator = animator; enemy.animationMode = mode;
        enemy.idleState = "Idle"; enemy.hideState = "Hide"; enemy.shootState = "Shoot"; enemy.gunplayState = "Gunplay"; enemy.deathState = "Death";
        if (mode == CoverEnemyAnimationMode.Gunplay) enemy.style = CoverEnemyStyle.Stationary;
        // Fixed markers describe feet positions for a model (the old cube used its centre).
        if (enemy.hiddenPoint) { Undo.RecordObject(enemy.hiddenPoint,"Set model feet marker"); var p = enemy.hiddenPoint.position; p.y = enemy.transform.position.y; enemy.hiddenPoint.position = p; }
        if (enemy.exposedPoint) { Undo.RecordObject(enemy.exposedPoint,"Set model feet marker"); var p = enemy.exposedPoint.position; p.y = enemy.transform.position.y; enemy.exposedPoint.position = p; }
        if (enemy.hiddenPoint && enemy.style == CoverEnemyStyle.SidePeek) actor.transform.position = enemy.hiddenPoint.position;
        foreach (var bone in new[]{HumanBodyBones.Head,HumanBodyBones.Chest,HumanBodyBones.Hips,HumanBodyBones.LeftUpperArm,HumanBodyBones.RightUpperArm,HumanBodyBones.LeftUpperLeg,HumanBodyBones.RightUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightLowerLeg})
        {
            var transform = animator.GetBoneTransform(bone); if (!transform) continue;
            var hitbox = new GameObject("Hitbox "+bone); Undo.RegisterCreatedObjectUndo(hitbox,"Create animated hitbox"); hitbox.transform.SetParent(transform,false);
            var sphere = hitbox.AddComponent<SphereCollider>();
            float radius = bone == HumanBodyBones.Chest || bone == HumanBodyBones.Hips ? .22f : .13f;
            var scale = hitbox.transform.lossyScale;
            sphere.radius = radius / Mathf.Max(.0001f,Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
        }
        var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        var muzzle = new GameObject("Muzzle"); Undo.RegisterCreatedObjectUndo(muzzle,"Create animated muzzle"); muzzle.transform.SetParent(hand ? hand : actor.transform,false); enemy.muzzle = muzzle.transform;
        EditorUtility.SetDirty(enemy); EditorUtility.SetDirty(animator);
    }
}
