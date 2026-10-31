using UnityEngine;
namespace LectureFPS
{
    public enum FpsDefenseKind { Barricade, Trap }
    public sealed class FpsBuildSite : MonoBehaviour
    {
        public FpsDefenseKind kind;
        public GameObject visualPrefab;
        [Min(0)] public int installCost = 50;
        [Min(1)] public int upgradeCost = 75;
        [Min(1)] public int maxLevel = 3;
        [Min(1)] public float basePower = 100;
        public FpsGameplayFlow flow;
        public Vector3 customColliderSize;
        public Vector3 visualOffset, visualEuler;
        public Vector3 visualScale = Vector3.one;
        public int Level { get; private set; }
        GameObject defense;
        public bool Installed => Level > 0;
        public int Cost => Installed ? upgradeCost * Level : installCost;
        public bool CanPurchase => flow && flow.CanBuild && Level < maxLevel && flow.game.Credits >= Cost;
        public void Purchase()
        {
            if (!CanPurchase) return;
            if (!flow.game.TrySpendCredits(Cost)) return;
            Level++;
            if (!defense)
            {
                defense = new GameObject(kind.ToString()); defense.transform.SetParent(transform,false);
                GameObject visual = visualPrefab ? Instantiate(visualPrefab,defense.transform) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (!visualPrefab) { visual.transform.SetParent(defense.transform,false); visual.transform.localScale = kind == FpsDefenseKind.Barricade ? new Vector3(3,1.5f,.5f) : new Vector3(2,.15f,2); visual.transform.localPosition = kind == FpsDefenseKind.Barricade ? Vector3.up*.75f : Vector3.up*.075f; }
                if (visualPrefab)
                {
                    visual.transform.localPosition = visualOffset; visual.transform.localEulerAngles = visualEuler;
                    visual.transform.localScale = visualScale;
                }
                foreach (var c in visual.GetComponentsInChildren<Collider>()) c.enabled = false;
                foreach (var behavior in visual.GetComponentsInChildren<MonoBehaviour>()) behavior.enabled = false;
                if (kind == FpsDefenseKind.Barricade)
                {
                    var rb = defense.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
                    var body = defense.AddComponent<BoxCollider>(); body.size = customColliderSize.sqrMagnitude > 0 ? customColliderSize : new Vector3(3,1.5f,.5f); body.center = Vector3.up*(body.size.y*.5f);
                    var barrier = defense.AddComponent<FpsBarricade>(); barrier.site = this;
                }
                else { var trap = defense.AddComponent<FpsTrap>(); trap.site = this; }
            }
            RestoreDefense();
        }
        public void RestoreDefense()
        {
            if (!defense) return;
            defense.SetActive(true);
            var barrier = defense.GetComponent<FpsBarricade>(); if (barrier) barrier.Restore(basePower * (1 + .5f*(Level-1)));
        }
        public void ClearDefense() { if (defense) { defense.SetActive(false); Destroy(defense); } defense = null; Level = 0; }
        void OnDrawGizmos()
        {
            Gizmos.color = kind == FpsDefenseKind.Barricade ? Color.yellow : Color.magenta;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(kind == FpsDefenseKind.Barricade ? Vector3.up*.75f : Vector3.up*.1f, kind == FpsDefenseKind.Barricade ? new Vector3(3,1.5f,.5f) : new Vector3(2,.2f,2));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
