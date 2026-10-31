using UnityEngine;
using UnityEngine.UI;
namespace LectureFPS
{
    public sealed class FpsFlowUI : MonoBehaviour
    {
        public FpsGameplayFlow flow;
        public GameObject panel;
        public Text title, description, waypoint;
        public Button primary, damage, reload, health, build;
        public Text primaryLabel, damageLabel, reloadLabel, healthLabel, buildLabel;
        public Dropdown sites;
        FpsCombatArea shownArea;
        void Awake()
        {
            primary.onClick.AddListener(Primary);
            damage.onClick.AddListener(BuyDamage); reload.onClick.AddListener(BuyReload); health.onClick.AddListener(BuyHealth); build.onClick.AddListener(Build);
        }
        void BuyDamage() => flow.game.BuyUpgrade("damage");
        void BuyReload() => flow.game.BuyUpgrade("reload");
        void BuyHealth() => flow.game.BuyUpgrade("health");
        void Build() { var site = SelectedSite(); if (site) site.Purchase(); }
        FpsBuildSite SelectedSite() => flow.CurrentArea && flow.CurrentArea.buildSites != null && sites.value < flow.CurrentArea.buildSites.Length ? flow.CurrentArea.buildSites[sites.value] : null;
        void Primary()
        {
            if (flow.Phase == FpsFlowPhase.Preparation) flow.StartCombat();
            else if (flow.Phase == FpsFlowPhase.Result) flow.Continue();
            else if (flow.Phase == FpsFlowPhase.Failed) flow.RetryCurrent();
        }
        public void SetOpen(bool value) { if (panel) panel.SetActive(value); }
        public void Refresh()
        {
            if (!flow || !flow.game || !flow.game.IsReady) return;
            var area = flow.CurrentArea;
            if (shownArea != area)
            {
                shownArea = area; sites.ClearOptions();
                var options = new System.Collections.Generic.List<string>();
                if (area && area.buildSites != null) foreach (var site in area.buildSites) options.Add(site ? site.name : "Missing site");
                sites.AddOptions(options);
            }
            title.text = $"{flow.Phase} | {(area ? area.title : "")} | Credits {flow.game.Credits}";
            description.text = (flow.Message ?? "") + (flow.CanBuild && area && area.autoStartCombat ? $"\nCombat starts automatically in {Mathf.CeilToInt(flow.SecondsRemaining)}s" : "");
            primaryLabel.text = flow.Phase == FpsFlowPhase.Preparation ? "Start wave combat" : flow.Phase == FpsFlowPhase.Result ? (flow.AreaIndex == flow.areas.Length-1 ? "Ending" : "Continue to next objective") : flow.Phase == FpsFlowPhase.Failed ? "Retry preparation" : "Campaign complete";
            primary.interactable = flow.Phase != FpsFlowPhase.Ending;
            damage.gameObject.SetActive(flow.CanBuild); reload.gameObject.SetActive(flow.CanBuild); health.gameObject.SetActive(flow.CanBuild); sites.gameObject.SetActive(flow.CanBuild && area && !area.buildGrid); build.gameObject.SetActive(flow.CanBuild && area && !area.buildGrid);
            damageLabel.text = flow.game.UpgradeLabel("damage"); reloadLabel.text = flow.game.UpgradeLabel("reload"); healthLabel.text = flow.game.UpgradeLabel("health");
            damage.interactable = flow.CanBuild && flow.game.CanBuyUpgrade("damage"); reload.interactable = flow.CanBuild && flow.game.CanBuyUpgrade("reload"); health.interactable = flow.CanBuild && flow.game.CanBuyUpgrade("health");
            var selected = SelectedSite();
            buildLabel.text = selected ? $"{(selected.Installed ? "Upgrade" : "Install")} {selected.kind} Lv.{selected.Level}/{selected.maxLevel} | {selected.Cost} credits" : "No build sites";
            build.interactable = selected && selected.CanPurchase;
            if (waypoint) waypoint.text = flow.Phase == FpsFlowPhase.Travel && area ? $"NEXT: {area.title} | {Vector3.Distance(flow.player.transform.position,area.transform.position):0} m" : flow.Phase == FpsFlowPhase.Countdown ? $"WAVE START IN {Mathf.CeilToInt(flow.SecondsRemaining)}" : flow.CanBuild ? (area.autoStartCombat ? $"Preparation: {Mathf.CeilToInt(flow.SecondsRemaining)}s | TAB: Toggle panel" : "TAB: Toggle preparation panel / inspect build sites") : flow.Phase == FpsFlowPhase.Combat && area ? area.encounter.FlowSummary : "";
        }
        void OnDestroy()
        {
            primary.onClick.RemoveListener(Primary); damage.onClick.RemoveListener(BuyDamage); reload.onClick.RemoveListener(BuyReload); health.onClick.RemoveListener(BuyHealth); build.onClick.RemoveListener(Build);
        }
    }
}
