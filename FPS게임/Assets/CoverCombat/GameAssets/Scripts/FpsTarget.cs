using UnityEngine;
namespace LectureFPS
{
    public sealed class FpsTarget : MonoBehaviour
    {
        public float maxHealth = 100;
        float health;
        bool dead;
        public bool IsDead => dead;
        void Awake() => ResetTarget();
        public void ResetTarget()
        {
            health = maxHealth; dead = false; gameObject.SetActive(true);
            var body = GetComponent<Collider>();
            if (body) body.enabled = true;
            var enemy = GetComponent<FpsEnemyAI>();
            if (enemy) { enemy.enabled = true; enemy.ResetEnemy(); }
        }
        public void Hit(float damage, FpsLectureGame game)
        {
            if (dead) return;
            health -= damage;
            var enemy = GetComponent<FpsEnemyAI>();
            if (health > 0) { if (enemy) enemy.OnHit(); return; }
            dead = true;
            game.TargetKilled(this);
            if (enemy) enemy.Die(); else gameObject.SetActive(false);
        }
    }
}
