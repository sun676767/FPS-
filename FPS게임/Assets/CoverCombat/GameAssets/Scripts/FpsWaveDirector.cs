using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace LectureFPS
{
    [Serializable] public class FpsWaveSpawn
    {
        public GameObject prefab;
        [Min(1)] public int count = 3;
        public Transform spawnPoint;
        public Vector3 offset;
        [Min(.5f)] public float spacing = 2;
    }
    [Serializable] public class FpsWave
    {
        public string title = "Wave";
        // Preserve existing scene-based encounters.
        public FpsTarget[] enemies = Array.Empty<FpsTarget>();
        public FpsWaveSpawn[] spawns = Array.Empty<FpsWaveSpawn>();
    }
    [DefaultExecutionOrder(100)]
    public sealed class FpsWaveDirector : MonoBehaviour
    {
        public bool flowManaged;
        public bool IsCompleted => completed;
        public bool IsRunning => running;
        public string Failure => failure;
        public string FlowSummary
        {
            get
            {
                int alive = 0; foreach (var target in current) if (target && !target.IsDead) alive++;
                return waiting ? $"Next wave in {Mathf.CeilToInt(Mathf.Max(0,nextWaveTime-Time.time))}" : index >= 0 && index < waves.Length ? $"{waves[index].title} [{index+1}/{waves.Length}] | Remaining {alive}" : "";
            }
        }
        public FpsLectureGame Game => game;
        public FpsLectureGame game;
        public Transform defaultSpawnPoint;
        public FpsWave[] waves = Array.Empty<FpsWave>();
        public Text status;
        [Min(0)] public float betweenWaves = 3;
        readonly List<FpsTarget> spawned = new();
        readonly List<FpsTarget> current = new();
        Transform staging;
        int index = -1;
        float nextWaveTime;
        bool running, waiting, completed;
        string failure;
        public bool Owns(FpsTarget target)
        {
            if (spawned.Contains(target)) return true;
            foreach (var wave in waves) if (wave != null && wave.enemies != null && Array.IndexOf(wave.enemies, target) >= 0) return true;
            return false;
        }
        public bool ValidateSetup()
        {
            if (!game || waves == null || waves.Length == 0) return false;
            var unique = new HashSet<FpsTarget>();
            foreach (var wave in waves)
            {
                if (wave == null) return false;
                int total = 0;
                if (wave.enemies != null) foreach (var enemy in wave.enemies)
                {
                    if (!enemy || !enemy.GetComponent<FpsEnemyAI>() || !unique.Add(enemy)) return false;
                    total++;
                }
                if (wave.spawns != null) foreach (var group in wave.spawns)
                {
                    if (group == null || !group.prefab || !group.prefab.GetComponent<FpsEnemyAI>() || !group.prefab.GetComponent<FpsTarget>() || !group.prefab.GetComponent<NavMeshAgent>() || group.count < 1 || group.count > 100 || (!group.spawnPoint && !defaultSpawnPoint) || group.spacing < .5f) return false;
                    total += group.count;
                }
                if (total == 0) return false;
            }
            return true;
        }
        void Start()
        {
            if (!ValidateSetup()) { Debug.LogError("Invalid wave setup: check prefab roots, counts and spawn points.", this); enabled = false; return; }
            if (!flowManaged) ResetEncounter();
        }
        public void ResetEncounter()
        {
            running = waiting = completed = false; index = -1; failure = null;
            current.Clear();
            foreach (var enemy in spawned) if (enemy) { enemy.gameObject.SetActive(false); Destroy(enemy.gameObject); }
            spawned.Clear();
            foreach (var wave in waves) if (wave?.enemies != null) foreach (var enemy in wave.enemies) if (enemy) enemy.gameObject.SetActive(false);
            Refresh();
        }
        void Update()
        {
            if (!game || !game.enabled) return;
            if (!flowManaged && !running && !game.MenuOpen && game.NearTerminal && Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame) Begin();
            if (running && waiting && Time.time >= nextWaveTime) ActivateNext();
            if (running && !waiting)
            {
                bool allDead = true;
                foreach (var enemy in current) if (enemy && !enemy.IsDead) { allDead = false; break; }
                if (allDead)
                {
                    if (index == waves.Length - 1) { running = false; completed = true; }
                    else { waiting = true; nextWaveTime = Time.time + betweenWaves; }
                }
            }
            Refresh();
        }
        public void Begin()
        {
            if (running || !game || !game.NearTerminal || game.MenuOpen || !ValidateSetup()) return;
            StartEncounter();
        }
        public bool StartEncounter()
        {
            if (running || !ValidateSetup()) return false;
            ResetEncounter(); running = true; ActivateNext(); return failure == null;
        }
        public void SetEnemiesActive(bool active)
        {
            foreach (var target in current) if (target) { var ai = target.GetComponent<FpsEnemyAI>(); if (ai) ai.enabled = active; }
        }
        void ActivateNext()
        {
            waiting = false; index++; current.Clear();
            var wave = waves[index];
            var slots = new Dictionary<Transform,int>();
            var totals = new Dictionary<Transform,int>();
            var spacings = new Dictionary<Transform,float>();
            if (wave.spawns != null) foreach (var group in wave.spawns)
            {
                var origin = group.spawnPoint ? group.spawnPoint : defaultSpawnPoint;
                totals.TryGetValue(origin,out int total); totals[origin] = total+group.count;
                spacings.TryGetValue(origin,out float spacing); spacings[origin] = Mathf.Max(spacing,group.spacing);
            }
            if (wave.spawns != null) foreach (var group in wave.spawns)
            {
                var groupOrigin = group.spawnPoint ? group.spawnPoint : defaultSpawnPoint;
                int width = Mathf.CeilToInt(Mathf.Sqrt(totals[groupOrigin]));
                var agentTemplate = group.prefab.GetComponent<NavMeshAgent>();
                var filter = new NavMeshQueryFilter { agentTypeID = agentTemplate.agentTypeID, areaMask = agentTemplate.areaMask };
                for (int i = 0; i < group.count; i++)
                {
                    slots.TryGetValue(groupOrigin,out int slot); slots[groupOrigin] = slot+1;
                    float spacing = spacings[groupOrigin];
                    Vector3 local = new Vector3((slot % width - (width - 1) * .5f) * spacing, 0, (slot / width) * spacing);
                    var origin = group.spawnPoint ? group.spawnPoint : defaultSpawnPoint;
                    Vector3 position = origin.TransformPoint(local+group.offset);
                    if (!NavMesh.SamplePosition(position, out var nav, 2, filter))
                    {
                        ResetEncounter(); failure = $"Spawn failed: {group.prefab.name}. Check spawn points and baked NavMesh.";
                        Debug.LogError(failure, this); Refresh(); return;
                    }
                    if (!staging)
                    {
                        var holder = new GameObject("Spawn staging"); holder.transform.SetParent(transform); holder.SetActive(false); staging = holder.transform;
                    }
                    var instance = Instantiate(group.prefab, nav.position, origin.rotation, staging);
                    instance.SetActive(false);
                    if (GetComponentInParent<Rigidbody>() && !instance.GetComponent<Rigidbody>())
                    { var body = instance.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false; }
                    var target = instance.GetComponent<FpsTarget>();
                    var ai = instance.GetComponent<FpsEnemyAI>();
                    var controller = game.view ? game.view.GetComponentInParent<StarterAssets.FirstPersonController>() : null;
                    ai.player = controller ? controller.transform : game.view ? game.view.transform : null;
                    spawned.Add(target); current.Add(target);
                    instance.transform.SetParent(transform, true);
                    target.ResetTarget();
                }
            }
            if (wave.enemies != null) foreach (var enemy in wave.enemies) { current.Add(enemy); enemy.ResetTarget(); }
        }
        void Refresh()
        {
            if (!status) return;
            if (flowManaged && !running) { status.text = ""; return; }
            int alive = 0; foreach (var enemy in current) if (enemy && !enemy.IsDead) alive++;
            status.text = failure ?? (running ? (waiting ? $"Next wave in {Mathf.CeilToInt(Mathf.Max(0,nextWaveTime-Time.time))}" : $"{waves[index].title} [{index+1}/{waves.Length}] | Remaining: {alive}") : completed ? "WAVES CLEARED | V at terminal: replay" : "V at terminal: start waves");
        }
        void OnDestroy() { foreach (var enemy in spawned) if (enemy) Destroy(enemy.gameObject); }
    }
}
