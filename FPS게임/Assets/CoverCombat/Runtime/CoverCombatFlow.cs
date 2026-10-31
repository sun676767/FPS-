using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
namespace CoverCombat
{
    public enum CoverCombatPhase { Travel, Combat, Complete, Failed }
    [DefaultExecutionOrder(100)]
    public sealed class CoverCombatFlow : MonoBehaviour
    {
        public CoverCombatPlayer player;
        public CoverCombatArea[] areas = new CoverCombatArea[0];
        public UnityEvent onComplete, onFailed;
        public CoverCombatPhase Phase { get; private set; }
        public int AreaIndex { get; private set; }
        bool initialized;
        public CoverCombatArea CurrentArea => areas != null && AreaIndex < areas.Length ? areas[AreaIndex] : null;
        public string ValidateSetup()
        {
            if (!player || !player.view || areas == null || areas.Length == 0) return "Assign player, camera and ordered areas.";
            if (!player.weapons || player.weapons.view != player.view || player.weapons.combatDriver != player) return "Bind the player's FPS weapons, camera and combat driver using Connect FPS Weapons.";
            var used = new HashSet<CoverCombatEnemy>(); var areaSet = new HashSet<CoverCombatArea>();
            foreach (var area in areas)
            {
                if (!area || !areaSet.Add(area) || !area.playerAnchor || !area.playerCover) return "Each unique area needs an anchor and player cover.";
                var box = area.GetComponent<BoxCollider>();
                if (!box || !box.enabled || !box.isTrigger) return area.name+": enable the BoxCollider trigger.";
                if (area.enemies == null || area.enemies.Length == 0) return area.name+": assign fixed enemies.";
                foreach (var enemy in area.enemies)
                {
                    if (!enemy || !used.Add(enemy)) return area.name+": enemy references must be unique across areas.";
                    string animationError = enemy.ValidateAnimation(); if (animationError != null) return enemy.name+": "+animationError;
                    if (enemy.style == CoverEnemyStyle.SidePeek && (!enemy.actor || !enemy.hiddenPoint || !enemy.exposedPoint || !enemy.assignedCover)) return enemy.name+": assign actor, cover and both peek points.";
                    if (enemy.actor && (enemy.actor == enemy.hiddenPoint || enemy.actor == enemy.exposedPoint || (enemy.hiddenPoint && enemy.hiddenPoint.IsChildOf(enemy.actor)) || (enemy.exposedPoint && enemy.exposedPoint.IsChildOf(enemy.actor)))) return enemy.name+": peek points must not move with actor.";
                }
            }
            return null;
        }
        void Start()
        {
            string error = ValidateSetup(); if (error != null) { Debug.LogError(error,this); enabled = false; return; }
            foreach (var area in areas) { area.flow = this; area.Prepare(); foreach (var enemy in area.enemies) enemy.StopCombat(); }
            initialized = true;
        }
        public void Reach(CoverCombatArea area)
        {
            if (!initialized || !enabled || Phase != CoverCombatPhase.Travel || area != CurrentArea || player.IsDead) return;
            Phase = CoverCombatPhase.Combat; player.EnterCombat(area.playerAnchor,area.playerCover);
            foreach (var enemy in area.enemies) enemy.Begin(player); area.onEntered.Invoke();
        }
        void Update()
        {
            if (!initialized) return;
            if (Phase == CoverCombatPhase.Complete || Phase == CoverCombatPhase.Failed) return;
            if (player.IsDead) { Phase = CoverCombatPhase.Failed; StopEnemies(); onFailed.Invoke(); return; }
            if (Phase != CoverCombatPhase.Combat) return;
            if (!CurrentArea.AllEnemiesDefeated) return;
            var cleared = CurrentArea; player.LeaveCombat(); AreaIndex++;
            Phase = AreaIndex == areas.Length ? CoverCombatPhase.Complete : CoverCombatPhase.Travel;
            cleared.MarkCleared(); if (Phase == CoverCombatPhase.Complete) onComplete.Invoke();
        }
        void StopEnemies() { if (areas != null) foreach (var area in areas) if (area && area.enemies != null) foreach (var enemy in area.enemies) if (enemy) enemy.StopCombat(); }
        void OnDisable() { StopEnemies(); if (player) player.LeaveCombat(); }
        void OnGUI()
        {
            if (!player) return;
            string objective = Phase == CoverCombatPhase.Combat && CurrentArea ? $"Enemies remaining: {CurrentArea.RemainingEnemies} - Defeat all enemies to proceed."
                : Phase == CoverCombatPhase.Travel && CurrentArea ? "Move to " + CurrentArea.title
                : Phase == CoverCombatPhase.Complete ? "All areas cleared." : "Defeated.";
            GUI.Label(new Rect(20,20,700,80),$"HP {player.Health:0} | {Phase} | Area {Mathf.Min(AreaIndex+1,areas.Length)}/{areas.Length}\nWASD: travel | Hold RMB: expose/zoom | LMB: fire | Esc: cursor\n{objective}");
            if (!player.weapons || !player.weapons.crosshair) GUI.Label(new Rect(Screen.width/2-5,Screen.height/2-12,30,30),"+");
            if (player.InCombat) GUI.Label(new Rect(20,105,400,30),player.Protected?"COVERED - protected":"EXPOSED - vulnerable");
        }
    }
}
