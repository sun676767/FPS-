using System.Collections.Generic;
using UnityEngine;
namespace LectureFPS
{
    public sealed class FpsTrap : MonoBehaviour
    {
        public FpsBuildSite site;
        float nextTick;
        readonly HashSet<FpsTarget> hit = new();
        void Update()
        {
            if (!site || !site.flow || site.flow.Phase != FpsFlowPhase.Combat || Time.time < nextTick) return;
            nextTick = Time.time + .75f; hit.Clear();
            foreach (var collider in Physics.OverlapSphere(transform.position + Vector3.up*.5f,1.5f,~0,QueryTriggerInteraction.Ignore))
            {
                var target = collider.GetComponentInParent<FpsTarget>();
                if (target && !target.IsDead && hit.Add(target) && site.flow.CurrentArea.encounter.Owns(target)) target.Hit(site.basePower * (1 + .5f*(site.Level-1)),site.flow.game);
            }
        }
    }
}
