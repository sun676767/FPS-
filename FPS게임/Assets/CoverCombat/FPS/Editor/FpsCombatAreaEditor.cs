using LectureFPS;
using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(FpsCombatArea))]
public sealed class FpsCombatAreaEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject,"encounterSource","encounterDefinition","serializedEncounter","encounter");
        serializedObject.ApplyModifiedProperties();
        FpsEncounterAuthoring.DrawData((FpsCombatArea)target);
        EditorGUILayout.HelpBox("Orange box: preparation entry trigger. Cyan box: combat planning bounds (visual guide). Use Edit Collider on BoxCollider to resize the trigger. Moving this area moves child spawn points and defense sites.",MessageType.Info);
    }
    void OnSceneGUI()
    {
        var area = (FpsCombatArea)target;
        Handles.Label(area.transform.position + Vector3.up*4,area.title + "\nEntry trigger / Combat area");
        if (area.waveStartPoint)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 position = Handles.PositionHandle(area.waveStartPoint.position,area.waveStartPoint.rotation);
            if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(area.waveStartPoint,"Move wave start"); area.waveStartPoint.position = position; }
            Handles.Label(area.waveStartPoint.position+Vector3.up,"Regional Wave Start");
        }
        var data = area.EncounterData;
        if (data?.waves != null) foreach (var wave in data.waves) if (wave?.groups != null) foreach (var group in wave.groups)
        {
            if (group == null) continue;
            var point = area.ResolveSpawn(group.spawnPointId);
            if (!point) continue;
            Handles.color = Color.red; Handles.DrawDottedLine(area.transform.position,point.position,5);
            Handles.Label(point.TransformPoint(group.offset),$"{(group.prefab ? group.prefab.name : "Missing prefab")} x{group.count}");
        }
        if (area.UsesLegacyEncounter && area.encounter.waves != null)
            foreach (var wave in area.encounter.waves)
                if (wave.spawns != null) foreach (var spawn in wave.spawns) if (spawn.spawnPoint)
                {
                    Handles.color = Color.red; Handles.DrawDottedLine(area.transform.position,spawn.spawnPoint.position,5);
                    Handles.Label(spawn.spawnPoint.position, $"{(spawn.prefab ? spawn.prefab.name : "Missing prefab")} x{spawn.count}");
                }
    }
}
