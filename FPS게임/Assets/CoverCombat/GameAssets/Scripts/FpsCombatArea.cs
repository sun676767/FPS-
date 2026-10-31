using UnityEngine;
using UnityEngine.Events;
namespace LectureFPS
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class FpsCombatArea : MonoBehaviour
    {
        public string areaId, title = "Combat Area";
        public enum EncounterSource { ScriptableObject, Serialized }
        public EncounterSource encounterSource;
        public FpsEncounterDefinition encounterDefinition;
        public FpsEncounterData serializedEncounter = new FpsEncounterData();
        [System.Serializable] public sealed class SpawnBinding { public string id; public Transform point; }
        public SpawnBinding[] spawnBindings = new SpawnBinding[0];
        [HideInInspector] public FpsWaveDirector encounter;
        public FpsEncounterData EncounterData => encounterSource == EncounterSource.Serialized ? serializedEncounter : encounterDefinition ? encounterDefinition.data : null;
        public bool UsesLegacyEncounter => encounterSource == EncounterSource.ScriptableObject && !encounterDefinition && encounter;
        public Transform ResolveSpawn(string id)
        {
            if (string.IsNullOrEmpty(id)) return waveStartPoint;
            if (spawnBindings != null) foreach (var binding in spawnBindings) if (binding != null && binding.id == id) return binding.point;
            return null;
        }
        public string ValidateEncounter(FpsLectureGame game)
        {
            if (UsesLegacyEncounter) return encounter.game == game && encounter.ValidateSetup() ? null : "Invalid legacy encounter; convert it to data.";
            var data = EncounterData;
            if (data == null) return "Assign Encounter Definition or choose Serialized data.";
            string error = data.Validate(); if (error != null) return error;
            var ids = new System.Collections.Generic.HashSet<string>();
            if (spawnBindings != null) foreach (var binding in spawnBindings)
                if (binding == null || string.IsNullOrEmpty(binding.id) || !ids.Add(binding.id) || !binding.point || binding.point.gameObject.scene != gameObject.scene) return "Spawn bindings need unique IDs and points in this scene.";
            foreach (var wave in data.waves) foreach (var group in wave.groups)
            {
                var point = ResolveSpawn(group.spawnPointId);
                if (!point || point.gameObject.scene != gameObject.scene) return "Missing scene spawn point for ID: " + (string.IsNullOrEmpty(group.spawnPointId) ? "(Wave Start Point)" : group.spawnPointId);
            }
            return null;
        }
        public void ConfigureEncounter(FpsLectureGame game)
        {
            if (UsesLegacyEncounter) { encounter.flowManaged = true; encounter.enabled = true; return; }
            // Only runtime copies contain scene references. Shared assets remain unchanged.
            if (encounter) { encounter.ResetEncounter(); encounter.enabled = false; }
            var root = new GameObject("Encounter Runtime"); root.transform.SetParent(transform,false);
            encounter = root.AddComponent<FpsWaveDirector>();
            encounter.game = game; encounter.flowManaged = true; encounter.defaultSpawnPoint = waveStartPoint;
            var data = EncounterData; encounter.betweenWaves = data.betweenWaves;
            encounter.waves = new FpsWave[data.waves.Length];
            for (int w = 0; w < data.waves.Length; w++)
            {
                var source = data.waves[w]; var wave = new FpsWave { title = source.title, spawns = new FpsWaveSpawn[source.groups.Length] };
                for (int g = 0; g < source.groups.Length; g++)
                {
                    var group = source.groups[g];
                    wave.spawns[g] = new FpsWaveSpawn { prefab = group.prefab, count = group.count, spacing = group.spacing, spawnPoint = ResolveSpawn(group.spawnPointId), offset = group.offset };
                }
                encounter.waves[w] = wave;
            }
        }
        public Transform waveStartPoint;
        public bool autoStartCombat = true;
        [Min(0)] public float preparationSeconds = 30;
        [Min(0)] public float countdownSeconds = 3;
        public FpsBuildGrid buildGrid;
        public FpsBuildSite[] buildSites = new FpsBuildSite[0];
        public Vector3 combatSize = new Vector3(30, 6, 30);
        [Min(0)] public int clearReward = 200;
        public UnityEvent onPreparation, onCombat, onCleared;
        [HideInInspector] public FpsGameplayFlow flow;
        void Awake() { if (encounter && waveStartPoint) encounter.defaultSpawnPoint = waveStartPoint; }
        void OnTriggerEnter(Collider other) => TryEnter(other);
        void OnTriggerStay(Collider other) => TryEnter(other);
        void TryEnter(Collider other)
        {
            if (flow && other.GetComponentInParent<FpsPlayerHealth>() == flow.player) flow.ReachArea(this);
        }
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1,.65f,0,.3f);
            var box = GetComponent<BoxCollider>();
            Gizmos.matrix = transform.localToWorldMatrix;
            if (box) Gizmos.DrawWireCube(box.center,box.size);
            Gizmos.color = Color.cyan; Gizmos.DrawWireCube(Vector3.up * combatSize.y * .5f,combatSize);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
