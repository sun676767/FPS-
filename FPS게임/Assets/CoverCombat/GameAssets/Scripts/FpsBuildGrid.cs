using System.Collections.Generic;
using UnityEngine;
namespace LectureFPS
{
    public sealed class FpsBuildGrid : MonoBehaviour
    {
        public FpsCombatArea area;
        [Min(1)] public int columns = 12, rows = 12;
        [Min(.25f)] public float cellSize = 1;
        public LayerMask blockerMask = ~0, groundMask = ~0;
        [Min(.01f)] public float groundTolerance = .3f;
        public FpsBuildDefinition[] catalog = new FpsBuildDefinition[0];
        readonly HashSet<Vector2Int> occupied = new();
        readonly List<FpsBuildSite> placed = new();
        public Vector2Int Cell(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            return new Vector2Int(Mathf.FloorToInt(local.x/cellSize),Mathf.FloorToInt(local.z/cellSize));
        }
        public Vector3 Center(Vector2Int cell,Vector2Int size) => transform.TransformPoint(new Vector3((cell.x+size.x*.5f)*cellSize,0,(cell.y+size.y*.5f)*cellSize));
        public Vector3 PlacementPosition(Vector2Int cell,Vector2Int size)
        {
            Vector3 result = Center(cell,size);
            float highest = float.NegativeInfinity;
            for (int x = cell.x; x < cell.x+size.x; x++) for (int z = cell.y; z < cell.y+size.y; z++)
            {
                Vector3 floor = Center(new Vector2Int(x,z),Vector2Int.one);
                if (Physics.Raycast(floor+Vector3.up*(groundTolerance+.05f),Vector3.down,out var hit,2*groundTolerance+.1f,groundMask,QueryTriggerInteraction.Ignore)) highest = Mathf.Max(highest,hit.point.y);
            }
            if (!float.IsNegativeInfinity(highest)) result.y = highest;
            return result;
        }
        public bool IsAvailable(FpsBuildDefinition item, Vector2Int cell,bool rotated,out string reason)
        {
            reason = "";
            if (Vector3.Distance(transform.lossyScale,Vector3.one) > .001f || Vector3.Dot(transform.up,Vector3.up) < .999f || cellSize < .25f)
            { reason = "Grid must be horizontal with scale 1 and cell size >= 0.25"; return false; }
            if (!area || !area.flow || !area.flow.CanBuild || area.flow.CurrentArea != area) { reason = "Preparation only"; return false; }
            if (!item || !item.IsValid) { reason = !item ? "No build definition" : !item.prefab ? "Build Definition: missing 3D prefab" : !item.icon ? "Build Definition: missing Sprite" : "Build Definition: invalid size, height or power"; return false; }
            var size = item.Footprint(rotated);
            if (cell.x < 0 || cell.y < 0 || cell.x+size.x > columns || cell.y+size.y > rows) { reason = "Outside build zone"; return false; }
            for (int x = cell.x; x < cell.x+size.x; x++) for (int z = cell.y; z < cell.y+size.y; z++)
            {
                if (occupied.Contains(new Vector2Int(x,z))) { reason = "Cells occupied"; return false; }
                Vector3 floor = Center(new Vector2Int(x,z),Vector2Int.one);
                if (!Physics.Raycast(floor + Vector3.up*(groundTolerance+.05f),Vector3.down,out var hit,2*groundTolerance+.1f,groundMask,QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.point.y-floor.y) > groundTolerance)
                { reason = $"Ground missing or height mismatch at cell ({x},{z}); align Grid Y to the floor"; return false; }
            }
            Vector3 center = PlacementPosition(cell,size) + Vector3.up*(item.collisionHeight*.5f+.03f);
            Vector3 half = new Vector3(size.x*cellSize*.5f-.02f,item.collisionHeight*.5f-.02f,size.y*cellSize*.5f-.02f);
            var blockers = Physics.OverlapBox(center,half,transform.rotation,blockerMask,QueryTriggerInteraction.Ignore);
            if (blockers.Length > 0) { reason = $"Blocked by '{blockers[0].name}' ({LayerMask.LayerToName(blockers[0].gameObject.layer)})"; return false; }
            if (area.flow.game.Credits < item.cost) { reason = "Not enough credits"; return false; }
            reason = "LMB: install"; return true;
        }
        public bool TryPlace(FpsBuildDefinition item,Vector2Int cell,bool rotated)
        {
            Physics.SyncTransforms();
            if (!IsAvailable(item,cell,rotated,out var reason)) { Debug.LogWarning("Build rejected: " + reason,this); return false; }
            var size = item.Footprint(rotated);
            var root = new GameObject(item.displayName); root.transform.SetParent(transform,false);
            root.transform.SetPositionAndRotation(PlacementPosition(cell,size),transform.rotation*Quaternion.Euler(0,rotated ? 90 : 0,0));
            var site = root.AddComponent<FpsBuildSite>();
            site.kind = item.kind; site.flow = area.flow; site.visualPrefab = item.prefab;
            site.installCost = item.cost; site.basePower = item.power; site.maxLevel = 1;
            site.customColliderSize = new Vector3(item.width*cellSize,item.collisionHeight,item.depth*cellSize);
            site.visualOffset = item.visualOffset; site.visualEuler = item.visualEuler; site.visualScale = item.visualScale;
            site.Purchase();
            if (!site.Installed) { Destroy(root); return false; }
            placed.Add(site);
            for (int x = cell.x; x < cell.x+size.x; x++) for (int z = cell.y; z < cell.y+size.y; z++) occupied.Add(new Vector2Int(x,z));
            return true;
        }
        public void RestorePlaced() { foreach (var site in placed) if (site) site.RestoreDefense(); }
        public void ClearPlaced()
        {
            foreach (var site in placed) if (site) { site.ClearDefense(); site.gameObject.SetActive(false); Destroy(site.gameObject); }
            placed.Clear(); occupied.Clear();
        }
        void OnValidate()
        {
            columns = Mathf.Clamp(columns,1,100); rows = Mathf.Clamp(rows,1,100);
            cellSize = Mathf.Max(.25f,cellSize); groundTolerance = Mathf.Max(.01f,groundTolerance);
        }
        void OnDrawGizmos()
        {
            Gizmos.color = Color.green; Gizmos.matrix = transform.localToWorldMatrix;
            int c = Mathf.Clamp(columns,1,100), r = Mathf.Clamp(rows,1,100);
            for (int x = 0; x <= c; x++) Gizmos.DrawLine(new Vector3(x*cellSize,.02f,0),new Vector3(x*cellSize,.02f,r*cellSize));
            for (int z = 0; z <= r; z++) Gizmos.DrawLine(new Vector3(0,.02f,z*cellSize),new Vector3(c*cellSize,.02f,z*cellSize));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
