using UnityEngine;
namespace LectureFPS
{
    // Attach to the same GameObject as the model Animator, not the AI root.
    public sealed class FpsEnemyAnimationEvents : MonoBehaviour
    {
        public FpsEnemyAI owner;
        public void AttackHit() { if (owner && owner.useAnimationEvents) owner.ApplyAttackHit(); }
    }
}
