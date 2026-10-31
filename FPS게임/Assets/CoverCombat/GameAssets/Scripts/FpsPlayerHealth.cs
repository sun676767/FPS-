using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace LectureFPS
{
    public sealed class FpsPlayerHealth : MonoBehaviour
    {
        public float maxHealth = 100;
        public Text healthLabel;
        public GameObject resultPanel;
        public Button retryButton;
        public AudioClip hurtSound, deathSound;
        public float Current { get; private set; }
        public bool IsDead => Current <= 0;
        float baseMaxHealth;
        Vector3 spawn;
        Quaternion rotation;
        FirstPersonController movement;
        StarterAssetsInputs inputs;
        PlayerInput playerInput;
        bool movementWasEnabled, inputWasEnabled;
        CursorLockMode previousLock;
        bool previousVisible;
        FpsLectureGame game;
        void Awake()
        {
            baseMaxHealth = maxHealth;
            Current = maxHealth;
            spawn = transform.position; rotation = transform.rotation;
            movement = GetComponent<FirstPersonController>();
            inputs = GetComponent<StarterAssetsInputs>();
            playerInput = GetComponent<PlayerInput>();
            game = FindFirstObjectByType<FpsLectureGame>();
            if (retryButton) retryButton.onClick.AddListener(Retry);
            if (resultPanel) resultPanel.SetActive(false);
            Refresh();
        }
        void OnDestroy() { if (retryButton) retryButton.onClick.RemoveListener(Retry); }
        public void SetHealthBonus(float bonus)
        {
            float previous = maxHealth;
            maxHealth = Mathf.Max(1, baseMaxHealth + Mathf.Max(0, bonus));
            if (!IsDead) Current = Mathf.Clamp(Current + Mathf.Max(0, maxHealth - previous), 0, maxHealth);
            Refresh();
        }
        void Refresh() { if (healthLabel) healthLabel.text = $"HP {Mathf.CeilToInt(Current)} / {maxHealth}"; }
        public void TakeDamage(float damage)
        {
            if (IsDead || damage <= 0) return;
            Current = Mathf.Max(0, Current - damage); Refresh();
            var sound = IsDead ? deathSound : hurtSound;
            if (sound) AudioSource.PlayClipAtPoint(sound, transform.position);
            if (!IsDead) return;
            var flow = FindAnyObjectByType<FpsGameplayFlow>();
            if (flow && flow.isActiveAndEnabled) { flow.PlayerDied(); return; }
            var terminalUI = FindAnyObjectByType<FpsTerminalUI>();
            if (terminalUI) terminalUI.CloseForDeath();
            if (game) game.enabled = false;
            movementWasEnabled = movement && movement.enabled;
            inputWasEnabled = playerInput && playerInput.enabled;
            if (movement) movement.enabled = false;
            if (playerInput) playerInput.enabled = false;
            ClearInput();
            previousLock = Cursor.lockState; previousVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (resultPanel) resultPanel.SetActive(true);
            if (retryButton) retryButton.Select();
        }
        void ClearInput()
        {
            if (!inputs) return;
            inputs.move = inputs.look = Vector2.zero; inputs.jump = inputs.sprint = false;
        }
        public void RestoreAt(Vector3 position, Quaternion facing)
        {
            var controller = GetComponent<CharacterController>();
            bool wasEnabled = controller && controller.enabled;
            if (controller) controller.enabled = false;
            transform.SetPositionAndRotation(position, facing);
            if (controller) controller.enabled = wasEnabled;
            Current = maxHealth; ClearInput(); Refresh();
            if (game) game.enabled = true;
            if (resultPanel) resultPanel.SetActive(false);
        }
        public void Retry()
        {
            if (!IsDead) return;
            var controller = GetComponent<CharacterController>();
            bool controllerEnabled = controller && controller.enabled;
            if (controller) controller.enabled = false;
            transform.SetPositionAndRotation(spawn, rotation);
            if (controller) controller.enabled = controllerEnabled;
            Current = maxHealth; ClearInput(); Refresh();
            if (game) { game.ResetRun(); game.enabled = true; }
            if (movement) movement.enabled = movementWasEnabled;
            if (playerInput) playerInput.enabled = inputWasEnabled;
            Cursor.lockState = previousLock; Cursor.visible = previousVisible;
            if (resultPanel) resultPanel.SetActive(false);
        }
    }
}
