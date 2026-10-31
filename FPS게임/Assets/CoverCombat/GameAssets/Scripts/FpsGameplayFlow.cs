using System.Collections;
using StarterAssets;
using UnityEngine;
namespace LectureFPS
{
    public enum FpsFlowPhase { Travel, Preparation, Combat, Result, Ending, Failed, Countdown }
    [DefaultExecutionOrder(200)]
    public sealed class FpsGameplayFlow : MonoBehaviour
    {
        public string campaignId;
        public FpsLectureGame game;
        public FpsPlayerHealth player;
        public FpsCombatArea[] areas = new FpsCombatArea[0];
        public bool startInPreparation;
        [Min(0)] public int startingCredits = 300;
        public FpsFlowUI ui;
        public UnityEngine.Events.UnityEvent onEnding, onTravel;
        public FpsFlowPhase Phase { get; private set; } = FpsFlowPhase.Travel;
        public int AreaIndex { get; private set; }
        public FpsCombatArea CurrentArea => AreaIndex >= 0 && AreaIndex < areas.Length ? areas[AreaIndex] : null;
        public bool CanBuild => Phase == FpsFlowPhase.Preparation;
        public string Message { get; private set; }
        FirstPersonController movement;
        StarterAssetsInputs inputs;
        bool initialMovement;
        Vector3 checkpoint;
        Quaternion checkpointFacing;
        bool uiOpen, initialized, retryTravel;
        float preparationEnds, countdownEnds;
        public float SecondsRemaining => Mathf.Max(0,(Phase == FpsFlowPhase.Countdown ? countdownEnds : preparationEnds)-Time.time);
        public bool UIOpen => uiOpen;
        public string ValidateSetup()
        {
            if (!game || !player || !ui || string.IsNullOrEmpty(campaignId) || areas == null || areas.Length == 0) return "Assign game, player, UI and at least one area.";
            var used = new System.Collections.Generic.HashSet<FpsWaveDirector>();
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var area in areas)
            {
                if (!area || string.IsNullOrEmpty(area.areaId) || !ids.Add(area.areaId)) return "Every area needs a unique ID.";
                if (area.UsesLegacyEncounter && !used.Add(area.encounter)) return "Legacy directors cannot be shared; convert to Encounter Definition.";
                if (area.UsesLegacyEncounter && area.waveStartPoint) area.encounter.defaultSpawnPoint = area.waveStartPoint;
                if (area.gameObject.scene != game.gameObject.scene) return area.title + ": different scene.";
                string encounterError = area.ValidateEncounter(game); if (encounterError != null) return area.title + ": " + encounterError;
                var box = area.GetComponent<BoxCollider>();
                if (!box || !box.isTrigger || !box.enabled) return area.title + ": enable the entry trigger.";
                if (area.buildGrid && (area.buildGrid.area != area || area.buildGrid.gameObject.scene != area.gameObject.scene)) return area.title + ": build grid must reference this area in the same scene.";
                if (area.buildSites != null) foreach (var site in area.buildSites) if (!site || site.gameObject.scene != area.gameObject.scene) return area.title + ": invalid build site.";
            }
            return null;
        }
        IEnumerator Start()
        {
            movement = player ? player.GetComponent<FirstPersonController>() : null;
            inputs = player ? player.GetComponent<StarterAssetsInputs>() : null;
            initialMovement = movement && movement.enabled;
            string error = ValidateSetup();
            if (error != null) { Debug.LogError(error,this); enabled = false; yield break; }
            game.FlowManaged = true;
            foreach (var area in areas) { area.ConfigureEncounter(game); area.flow = this; area.encounter.flowManaged = true; area.encounter.ResetEncounter(); }
            while (!game.IsReady) { if (!game.enabled) yield break; yield return null; }
            initialized = true;
            game.GrantFlowReward(campaignId + ":starting", startingCredits);
            checkpoint = player.transform.position; checkpointFacing = player.transform.rotation;
            if (startInPreparation) EnterPreparation(); else SetPhase(FpsFlowPhase.Travel, false);
        }
        void Update()
        {
            if (!game || !game.IsReady || !player) return;
            if (!initialized) return;
            if (CanBuild && CurrentArea.autoStartCombat && Time.time >= preparationEnds && !player.IsDead) StartCombat();
            if (Phase == FpsFlowPhase.Countdown && Time.time >= countdownEnds && !player.IsDead) SpawnCombat();
            if (Phase == FpsFlowPhase.Combat)
            {
                if (!string.IsNullOrEmpty(CurrentArea.encounter.Failure)) { Message = CurrentArea.encounter.Failure; SetPhase(FpsFlowPhase.Failed,true); }
                else if (CurrentArea.encounter.IsCompleted)
                {
                    game.GrantFlowReward(campaignId + ":clear:" + CurrentArea.areaId, CurrentArea.clearReward);
                    Message = $"{CurrentArea.title} cleared. Reward: {CurrentArea.clearReward} (once per save).";
                    SetPhase(FpsFlowPhase.Result,true); CurrentArea.onCleared?.Invoke();
                }
            }
            if (CanBuild && (!game.MenuOpen || uiOpen) && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.tabKey.wasPressedThisFrame) SetUI(!uiOpen);
            if (ui) ui.Refresh();
        }
        public void ReachArea(FpsCombatArea area)
        {
            if (initialized && Phase == FpsFlowPhase.Travel && area == CurrentArea && game.IsReady && !player.IsDead)
            { checkpoint = player.transform.position; checkpointFacing = player.transform.rotation; EnterPreparation(); }
        }
        void EnterPreparation()
        {
            preparationEnds = Time.time+Mathf.Max(0,CurrentArea.preparationSeconds);
            Message = "Install defenses and upgrade. TAB toggles this panel so you can inspect the area.";
            game.RefillAmmo();
            if (CurrentArea.buildSites != null) foreach (var site in CurrentArea.buildSites) { site.flow = this; site.RestoreDefense(); }
            if (CurrentArea.buildGrid) CurrentArea.buildGrid.RestorePlaced();
            SetPhase(FpsFlowPhase.Preparation,true); CurrentArea.onPreparation?.Invoke();
        }
        void SetPhase(FpsFlowPhase phase, bool showUI)
        {
            Phase = phase;
            game.PreparationAccess = CanBuild;
            SetUI(showUI);
        }
        public void SetBuildPlacement(bool placing)
        {
            if (!CanBuild) return;
            SetUI(!placing);
            if (placing) game.MenuOpen = true;
        }
        void SetUI(bool value)
        {
            uiOpen = value;
            bool lockPlayer = value || Phase == FpsFlowPhase.Countdown || Phase == FpsFlowPhase.Ending || Phase == FpsFlowPhase.Failed;
            game.MenuOpen = lockPlayer;
            if (movement) movement.enabled = initialMovement && !lockPlayer;
            if (inputs) { inputs.move = inputs.look = Vector2.zero; inputs.jump = inputs.sprint = false; inputs.cursorLocked = !lockPlayer; inputs.cursorInputForLook = !lockPlayer; }
            Cursor.lockState = lockPlayer ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = lockPlayer;
            if (ui) { ui.SetOpen(value); ui.Refresh(); }
        }
        public void StartCombat()
        {
            if (!CanBuild || player.IsDead) return;
            countdownEnds = Time.time + Mathf.Max(0,CurrentArea.countdownSeconds);
            Message = null; SetPhase(FpsFlowPhase.Countdown,false);
        }
        void SpawnCombat()
        {
            game.RefillAmmo(); Message = null; SetPhase(FpsFlowPhase.Combat,false);
            if (!CurrentArea.encounter.StartEncounter()) { Message = CurrentArea.encounter.Failure ?? "Invalid wave setup."; SetPhase(FpsFlowPhase.Failed,true); }
            else CurrentArea.onCombat?.Invoke();
        }
        public void Continue()
        {
            if (Phase != FpsFlowPhase.Result) return;
            CurrentArea.encounter.ResetEncounter();
            if (CurrentArea.buildGrid) CurrentArea.buildGrid.ClearPlaced();
            if (CurrentArea.buildSites != null) foreach (var site in CurrentArea.buildSites) site.ClearDefense();
            if (AreaIndex == areas.Length-1) { Message = "All areas cleared. ENDING"; SetPhase(FpsFlowPhase.Ending,true); onEnding?.Invoke(); }
            else { AreaIndex++; checkpoint = player.transform.position; checkpointFacing = player.transform.rotation; Message = "Reach " + CurrentArea.title; SetPhase(FpsFlowPhase.Travel,false); onTravel?.Invoke(); }
        }
        public void PlayerDied()
        {
            retryTravel = Phase == FpsFlowPhase.Travel;
            if (CurrentArea && CurrentArea.encounter) CurrentArea.encounter.SetEnemiesActive(false);
            Message = "Defeated. Retry from this area's preparation. Purchased defenses and upgrades are retained.";
            SetPhase(FpsFlowPhase.Failed,true);
        }
        public void RetryCurrent()
        {
            if (Phase != FpsFlowPhase.Failed) return;
            CurrentArea.encounter.ResetEncounter(); player.RestoreAt(checkpoint,checkpointFacing);
            game.RefillAmmo();
            if (retryTravel) { Message = "Reach " + CurrentArea.title; SetPhase(FpsFlowPhase.Travel,false); }
            else EnterPreparation();
        }
        void OnDisable()
        {
            if (!game) return;
            game.FlowManaged = false; game.PreparationAccess = false; game.MenuOpen = false;
            if (movement) movement.enabled = initialMovement;
        }
    }
}
