using UnityEngine;
using UnityEngine.Events;
namespace CoverCombat
{
    public enum CoverEnemyStyle { Stationary, SidePeek }
    public enum CoverEnemyAnimationMode { CoverShootIdle, Gunplay }
    public sealed class CoverCombatEnemy : MonoBehaviour
    {
        public CoverEnemyStyle style;
        public CoverEnemyAnimationMode animationMode;
        [Tooltip("Move the model AND its hit colliders, not the stationary root.")]
        public Transform actor;
        public Transform muzzle, hiddenPoint, exposedPoint;
        public CoverCombatCover assignedCover;
        public Animator animator;
        public string idleState = "Idle", shootState = "Shoot", hideState = "Hide", deathState = "Death";
        public string gunplayState = "Gunplay";
        [Range(0,1)] public float gunplayFireStart = .35f, gunplayFireEnd = .7f;
        [Min(0)] public float blendSeconds = .15f;
        public float maxHealth = 100, idleSeconds = 2, attackSeconds = 2, peekSpeed = 5;
        public float fireInterval = .7f, damage = 10, range = 100;
        public LayerMask hitMask = ~0;
        public UnityEvent onShot, onHit, onDeath;
        public float Health => health;
        public bool IsDead { get; private set; }
        public bool Engaged { get; private set; }
        CoverCombatPlayer player;
        float health, phaseEnds, nextShot;
        bool attacking;
        void Awake() { if (animator) animator.applyRootMotion = true; }
        public void Begin(CoverCombatPlayer target)
        {
            player = target; health = Mathf.Max(1,maxHealth); IsDead = false; Engaged = true;
            foreach (var body in GetComponentsInChildren<Collider>()) body.enabled = true;
            attacking = false; phaseEnds = Time.time+Mathf.Max(.1f,idleSeconds);
            if (actor && UsesSidePeek && hiddenPoint) actor.position = hiddenPoint.position;
            Play(HiddenState);
        }
        bool UsesSidePeek => style == CoverEnemyStyle.SidePeek && animationMode != CoverEnemyAnimationMode.Gunplay;
        bool UsesScriptedMovement => UsesSidePeek;
        string HiddenState => animationMode == CoverEnemyAnimationMode.Gunplay || UsesSidePeek ? hideState : idleState;
        void Play(string state)
        {
            if (animator && animator.runtimeAnimatorController && !string.IsNullOrEmpty(state) && animator.HasState(0,Animator.StringToHash(state))) animator.CrossFadeInFixedTime(state,blendSeconds,0,0);
        }
        void Update()
        {
            if (!Engaged || IsDead || !player || player.IsDead) return;
            bool gunplay = animationMode == CoverEnemyAnimationMode.Gunplay;
            var state = animator ? animator.GetCurrentAnimatorStateInfo(0) : default;
            // Gunplay already contains rise, fire and crouch. Let the whole clip finish.
            bool gunplayFinished = gunplay && attacking && animator && !animator.IsInTransition(0) && state.IsName(gunplayState) && state.normalizedTime >= 1;
            if ((!gunplay || !attacking) && Time.time >= phaseEnds || gunplayFinished)
            {
                attacking = !attacking; phaseEnds = Time.time+Mathf.Max(.1f,attacking?attackSeconds:idleSeconds);
                nextShot = Time.time+.25f;
                Play(attacking ? gunplay ? gunplayState : shootState : HiddenState);
            }
            Transform point = attacking?exposedPoint:hiddenPoint;
            // Animated actors move through their clip's root motion, never through MoveTowards.
            if (UsesScriptedMovement && actor && point) actor.position = Vector3.MoveTowards(actor.position,point.position,Mathf.Max(.1f,peekSpeed)*Time.deltaTime);
            if (!attacking || Time.time < nextShot || (UsesScriptedMovement && point && actor && Vector3.Distance(actor.position,point.position)>.05f)) return;
            if (gunplay && (!animator || animator.IsInTransition(0) || !state.IsName(gunplayState) || state.normalizedTime < gunplayFireStart || state.normalizedTime > gunplayFireEnd)) return;
            nextShot = Time.time+Mathf.Max(.05f,fireInterval); onShot.Invoke();
            Vector3 origin = muzzle ? muzzle.position : (actor ? actor.position : transform.position)+Vector3.up;
            Vector3 direction = player.AimPoint-origin;
            if (CoverCombatRay.First(origin,direction.normalized,Mathf.Min(direction.magnitude+.1f,range),hitMask,transform,out var hit) && hit.collider.GetComponentInParent<CoverCombatPlayer>() == player) player.TakeDamage(damage);
        }
        public void TakeDamage(float amount)
        {
            if (!Engaged || IsDead || amount<=0) return;
            health -= amount;
            health = Mathf.Max(0,health); onHit.Invoke();
            if (health>0) return;
            IsDead = true; Engaged = false; Play(deathState);
            foreach (var body in GetComponentsInChildren<Collider>()) body.enabled = false;
            onDeath.Invoke();
        }
        public void StopCombat() { Engaged = false; attacking = false; if (!IsDead) Play(HiddenState); }
        public string ValidateAnimation()
        {
            if (animationMode != CoverEnemyAnimationMode.Gunplay) return null;
            if (!animator || !animator.runtimeAnimatorController) return "Gunplay requires an Animator controller.";
            foreach (string state in new[]{hideState,gunplayState,deathState})
                if (string.IsNullOrEmpty(state) || !animator.HasState(0,Animator.StringToHash(state))) return "Missing animation state: "+state;
            if (gunplayFireEnd <= gunplayFireStart) return "Gunplay fire end must be after fire start.";
            return null;
        }
    }
}
