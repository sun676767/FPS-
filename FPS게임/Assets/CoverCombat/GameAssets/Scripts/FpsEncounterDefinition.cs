using System;
using UnityEngine;
using UnityEngine.AI;
namespace LectureFPS
{
    [Serializable] public sealed class FpsSpawnGroupData
    {
        public GameObject prefab;
        [Min(1)] public int count = 3;
        [Min(.5f)] public float spacing = 2;
        [Tooltip("Empty uses the area's Wave Start Point. Otherwise matches a scene spawn binding ID.")] public string spawnPointId;
        public Vector3 offset;
    }
    [Serializable] public sealed class FpsWaveData
    {
        public string title = "Wave";
        public FpsSpawnGroupData[] groups = new FpsSpawnGroupData[0];
    }
    [Serializable] public sealed class FpsEncounterData
    {
        [Min(0)] public float betweenWaves = 3;
        public FpsWaveData[] waves = new FpsWaveData[0];
        public string Validate()
        {
            if (waves == null || waves.Length == 0) return "Add at least one wave.";
            if (float.IsNaN(betweenWaves) || float.IsInfinity(betweenWaves) || betweenWaves < 0) return "Invalid wave interval.";
            for (int w = 0; w < waves.Length; w++)
            {
                var wave = waves[w];
                if (wave?.groups == null || wave.groups.Length == 0) return $"Wave {w+1}: add a spawn group.";
                for (int g = 0; g < wave.groups.Length; g++)
                {
                    var group = wave.groups[g]; string slot = $"Wave {w+1}, Group {g+1}: ";
                    if (group == null || !group.prefab || group.prefab.scene.IsValid() || !group.prefab.GetComponent<FpsEnemyAI>() || !group.prefab.GetComponent<FpsTarget>() || !group.prefab.GetComponent<NavMeshAgent>()) return slot+"assign an enemy prefab with AI, Target and Agent on its root.";
                    if (group.count < 1 || group.count > 100 || group.spacing < .5f || float.IsNaN(group.spacing) || float.IsInfinity(group.spacing)) return slot+"invalid count or spacing.";
                }
            }
            return null;
        }
    }
    [CreateAssetMenu(menuName = "FPS Lecture/Encounter Definition",fileName = "EncounterDefinition")]
    public sealed class FpsEncounterDefinition : ScriptableObject
    {
        public FpsEncounterData data = new FpsEncounterData();
    }
}
