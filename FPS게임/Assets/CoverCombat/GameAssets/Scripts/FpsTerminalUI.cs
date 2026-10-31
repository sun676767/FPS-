using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace LectureFPS
{
    public sealed class FpsTerminalUI : MonoBehaviour
    {
        public FpsLectureGame game;
        public FirstPersonController movement;
        public GameObject panel;
        public Text status, missionLabel, upgradeLabel;
        public Button mission, upgrade, close;
        public Button reloadUpgrade, healthUpgrade;
        public Text reloadLabel, healthLabel;
        bool open, wasMoving;
        CursorLockMode cursorLock;
        bool cursorVisible;
        void Awake()
        {
            panel.SetActive(false);
            mission.onClick.AddListener(UseMission);
            upgrade.onClick.AddListener(Buy);
            close.onClick.AddListener(Close);
            if (reloadUpgrade) reloadUpgrade.onClick.AddListener(BuyReload);
            if (healthUpgrade) healthUpgrade.onClick.AddListener(BuyHealth);
        }
        void OnDestroy()
        {
            if (mission) mission.onClick.RemoveListener(UseMission);
            if (upgrade) upgrade.onClick.RemoveListener(Buy);
            if (close) close.onClick.RemoveListener(Close);
            if (reloadUpgrade) reloadUpgrade.onClick.RemoveListener(BuyReload);
            if (healthUpgrade) healthUpgrade.onClick.RemoveListener(BuyHealth);
        }
        void Update()
        {
            if (game.FlowManaged) { if (open) CloseForDeath(); return; }
            var health = movement ? movement.GetComponent<FpsPlayerHealth>() : null;
            if (open && (health && health.IsDead || !game.NearTerminal)) { Close(); return; }
            if (Keyboard.current == null) return;
            if (open && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
            else if (!open && game.enabled && game.NearTerminal && Keyboard.current.tabKey.wasPressedThisFrame) Open();
            if (open) Refresh();
        }
        void Open()
        {
            open = true; game.MenuOpen = true;
            wasMoving = movement && movement.enabled;
            if (movement) movement.enabled = false;
            cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            panel.SetActive(true); Refresh(); close.Select();
        }
        public void CloseForDeath()
        {
            if (!open) return;
            if (movement) movement.enabled = wasMoving;
            Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
            open = false; game.MenuOpen = false; panel.SetActive(false);
        }
        public void Close()
        {
            if (!open) return;
            open = false; game.MenuOpen = false;
            var health = movement ? movement.GetComponent<FpsPlayerHealth>() : null;
            if (!health || !health.IsDead)
            {
                if (movement) movement.enabled = wasMoving;
                Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
            }
            panel.SetActive(false);
        }
        void OnDisable() => Close();
        void UseMission() { game.UseTerminal(); Refresh(); }
        void BuyReload() { game.BuyUpgrade("reload"); Refresh(); }
        void BuyHealth() { game.BuyUpgrade("health"); Refresh(); }
        void Buy() { game.BuyUpgrade(); Refresh(); }
        void Refresh()
        {
            status.text = game.TerminalStatus;
            missionLabel.text = game.QuestActive ? (game.CanClaim ? "Claim reward" : "Mission in progress") : (game.CanAccept ? "Accept mission" : "All missions completed");
            mission.interactable = game.CanAccept || game.CanClaim;
            upgradeLabel.text = game.UpgradeLabel("damage");
            if (reloadLabel) reloadLabel.text = game.UpgradeLabel("reload");
            if (healthLabel) healthLabel.text = game.UpgradeLabel("health");
            if (reloadUpgrade) reloadUpgrade.interactable = game.CanBuyUpgrade("reload");
            if (healthUpgrade) healthUpgrade.interactable = game.CanBuyUpgrade("health");
            upgrade.interactable = game.CanUpgrade;
        }
    }
}
