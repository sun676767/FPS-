using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace LectureFPS
{
    [RequireComponent(typeof(BoxCollider),typeof(NavMeshObstacle))]
    public sealed class FpsBarricade : MonoBehaviour
    {
        public static readonly HashSet<FpsBarricade> Active = new();
        public FpsBuildSite site;
        [SerializeField] float health, maxHealth;
        public float Health => health;
        public float MaxHealth => maxHealth;
        public bool IsAlive => isActiveAndEnabled && Health > 0;
        public Collider Body => GetComponent<Collider>();
        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);
        public void Restore(float amount)
        {
            health = maxHealth = Mathf.Max(1,amount);
            var body = GetComponent<BoxCollider>(); body.enabled = true;
            var obstacle = GetComponent<NavMeshObstacle>(); if (!obstacle) obstacle = gameObject.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = body.center; obstacle.size = body.size;
            obstacle.carving = true; obstacle.carveOnlyStationary = false; obstacle.enabled = true;
        }
        public void TakeDamage(float amount)
        {
            if (!site || !site.flow || site.flow.Phase != FpsFlowPhase.Combat || !IsAlive || amount <= 0) return;
            health = Mathf.Max(0,Health-amount);
            if (Health <= 0)
            {
                GetComponent<NavMeshObstacle>().enabled = false; Body.enabled = false;
                gameObject.SetActive(false);
            }
        }
    }
}
