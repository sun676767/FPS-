using System;
using System.Collections.Generic;
using GameLecture.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LectureFPS
{
    [Serializable] public class WeaponData
    {
        public string name;
        public float damage, interval, reload, spread;
        public int magazine, pellets;
    }
    [Serializable] public class WeaponTable : IValidate
    {
        public List<WeaponData> weapons = new();
        public bool Validate() => weapons != null && weapons.Count > 0 && weapons.TrueForAll(w =>
            w != null && !string.IsNullOrEmpty(w.name) && w.damage > 0 && w.interval > 0 && w.reload > 0 && w.magazine > 0 && w.pellets > 0);
    }
    [Serializable] public class QuestData
    {
        public string id, title, prerequisiteId;
        public string objective = "Target";
        public int requiredCount = 1, rewardCredits = 100;
    }
    [Serializable] public class QuestTable : IValidate
    {
        public List<QuestData> quests = new();
        public bool Validate()
        {
            if (quests == null || quests.Count == 0) return false;
            var ids = new HashSet<string>();
            foreach (var q in quests)
                if (q == null || string.IsNullOrWhiteSpace(q.id) || string.IsNullOrWhiteSpace(q.title) || !ids.Add(q.id) || q.requiredCount <= 0 || q.rewardCredits < 0 || (q.objective != "Target" && q.objective != "Enemy")) return false;
            foreach (var q in quests)
            {
                var visited = new HashSet<string> { q.id };
                var current = q;
                while (!string.IsNullOrEmpty(current.prerequisiteId))
                {
                    if (!visited.Add(current.prerequisiteId)) return false;
                    current = quests.Find(x => x.id == current.prerequisiteId);
                    if (current == null) return false;
                }
            }
            return true;
        }
    }
    [Serializable] public class UpgradeData
    {
        public string id, title;
        public float valuePerLevel;
        public int[] costs;
    }
    [Serializable] public class UpgradeTable : IValidate
    {
        public List<UpgradeData> upgrades = new();
        public bool Validate()
        {
            if (upgrades == null || upgrades.Count != 3) return false;
            var ids = new HashSet<string>();
            foreach (var u in upgrades)
                if (u == null || (u.id != "damage" && u.id != "reload" && u.id != "health") || !ids.Add(u.id) || string.IsNullOrWhiteSpace(u.title) || float.IsNaN(u.valuePerLevel) || float.IsInfinity(u.valuePerLevel) || u.valuePerLevel <= 0 || u.costs == null || u.costs.Length == 0 || Array.Exists(u.costs, c => c <= 0)) return false;
            return true;
        }
        public static UpgradeTable Defaults() => new UpgradeTable { upgrades = new List<UpgradeData> {
            new UpgradeData { id = "damage", title = "Damage", valuePerLevel = .1f, costs = new[] {100,200,400} },
            new UpgradeData { id = "reload", title = "Reload speed", valuePerLevel = .08f, costs = new[] {100,200,400} },
            new UpgradeData { id = "health", title = "Max health", valuePerLevel = 20, costs = new[] {100,200,400} }
        } };
    }
    [Serializable] public class Progress
    {
        public int credits, level, completed, reloadLevel, healthLevel;
        public List<string> completedQuestIds = new();
        public List<string> flowRewardIds = new();
    }

    public sealed class FpsLectureGame : MonoBehaviour
    {
        public Camera view;
        public Transform socket;
        public GameObject[] weaponModels;
        public Text hud;
        public Text crosshair;
        public AudioSource shotSource;
        public AudioClip[] shotClips;
        public AudioClip impactClip;
        public Texture2D impactTexture;
        public RawImage hitIndicator;
        float hitIndicatorUntil;
        Sprite impactSprite;
        readonly Queue<GameObject> impacts = new();
        public LayerMask hitMask = ~0;
        [Tooltip("Optional mode-specific firing permission and hit receiver (IFpsCombatDriver).")]
        public MonoBehaviour combatDriver;
        public Transform terminal;
        public FpsTarget[] targets = new FpsTarget[0];
        UpgradeTable upgradeTable;
        FpsPlayerHealth playerHealth;
        QuestTable questTable;
        QuestData currentQuest;
        readonly HashSet<int> countedTargets = new();
        WeaponTable table;
        Progress progress;
        JsonSaveStore<Progress> save;
        int selected, kills;
        int[] ammo;
        float nextShot, reloadUntil;
        bool quest, ready, reloading;
        GameObject model;
        public bool FlowManaged { get; set; }
        public bool PreparationAccess { get; set; }
        public bool IsReady => ready;
        public int Credits => progress?.credits ?? 0;
        public bool MenuOpen { get; set; }
        public bool TrySpendCredits(int cost)
        {
            if (!ready || cost < 0 || Credits < cost) return false;
            progress.credits -= cost; save.Save(progress); return true;
        }
        public void GrantFlowReward(string id, int amount)
        {
            if (!ready || string.IsNullOrEmpty(id) || amount < 0 || progress.flowRewardIds.Contains(id)) return;
            progress.flowRewardIds.Add(id); progress.credits += amount; save.Save(progress);
        }
        public void RefillAmmo()
        {
            reloading = false; nextShot = Time.time + .2f;
            if (ammo != null) for (int i = 0; i < ammo.Length; i++) ammo[i] = table.weapons[i].magazine;
        }
        public bool NearTerminal => ready && view && terminal && Vector3.Distance(view.transform.position, terminal.position) < 4;
        public bool QuestActive => quest;
        public bool CanClaim => quest && currentQuest != null && kills >= currentQuest.requiredCount;
        public bool CanAccept => ready && !quest && NextQuest() != null;
        QuestData NextQuest() => questTable?.quests.Find(q => !progress.completedQuestIds.Contains(q.id) && (string.IsNullOrEmpty(q.prerequisiteId) || progress.completedQuestIds.Contains(q.prerequisiteId)));
        public string MissionSummary => currentQuest != null && quest ? $"{currentQuest.title}: {kills}/{currentQuest.requiredCount}" : NextQuest()?.title ?? "All missions completed";
        public bool CanUpgrade => CanBuyUpgrade("damage");
        public int UpgradeCost => CostFor("damage");
        public string TerminalStatus
        {
            get
            {
                if (!ready) return "Loading";
                var q = quest ? currentQuest : NextQuest();
                return $"Credits: {progress.credits} | Damage Lv.{progress.level}/3\n" + (q == null ? "All missions completed" : $"{q.title}\n{q.objective}: {(quest ? kills : 0)}/{q.requiredCount} | Reward: {q.rewardCredits}");
            }
        }
        public void UseTerminal()
        {
            if (FlowManaged || !NearTerminal) return;
            if (CanAccept)
            {
                currentQuest = NextQuest();
                var candidates = FindObjectsByType<FpsTarget>(FindObjectsSortMode.None);
                int available = 0;
                foreach (var t in candidates) if (Matches(t)) available++;
                if (available < currentQuest.requiredCount)
                {
                    Debug.LogWarning($"Mission {currentQuest.id} needs {currentQuest.requiredCount} {currentQuest.objective} objects; scene has {available}.", this);
                    currentQuest = null; return;
                }
                quest = true; kills = 0; countedTargets.Clear();
                foreach (var t in candidates) if (Matches(t)) t.ResetTarget();
            }
            else if (CanClaim)
            {
                // Complete before paying so repeated button presses cannot claim twice.
                if (!progress.completedQuestIds.Contains(currentQuest.id))
                {
                    progress.completedQuestIds.Add(currentQuest.id);
                    progress.credits += currentQuest.rewardCredits; progress.completed++;
                    save.Save(progress);
                }
                quest = false; currentQuest = null;
            }
        }
        bool IsWaveTarget(FpsTarget target)
        {
            foreach (var director in FindObjectsByType<FpsWaveDirector>(FindObjectsSortMode.None)) if (director.Owns(target)) return true;
            return false;
        }
        bool Matches(FpsTarget target) => target && !IsWaveTarget(target) && currentQuest != null &&
            (currentQuest.objective == "Enemy" ? target.GetComponent<FpsEnemyAI>() != null : Array.IndexOf(targets, target) >= 0 && target.GetComponent<FpsEnemyAI>() == null);
        UpgradeData Definition(string id) => upgradeTable?.upgrades.Find(u => u.id == id);
        public int LevelFor(string id) => progress == null ? 0 : id == "damage" ? progress.level : id == "reload" ? progress.reloadLevel : id == "health" ? progress.healthLevel : 0;
        public int CostFor(string id)
        {
            var u = Definition(id); int level = LevelFor(id);
            return u != null && level < u.costs.Length ? u.costs[level] : 0;
        }
        public bool CanBuyUpgrade(string id) => ready && Definition(id) != null && CostFor(id) > 0 && progress.credits >= CostFor(id) && (id != "health" || playerHealth);
        public string UpgradeLabel(string id)
        {
            var u = Definition(id);
            return u == null ? "Loading" : $"{u.title} Lv.{LevelFor(id)}/{u.costs.Length} " + (CostFor(id) == 0 ? "MAX" : $"({CostFor(id)} credits)");
        }
        public void BuyUpgrade() => BuyUpgrade("damage");
        public void BuyUpgrade(string id)
        {
            if ((FlowManaged ? !PreparationAccess : !NearTerminal) || !CanBuyUpgrade(id)) return;
            progress.credits -= CostFor(id);
            if (id == "damage") progress.level++;
            else if (id == "reload") progress.reloadLevel++;
            else if (id == "health") progress.healthLevel++;
            ApplyHealthUpgrade(); save.Save(progress);
        }
        void ApplyHealthUpgrade()
        {
            if (playerHealth) playerHealth.SetHealthBonus(Definition("health").valuePerLevel * progress.healthLevel);
        }
        public float DamageMultiplier => 1 + (Definition("damage")?.valuePerLevel ?? .1f) * LevelFor("damage");
        public float ReloadMultiplier => Mathf.Max(.25f, 1 - (Definition("reload")?.valuePerLevel ?? .08f) * LevelFor("reload"));
        void Start()
        {
            try
            {
                ResourceManager.Instance.LoadAll();
                table = DataManager.Instance.LoadJson<WeaponTable>("FPSWeapons");
                save = new JsonSaveStore<Progress>("fps-lecture.json");
                try { progress = save.Load() ?? new Progress(); } catch { progress = new Progress(); }
                upgradeTable = ResourceManager.Instance.Get<TextAsset>("FPSUpgrades") ? DataManager.Instance.LoadJson<UpgradeTable>("FPSUpgrades") : UpgradeTable.Defaults();
                progress.credits = Mathf.Max(0, progress.credits);
                progress.level = Mathf.Clamp(progress.level, 0, Definition("damage").costs.Length);
                progress.reloadLevel = Mathf.Clamp(progress.reloadLevel, 0, Definition("reload").costs.Length);
                progress.healthLevel = Mathf.Clamp(progress.healthLevel, 0, Definition("health").costs.Length);
                playerHealth = view ? view.GetComponentInParent<FpsPlayerHealth>() : null;
                if (!playerHealth) playerHealth = FindAnyObjectByType<FpsPlayerHealth>();
                ApplyHealthUpgrade();
                progress.flowRewardIds ??= new List<string>();
                progress.completedQuestIds ??= new List<string>();
                if (ResourceManager.Instance.Get<TextAsset>("FPSQuests")) questTable = DataManager.Instance.LoadJson<QuestTable>("FPSQuests");
                else questTable = new QuestTable { quests = new List<QuestData> { new QuestData { id = "training", title = "Target training", requiredCount = Mathf.Max(1, targets.Length) } } };
                ammo = new int[table.weapons.Count];
                for (int i = 0; i < ammo.Length; i++) ammo[i] = table.weapons[i].magazine;
                Equip(0);
                ready = true;
            }
            catch (Exception e) { Debug.LogException(e, this); if (hud) hud.text = "FPS initialization failed. See Console."; }
        }
        void Equip(int index)
        {
            selected = index;
            reloading = false;
            if (model) Destroy(model);
            if (weaponModels != null && index < weaponModels.Length && weaponModels[index])
            {
                model = Instantiate(weaponModels[index], socket);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                foreach (var c in model.GetComponentsInChildren<Collider>()) c.enabled = false;
            }
        }
        void Update()
        {
            if (!ready || !view || MenuOpen) return;
            var k = Keyboard.current;
            var m = Mouse.current;
            var w = table.weapons[selected];
            if (reloading && Time.time >= reloadUntil) { ammo[selected] = w.magazine; reloading = false; }
            if (k != null)
            {
                if (k.digit1Key.wasPressedThisFrame) Equip(0);
                if (k.digit2Key.wasPressedThisFrame && ammo.Length > 1) Equip(1);
                if (k.digit3Key.wasPressedThisFrame && ammo.Length > 2) Equip(2);
                w = table.weapons[selected];
                if (k.rKey.wasPressedThisFrame && !reloading && ammo[selected] < w.magazine)
                { reloading = true; reloadUntil = Time.time + w.reload * ReloadMultiplier; }
                if (!FlowManaged && terminal && Vector3.Distance(view.transform.position, terminal.position) < 4)
                {
                    if (k.eKey.wasPressedThisFrame) UseTerminal();
                    if (k.uKey.wasPressedThisFrame) BuyUpgrade();
                }
            }
            var driver = combatDriver as IFpsCombatDriver;
            bool canFire = !combatDriver || (combatDriver.isActiveAndEnabled && driver != null && driver.CanFire);
            if (canFire && m != null && m.leftButton.isPressed && !reloading && ammo[selected] > 0 && Time.time >= nextShot)
            {
                if (shotSource && shotClips != null && selected < shotClips.Length && shotClips[selected]) shotSource.PlayOneShot(shotClips[selected]);
                ammo[selected]--; nextShot = Time.time + w.interval;
                driver?.ShotFired();
                for (int i = 0; i < w.pellets; i++)
                {
                    Vector2 spread = UnityEngine.Random.insideUnitCircle * Mathf.Tan(w.spread * Mathf.Deg2Rad);
                    Vector3 direction = view.transform.TransformDirection(new Vector3(spread.x, spread.y, 1)).normalized;
                    if (Physics.Raycast(view.transform.position, direction, out var hit, 100, hitMask, QueryTriggerInteraction.Ignore))
                    {
                        ShowImpact(hit);
                        if (driver != null && driver.ApplyHit(hit, w.damage * DamageMultiplier)) hitIndicatorUntil = Time.time + .15f;
                        var target = hit.collider.GetComponentInParent<FpsTarget>();
                        if (target)
                        {
                            target.Hit(w.damage * DamageMultiplier, this);
                            hitIndicatorUntil = Time.time + .15f;
                        }
                    }
                }
            }
            if (hitIndicator) hitIndicator.enabled = Time.time < hitIndicatorUntil;
            if (hud) hud.text = FlowManaged ? $"{w.name}  {ammo[selected]}/{w.magazine} {(reloading ? "RELOADING" : "")}\nCredits: {Credits} | 1/2/3 Swap | R Reload" : $"{w.name}   {ammo[selected]}/{w.magazine} {(reloading ? "RELOADING" : "")}\n1/2/3 Swap | LMB Fire | R Reload\nMission: {MissionSummary}\nCredits {progress.credits} | Damage upgrade {progress.level}/3\nAt yellow terminal: E Accept / Claim | U Upgrade ({(progress.level + 1) * 100})";
        }
        void ShowImpact(RaycastHit hit)
        {
            if (impactClip) AudioSource.PlayClipAtPoint(impactClip, hit.point, .35f);
            if (!impactTexture) return;
            if (!impactSprite) impactSprite = Sprite.Create(impactTexture, new Rect(0, 0, impactTexture.width, impactTexture.height), new Vector2(.5f,.5f), impactTexture.width * 4);
            var mark = new GameObject("Bullet Impact");
            mark.transform.SetPositionAndRotation(hit.point + hit.normal * .015f, Quaternion.LookRotation(hit.normal));
            mark.transform.SetParent(hit.collider.transform, true);
            var renderer = mark.AddComponent<SpriteRenderer>();
            renderer.sprite = impactSprite;
            // Bound the number of live impact objects during sustained fire.
            impacts.Enqueue(mark);
            while (impacts.Count > 64) { var old = impacts.Dequeue(); if (old) Destroy(old); }
            Destroy(mark, 4);
        }
        void OnDestroy()
        {
            if (impactSprite) Destroy(impactSprite);
            foreach (var mark in impacts) if (mark) Destroy(mark);
        }
        public void ResetRun()
        {
            quest = false; currentQuest = null; countedTargets.Clear(); kills = 0; reloading = false; nextShot = 0; hitIndicatorUntil = 0;
            if (hitIndicator) hitIndicator.enabled = false;
            if (ammo != null) for (int i = 0; i < ammo.Length; i++) ammo[i] = table.weapons[i].magazine;
            foreach (var target in FindObjectsByType<FpsTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (!IsWaveTarget(target)) target.ResetTarget();
            foreach (var director in FindObjectsByType<FpsWaveDirector>(FindObjectsSortMode.None)) director.ResetEncounter();
            foreach (var mark in impacts) if (mark) Destroy(mark);
            impacts.Clear();
        }
        public void TargetKilled(FpsTarget target) { if (quest && Matches(target) && countedTargets.Add(target.GetInstanceID())) kills = Mathf.Min(kills + 1, currentQuest.requiredCount); }
    }
}
