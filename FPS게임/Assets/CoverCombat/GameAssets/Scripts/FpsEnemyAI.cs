using UnityEngine;
using UnityEngine.AI;
namespace LectureFPS
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class FpsEnemyAI : MonoBehaviour
    {
        public Animator animator;
        public Transform player;
        public bool useAnimationEvents;
        [Min(0)] public float hitDelay = .35f;
        [Min(.1f)] public float attackWindow = 1.2f;
        public float detectRange = 20, attackRange = 1.8f, attackInterval = 1.5f, damage = 10;
        [Min(.1f)] public float repathInterval = .4f;
        Vector3 spawn;
        Quaternion spawnRotation;
        bool capturedSpawn;
        NavMeshAgent agent;
        float normalStoppingDistance = 1.5f;
        NavMeshPath path;
        FpsBarricade barrierTarget, strikeBarrier;
        Vector3 destination;
        float nextAttack, hitTime, attackEnd, nextRepath;
        bool dead, attacking, hitConsumed, hasDestination;
        static readonly int Speed = Animator.StringToHash("Speed"), Attack = Animator.StringToHash("Attack"), Hit = Animator.StringToHash("Hit"), Dead = Animator.StringToHash("Dead");
        void Awake() { agent = GetComponent<NavMeshAgent>(); normalStoppingDistance = agent.stoppingDistance; path = new NavMeshPath(); if (animator) animator.applyRootMotion = false; }
        void OnEnable() => ResetEnemy();
        void OnDisable() { attacking = false; hitConsumed = true; }
        public void ResetEnemy()
        {
            if (!agent) agent = GetComponent<NavMeshAgent>();
            if (path == null) path = new NavMeshPath();
            if (!capturedSpawn) { spawn = transform.position; spawnRotation = transform.rotation; capturedSpawn = true; }
            else { if (agent && agent.isOnNavMesh) agent.Warp(spawn); else transform.position = spawn; transform.rotation = spawnRotation; }
            dead = attacking = hasDestination = false; hitConsumed = true; nextAttack = nextRepath = 0; barrierTarget = strikeBarrier = null;
            if (agent && agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = false; }
            if (animator) { animator.Rebind(); animator.SetBool(Dead, false); animator.SetFloat(Speed, 0); }
        }
        bool ClearStrike(Vector3 end,Transform targetRoot)
        {
            Vector3 origin = transform.position + Vector3.up;
            foreach (var hit in Physics.RaycastAll(origin,end-origin,Vector3.Distance(origin,end),~0,QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(targetRoot)) return false;
            return true;
        }
        bool CanHitBarrier(FpsBarricade target)
        {
            if (!target || !target.IsAlive) return false;
            Vector3 contact = target.Body.ClosestPoint(transform.position + Vector3.up*.8f);
            Vector3 flat = contact - (transform.position+Vector3.up*.8f); flat.y = 0;
            return flat.magnitude <= attackRange && ClearStrike(contact,target.transform);
        }
        bool CanHitPlayer()
        {
            var health = player ? player.GetComponentInParent<FpsPlayerHealth>() : null;
            return health && !health.IsDead && Vector3.Distance(transform.position,player.position) <= attackRange && ClearStrike(player.position+Vector3.up,health.transform);
        }
        void Repath()
        {
            barrierTarget = null; hasDestination = false;
            if (Vector3.Distance(transform.position,player.position) > detectRange) return;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(player.position,out var goal,2,filter)) return;
            path.ClearCorners();
            bool calculated = agent.CalculatePath(goal.position,path);
            if (calculated && path.status == NavMeshPathStatus.PathComplete)
            { destination = goal.position; hasDestination = true; return; }
            // Search around the reachable end of the partial route, not every nearby prop.
            Vector3 frontier = path.corners.Length > 0 ? path.corners[path.corners.Length-1] : transform.position;
            float best = float.PositiveInfinity;
            var approachPath = new NavMeshPath();
            foreach (var barrier in FpsBarricade.Active)
            {
                if (!barrier || !barrier.IsAlive || !barrier.site || !barrier.site.flow || barrier.site.flow.Phase != FpsFlowPhase.Combat) continue;
                Vector3 closest = barrier.Body.ClosestPoint(frontier+Vector3.up*.5f);
                if (Vector3.Distance(new Vector3(closest.x,frontier.y,closest.z),frontier) > Mathf.Max(3,attackRange+agent.radius*2)) continue;
                Vector3 toward = transform.position-barrier.transform.position; toward.y = 0;
                if (toward.sqrMagnitude < .01f) toward = -barrier.transform.forward;
                Vector3 edge = barrier.Body.ClosestPoint(barrier.transform.position+toward.normalized*100);
                Vector3 approach = edge + toward.normalized*(agent.radius+.15f); approach.y = transform.position.y;
                if (!NavMesh.SamplePosition(approach,out var nav,1.5f,filter) || !agent.CalculatePath(nav.position,approachPath) || approachPath.status != NavMeshPathStatus.PathComplete) continue;
                float length = 0; var corners = approachPath.corners;
                for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i-1],corners[i]);
                float score = length + Vector3.Distance(edge,goal.position);
                if (score < best) { best = score; barrierTarget = barrier; destination = nav.position; hasDestination = true; }
            }
            if (!hasDestination && path.corners.Length > 1) { destination = frontier; hasDestination = true; }
        }
        void Update()
        {
            if (dead || !player || !agent.isOnNavMesh) return;
            var health = player.GetComponentInParent<FpsPlayerHealth>();
            if (health && health.IsDead) { agent.isStopped = true; return; }
            if (attacking)
            {
                if (!useAnimationEvents && Time.time >= hitTime) ApplyAttackHit();
                if (Time.time >= attackEnd) { attacking = false; hitConsumed = true; }
            }
            if (!attacking && Time.time >= nextRepath) { nextRepath = Time.time+repathInterval; Repath(); }
            bool canHit = barrierTarget ? CanHitBarrier(barrierTarget) : CanHitPlayer();
            agent.stoppingDistance = barrierTarget ? .05f : Mathf.Min(normalStoppingDistance,attackRange*.8f);
            agent.isStopped = attacking || canHit || !hasDestination;
            if (!agent.isStopped) agent.SetDestination(destination);
            if (animator) animator.SetFloat(Speed,agent.velocity.magnitude);
            if (attacking || !canHit || Time.time < nextAttack) return;
            strikeBarrier = barrierTarget;
            nextAttack = Time.time+Mathf.Max(attackInterval,attackWindow);
            attacking = true; hitConsumed = false; hitTime = Time.time+hitDelay; attackEnd = Time.time+Mathf.Max(attackWindow,hitDelay+.05f);
            Vector3 facing = (strikeBarrier ? strikeBarrier.transform.position : player.position)-transform.position; facing.y = 0;
            if (facing.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(facing);
            if (animator) animator.SetTrigger(Attack);
        }
        public void ApplyAttackHit()
        {
            if (dead || !attacking || hitConsumed) return;
            hitConsumed = true;
            if (strikeBarrier) { if (CanHitBarrier(strikeBarrier)) strikeBarrier.TakeDamage(damage); }
            else if (CanHitPlayer()) player.GetComponentInParent<FpsPlayerHealth>().TakeDamage(damage);
        }
        public void OnHit()
        {
            if (dead) return; attacking = false; hitConsumed = true;
            if (animator) { animator.ResetTrigger(Attack); animator.SetTrigger(Hit); }
        }
        public void Die()
        {
            dead = true; attacking = false; hitConsumed = true;
            if (agent && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            if (animator) { animator.ResetTrigger(Attack); animator.ResetTrigger(Hit); animator.SetFloat(Speed,0); animator.SetBool(Dead,true); }
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        }
    }
}
