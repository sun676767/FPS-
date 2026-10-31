using UnityEngine;
namespace LectureFPS
{
    [CreateAssetMenu(menuName = "FPS Lecture/Build Definition",fileName = "BuildDefinition")]
    public sealed class FpsBuildDefinition : ScriptableObject
    {
        public string displayName = "Barricade";
        public Sprite icon;
        public GameObject prefab;
        public FpsDefenseKind kind;
        [Min(1)] public int width = 2, depth = 1;
        [Min(.1f)] public float collisionHeight = 1.5f;
        [Min(0)] public int cost = 50;
        [Min(1)] public float power = 100;
        public Vector3 visualOffset, visualEuler;
        public Vector3 visualScale = Vector3.one;
        public bool IsValid => icon && prefab && width > 0 && depth > 0 && collisionHeight > 0 && cost >= 0 && power > 0;
        public Vector2Int Footprint(bool rotated) => rotated ? new Vector2Int(depth,width) : new Vector2Int(width,depth);
    }
}
