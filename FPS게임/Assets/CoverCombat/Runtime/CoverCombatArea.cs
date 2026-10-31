using UnityEngine;
using UnityEngine.Events;
namespace CoverCombat
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CoverCombatArea : MonoBehaviour
    {
        public string title = "Combat Area";
        public CoverCombatFlow flow;
        public Transform playerAnchor;
        public CoverCombatCover playerCover;
        public CoverCombatEnemy[] enemies = new CoverCombatEnemy[0];
        [Tooltip("Optional gates opened after every assigned enemy dies.")]
        public GameObject[] exitGates = new GameObject[0];
        public UnityEvent onEntered, onCleared;
        public bool IsCleared { get; private set; }
        public int RemainingEnemies
        {
            get
            {
                if (enemies == null) return 0;
                int count = 0;
                // Missing or disabled enemies are not confirmed kills.
                foreach (var enemy in enemies) if (!enemy || !enemy.IsDead) count++;
                return count;
            }
        }
        public bool AllEnemiesDefeated => enemies != null && enemies.Length > 0 && RemainingEnemies == 0;
        public void Prepare()
        {
            IsCleared = false;
            foreach (var gate in exitGates) if (gate) gate.SetActive(true);
        }
        public void MarkCleared()
        {
            if (IsCleared || !AllEnemiesDefeated) return;
            IsCleared = true;
            foreach (var gate in exitGates) if (gate) gate.SetActive(false);
            onCleared.Invoke();
        }
        void Reset() { GetComponent<BoxCollider>().isTrigger = true; }
        void OnTriggerEnter(Collider other) { var player = other.GetComponentInParent<CoverCombatPlayer>(); if (flow && player == flow.player) flow.Reach(this); }
        void OnTriggerStay(Collider other) { var player = other.GetComponentInParent<CoverCombatPlayer>(); if (flow && player == flow.player) flow.Reach(this); }
        void OnDrawGizmosSelected() { var box = GetComponent<BoxCollider>(); if (!box) return; Gizmos.color = Color.yellow; Gizmos.matrix = transform.localToWorldMatrix; Gizmos.DrawWireCube(box.center,box.size); }
    }
}
