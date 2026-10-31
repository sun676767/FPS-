using System.Collections.Generic;
using LectureFPS;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class FpsEncounterAuthoring
{
    public static void DrawData(FpsCombatArea area)
    {
        var serialized = new SerializedObject(area); serialized.Update();
        EditorGUILayout.PropertyField(serialized.FindProperty("encounterSource"));
        if (area.encounterSource == FpsCombatArea.EncounterSource.Serialized)
            EditorGUILayout.PropertyField(serialized.FindProperty("serializedEncounter"),true);
        else EditorGUILayout.PropertyField(serialized.FindProperty("encounterDefinition"));
        serialized.ApplyModifiedProperties();
        if (area.encounterSource == FpsCombatArea.EncounterSource.ScriptableObject && area.encounterDefinition)
        {
            var asset = new SerializedObject(area.encounterDefinition); asset.Update();
            EditorGUILayout.PropertyField(asset.FindProperty("data"),true); asset.ApplyModifiedProperties();
        }
        var data = area.EncounterData;
        if (data != null)
        {
            string error = data.Validate();
            if (error != null) EditorGUILayout.HelpBox(error,MessageType.Warning);
        }
        if (!Application.isPlaying && area.UsesLegacyEncounter)
        {
            EditorGUILayout.HelpBox("Legacy scene Director detected. Convert its prefab groups and spawn bindings into an Encounter Definition.",MessageType.Info);
            if (GUILayout.Button("Convert legacy Director to Encounter asset")) Convert(area);
        }
    }
    public static void Convert(FpsCombatArea area)
    {
        var director = area.encounter;
        if (!director || Application.isPlaying) return;
        if (!director.ValidateSetup()) { Debug.LogError("Fix the legacy encounter before exporting.",area); return; }
        foreach (var wave in director.waves)
            if (wave.enemies != null && wave.enemies.Length > 0) { Debug.LogError("Legacy scene-enemy slots cannot be exported as prefab data. Convert them to prefab spawn groups first.",area); return; }
        string path = EditorUtility.SaveFilePanelInProject("Save Encounter Definition","EncounterDefinition","asset","Select asset location");
        if (string.IsNullOrEmpty(path)) return;
        var asset = ScriptableObject.CreateInstance<FpsEncounterDefinition>(); asset.data.betweenWaves = director.betweenWaves;
        asset.data.waves = new FpsWaveData[director.waves.Length];
        var bindings = new List<FpsCombatArea.SpawnBinding>();
        var pointIds = new Dictionary<Transform,string>();
        for (int w = 0; w < director.waves.Length; w++)
        {
            var source = director.waves[w]; var data = new FpsWaveData { title = source.title, groups = new FpsSpawnGroupData[source.spawns.Length] };
            for (int g = 0; g < source.spawns.Length; g++)
            {
                var group = source.spawns[g]; var point = group.spawnPoint ? group.spawnPoint : director.defaultSpawnPoint;
                string id = "";
                if (point && point != area.waveStartPoint)
                {
                    if (!pointIds.TryGetValue(point,out id)) { id = "spawn_"+(pointIds.Count+1); pointIds.Add(point,id); bindings.Add(new FpsCombatArea.SpawnBinding { id = id, point = point }); }
                }
                data.groups[g] = new FpsSpawnGroupData { prefab = group.prefab, count = group.count, spacing = group.spacing, spawnPointId = id, offset = group.offset };
            }
            asset.data.waves[w] = data;
        }
        string error = asset.data.Validate();
        if (error != null) { Object.DestroyImmediate(asset); Debug.LogError(error,area); return; }
        AssetDatabase.CreateAsset(asset,path); AssetDatabase.SaveAssets();
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
        Undo.RecordObject(area,"Convert encounter to data"); area.encounterDefinition = asset; area.encounterSource = FpsCombatArea.EncounterSource.ScriptableObject; area.spawnBindings = bindings.ToArray();
        Undo.RecordObject(director,"Disable legacy encounter"); director.flowManaged = true; director.enabled = false;
        EditorUtility.SetDirty(area); EditorUtility.SetDirty(director); EditorSceneManager.MarkSceneDirty(area.gameObject.scene); Undo.CollapseUndoOperations(undo);
        Selection.activeObject = asset;
    }
}
