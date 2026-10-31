using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using LectureFPS;

namespace CoverCombat
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class CoverCombatPlayer : MonoBehaviour, IFpsCombatDriver
    {
        public Camera view;
        public FpsLectureGame weapons;
        [Tooltip("Disable other movement scripts. Shooting is handled by FpsLectureGame.")]
        public float moveSpeed = 5, sensitivity = .12f, zoomFov = 40, zoomSpeed = 12;
        public float maxHealth = 100;
        public UnityEvent onShot, onHurt, onDeath;
        public bool InCombat { get; private set; }
        public bool Exposed { get; private set; }
        public bool IsDead => Health <= 0;
        public float Health { get; private set; }
        public bool Protected => InCombat && !Exposed;
        public bool CanFire => isActiveAndEnabled && Exposed && !IsDead && Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
        public bool ApplyHit(RaycastHit hit, float damage)
        {
            var enemy = hit.collider.GetComponentInParent<CoverCombatEnemy>();
            if (!enemy || !enemy.Engaged || enemy.IsDead) return false;
            enemy.TakeDamage(damage); return true;
        }
        public void ShotFired() => onShot.Invoke();
        public Vector3 AimPoint => view.transform.position;
        CharacterController controller;
        CoverCombatCover cover;
        float normalFov, pitch, vertical;
        CursorLockMode oldLock;
        bool oldVisible;
        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (!view) view = GetComponentInChildren<Camera>();
            if (!view) { Debug.LogError("Assign a player camera.", this); enabled = false; return; }
            normalFov = view.fieldOfView; Health = Mathf.Max(1, maxHealth);
            if (weapons) { weapons.combatDriver = this; weapons.view = view; weapons.FlowManaged = true; }
        }
        void OnEnable() { oldLock = Cursor.lockState; oldVisible = Cursor.visible; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        void OnDisable() { LeaveCombat(); if (view) view.fieldOfView = normalFov; Cursor.lockState = oldLock; Cursor.visible = oldVisible; }
        void Update()
        {
            var mouse = Mouse.current; var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !IsDead) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            bool focused = Application.isFocused && Cursor.lockState == CursorLockMode.Locked && !IsDead;
            Exposed = InCombat && focused && mouse != null && mouse.rightButton.isPressed;
            if (cover) cover.SetHidden(Exposed);
            view.fieldOfView = Mathf.Lerp(view.fieldOfView, Exposed ? zoomFov : normalFov, 1-Mathf.Exp(-zoomSpeed*Time.deltaTime));
            if (!focused) return;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue()*sensitivity;
                transform.Rotate(0, delta.x, 0); pitch = Mathf.Clamp(pitch-delta.y,-75,75);
                view.transform.localRotation = Quaternion.Euler(pitch,0,0);
            }
            if (!InCombat && keyboard != null && controller.enabled)
            {
                Vector2 axis = new Vector2((keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0),(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0));
                if (controller.isGrounded && vertical < 0) vertical = -2;
                vertical += Physics.gravity.y*Time.deltaTime;
                Vector3 move = transform.TransformDirection(new Vector3(axis.x,0,axis.y).normalized)*moveSpeed;
                controller.Move((move+Vector3.up*vertical)*Time.deltaTime);
            }
        }
        public void EnterCombat(Transform anchor, CoverCombatCover barrier)
        {
            LeaveCombat(); bool wasEnabled = controller.enabled; controller.enabled = false;
            transform.SetPositionAndRotation(anchor.position,anchor.rotation); controller.enabled = wasEnabled;
            pitch = 0; vertical = 0; InCombat = true; cover = barrier;
        }
        public void LeaveCombat() { if (cover) cover.SetHidden(false); cover = null; InCombat = Exposed = false; }
        public void TakeDamage(float amount)
        {
            if (IsDead || Protected || amount <= 0) return;
            Health = Mathf.Max(0,Health-amount); onHurt.Invoke();
            if (IsDead) { Exposed = false; if (cover) cover.SetHidden(false); onDeath.Invoke(); }
        }
    }
    public static class CoverCombatRay
    {
        // Ignore only the shooter's hierarchy. All other solid geometry blocks bullets.
        public static bool First(Vector3 origin, Vector3 direction, float range, int mask, Transform shooter, out RaycastHit first)
        {
            first = default; float nearest = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(origin,direction,range,mask,QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(shooter) && hit.distance < nearest) { nearest = hit.distance; first = hit; }
            return nearest < float.PositiveInfinity;
        }
    }
}
