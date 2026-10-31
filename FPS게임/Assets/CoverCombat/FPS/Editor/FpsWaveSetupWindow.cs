using System;
using System.Collections.Generic;
using LectureFPS;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
public sealed class FpsWaveSetupWindow : EditorWindow
{
    FpsLectureGame game;
    readonly List<FpsWave> waves = new List<FpsWave> { NewWave(1) };
    FpsWaveDirector edit;
    Transform defaultSpawnPoint;
    Vector2 scroll;
    float delay = 3;
    static FpsWave NewWave(int n) => new FpsWave { title = "Wave " + n, spawns = new[] { new FpsWaveSpawn() } };
    [MenuItem("Tools/FPS Lecture/9_Wave Setup", false, 9)]
    static void Open() => GetWindow<FpsWaveSetupWindow>("Wave Setup");
    void OnEnable() => game = UnityEngine.Object.FindAnyObjectByType<FpsLectureGame>();
    void OnGUI()
    {
        EditorGUILayout.HelpBox("Drag enemy prefabs from Project. Each group spawns Count enemies in a grid around its spawn point. The same prefab can be used repeatedly. Prefab root needs FpsTarget, FpsEnemyAI and NavMeshAgent. Bake NavMesh before Play.", MessageType.Info);
        game = (FpsLectureGame)EditorGUILayout.ObjectField("Game", game, typeof(FpsLectureGame), true);
        edit = (FpsWaveDirector)EditorGUILayout.ObjectField("Existing encounter", edit, typeof(FpsWaveDirector), true);
        if (edit && GUILayout.Button("Load existing encounter"))
        {
            game = edit.game; defaultSpawnPoint = edit.defaultSpawnPoint; delay = edit.betweenWaves; waves.Clear();
            foreach (var wave in edit.waves) waves.Add(Clone(wave));
        }
        defaultSpawnPoint = (Transform)EditorGUILayout.ObjectField("Default spawn point",defaultSpawnPoint,typeof(Transform),true);
        delay = EditorGUILayout.Slider("Between waves", delay, 0, 10);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < waves.Count; i++)
        {
            var wave = waves[i];
            EditorGUILayout.Space();
            wave.title = EditorGUILayout.TextField("Wave " + (i+1), wave.title);
            if (wave.enemies != null && wave.enemies.Length > 0)
                EditorGUILayout.HelpBox($"Legacy scene enemies retained: {wave.enemies.Length}", MessageType.Info);
            for (int j = 0; j < wave.spawns.Length; j++)
            {
                var spawn = wave.spawns[j];
                EditorGUILayout.LabelField("Prefab group " + (j+1), EditorStyles.boldLabel);
                spawn.prefab = (GameObject)EditorGUILayout.ObjectField("Enemy prefab", spawn.prefab, typeof(GameObject), false);
                spawn.count = EditorGUILayout.IntSlider("Count", spawn.count, 1, 100);
                spawn.spawnPoint = (Transform)EditorGUILayout.ObjectField("Spawn point", spawn.spawnPoint, typeof(Transform), true);
                spawn.spacing = EditorGUILayout.Slider("Spacing", spawn.spacing, .5f, 10);
                if (game && GUILayout.Button("Create spawn point for this group"))
                {
                    var marker = new GameObject($"Wave {i+1} Spawn {j+1}"); Undo.RegisterCreatedObjectUndo(marker,"Create spawn point");
                    marker.transform.SetParent(game.transform,false);
                    marker.transform.position = game.view ? game.view.transform.position + Vector3.ProjectOnPlane(game.view.transform.forward, Vector3.up).normalized * 8 : game.transform.position;
                    if (Physics.Raycast(marker.transform.position + Vector3.up * 2, Vector3.down, out var ground, 30)) marker.transform.position = ground.point;
                    spawn.spawnPoint = marker.transform; Selection.activeGameObject = marker;
                    EditorSceneManager.MarkSceneDirty(marker.scene);
                }
            }
            if (GUILayout.Button("Add prefab group to wave " + (i+1)))
            { int n = wave.spawns.Length; Array.Resize(ref wave.spawns,n+1); wave.spawns[n] = new FpsWaveSpawn(); }
            if (wave.spawns.Length > 0 && GUILayout.Button("Remove last prefab group from wave " + (i+1))) Array.Resize(ref wave.spawns,wave.spawns.Length-1);
        }
        EditorGUILayout.EndScrollView();
        if (GUILayout.Button("Add wave")) waves.Add(NewWave(waves.Count+1));
        if (waves.Count > 1 && GUILayout.Button("Remove last wave")) waves.RemoveAt(waves.Count-1);
        string error = Validate();
        if (error != null) EditorGUILayout.HelpBox(error,MessageType.Warning);
        using (new EditorGUI.DisabledScope(error != null || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Create new wave encounter")) Build(false);
            if (edit && GUILayout.Button("Apply to existing encounter")) Build(true);
        }
    }
    string Validate()
    {
        if (!game || EditorUtility.IsPersistent(game)) return "Assign a Game in the scene.";
        if (waves.Count == 0) return "Add at least one wave.";
        foreach (var wave in waves)
        {
            if (wave.spawns.Length == 0 && (wave.enemies == null || wave.enemies.Length == 0)) return wave.title + ": add a prefab group.";
            foreach (var spawn in wave.spawns)
            {
                if (!spawn.prefab || !PrefabUtility.IsPartOfPrefabAsset(spawn.prefab)) return wave.title + ": assign a Project prefab.";
                if (spawn.prefab.transform.parent || !spawn.prefab.GetComponent<FpsEnemyAI>() || !spawn.prefab.GetComponent<FpsTarget>() || !spawn.prefab.GetComponent<UnityEngine.AI.NavMeshAgent>()) return wave.title + ": prefab root requires FpsEnemyAI, FpsTarget and NavMeshAgent.";
                var origin = spawn.spawnPoint ? spawn.spawnPoint : defaultSpawnPoint;
                if (!origin || EditorUtility.IsPersistent(origin) || origin.gameObject.scene != game.gameObject.scene) return wave.title + ": assign a spawn point in the Game scene.";
            }
        }
        if (edit && (EditorUtility.IsPersistent(edit) || edit.gameObject.scene != game.gameObject.scene)) return "Existing encounter must belong to the Game scene.";
        return null;
    }
    static FpsWave Clone(FpsWave source)
    {
        var result = new FpsWave { title = source.title, enemies = source.enemies == null ? Array.Empty<FpsTarget>() : (FpsTarget[])source.enemies.Clone(), spawns = new FpsWaveSpawn[source.spawns?.Length ?? 0] };
        for (int i = 0; i < result.spawns.Length; i++)
        { var s = source.spawns[i]; result.spawns[i] = new FpsWaveSpawn { prefab = s.prefab, count = s.count, spawnPoint = s.spawnPoint, spacing = s.spacing, offset = s.offset }; }
        return result;
    }
    void Build(bool update)
    {
        if (Validate() != null) return;
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        FpsWaveDirector director;
        if (update) { director = edit; Undo.RecordObject(director,"Update waves"); }
        else
        {
            var root = new GameObject("Wave Encounter"); Undo.RegisterCreatedObjectUndo(root,"Create waves"); root.transform.SetParent(game.transform,false);
            director = Undo.AddComponent<FpsWaveDirector>(root);
            var ui = new GameObject("Wave HUD",typeof(RectTransform)); Undo.RegisterCreatedObjectUndo(ui,"Wave HUD"); ui.transform.SetParent(root.transform,false);
            var canvas = Undo.AddComponent<Canvas>(ui); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = Undo.AddComponent<CanvasScaler>(ui); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080);
            var label = new GameObject("Wave Status",typeof(RectTransform)); Undo.RegisterCreatedObjectUndo(label,"Wave label"); label.transform.SetParent(ui.transform,false);
            var text = Undo.AddComponent<Text>(label); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 28; text.alignment = TextAnchor.UpperCenter; text.raycastTarget = false;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f,1); text.rectTransform.pivot = new Vector2(.5f,1); text.rectTransform.anchoredPosition = new Vector2(0,-30); text.rectTransform.sizeDelta = new Vector2(1000,80);
            text.text = "V at terminal: start waves"; director.status = text;
        }
        director.game = game; director.defaultSpawnPoint = defaultSpawnPoint; director.betweenWaves = delay; director.waves = waves.ConvertAll(Clone).ToArray();
        EditorUtility.SetDirty(director); EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        Undo.CollapseUndoOperations(group); edit = director; Selection.activeGameObject = director.gameObject;
    }
}
