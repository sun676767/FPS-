using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace LectureFPS
{
    [DefaultExecutionOrder(300)]
    public sealed class FpsGridBuilder : MonoBehaviour
    {
        public FpsGameplayFlow flow;
        public GameObject palette;
        public Transform buttonsRoot;
        public Button itemTemplate;
        public Text hint;
        FpsBuildGrid shownGrid, activeGrid;
        FpsBuildDefinition selected;
        readonly List<GameObject> buttons = new();
        GameObject preview, footprint, gridLines;
        Material previewMaterial, gridMaterial;
        Mesh gridMesh;
        bool rotated;
        string selectionError;
        public bool IsPlacing => selected;
        void Update()
        {
            var grid = flow && flow.CurrentArea ? flow.CurrentArea.buildGrid : null;
            if (grid != shownGrid) { Cancel(); shownGrid = grid; RebuildCatalog(); }
            bool preparing = flow && flow.CanBuild && flow.game.IsReady;
            palette.SetActive(preparing && flow.UIOpen && grid);
            if (!selected) { hint.text = preparing && grid ? selectionError ?? "Select a build icon in the preparation panel" : ""; return; }
            if (!preparing || grid != activeGrid) { Cancel(); return; }
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame) || mouse != null && mouse.rightButton.wasPressedThisFrame) { Cancel(); return; }
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) rotated = !rotated;
            var ray = flow.game.view.ViewportPointToRay(new Vector3(.5f,.5f,0));
            var plane = new Plane(Vector3.up,activeGrid.transform.position);
            if (!plane.Raycast(ray,out float distance) || distance > 30)
            { preview.SetActive(false); footprint.SetActive(false); hint.text = "Aim at the build zone ground (within 30m). RMB/Esc: cancel"; return; }
            Vector3 aim = ray.GetPoint(distance);
            if (Physics.Raycast(ray,out var obstruction,distance,~0,QueryTriggerInteraction.Ignore))
            {
                bool isGround = (activeGrid.groundMask.value & (1 << obstruction.collider.gameObject.layer)) != 0 &&
                    Vector3.Dot(obstruction.normal,Vector3.up) > .85f && Mathf.Abs(obstruction.point.y-activeGrid.transform.position.y) <= activeGrid.groundTolerance;
                if (isGround) aim = obstruction.point;
                else if (obstruction.distance < distance-.1f)
                { preview.SetActive(false); footprint.SetActive(false); hint.text = $"Aim blocked by '{obstruction.collider.name}'. RMB/Esc: cancel"; return; }
            }
            Vector2Int cell = activeGrid.Cell(aim);
            var size = selected.Footprint(rotated);
            Vector3 position = activeGrid.PlacementPosition(cell,size);
            bool valid = activeGrid.IsAvailable(selected,cell,rotated,out string reason);
            preview.SetActive(true); footprint.SetActive(true);
            preview.transform.SetPositionAndRotation(position,activeGrid.transform.rotation*Quaternion.Euler(0,rotated ? 90 : 0,0));
            footprint.transform.SetPositionAndRotation(position+Vector3.up*.025f,activeGrid.transform.rotation);
            footprint.transform.localScale = new Vector3(size.x*activeGrid.cellSize,.04f,size.y*activeGrid.cellSize);
            SetColor(previewMaterial,valid ? new Color(.15f,.8f,.3f) : new Color(.9f,.15f,.15f));
            hint.text = $"{selected.displayName} {size.x}x{size.y} | {selected.cost} credits | {reason}\nR: Rotate | RMB / Esc / Tab: return to preparation";
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && valid && !(EventSystem.current && EventSystem.current.IsPointerOverGameObject())) activeGrid.TryPlace(selected,cell,rotated);
        }
        void RebuildCatalog()
        {
            foreach (var button in buttons) if (button) Destroy(button); buttons.Clear();
            if (!shownGrid) return;
            foreach (var item in shownGrid.catalog)
            {
                if (!item) continue;
                var definition = item;
                var button = Instantiate(itemTemplate,buttonsRoot); button.gameObject.SetActive(true);
                var icon = button.transform.Find("Icon").GetComponent<Image>(); icon.sprite = definition.icon; icon.enabled = definition.icon;
                button.GetComponentInChildren<Text>().text = $"{definition.displayName}\n{definition.width}x{definition.depth} | {definition.cost}";
                button.onClick.AddListener(() => Select(definition)); buttons.Add(button.gameObject);
            }
        }
        public void Select(FpsBuildDefinition item)
        {
            if (!flow || !flow.CanBuild || !shownGrid) return;
            if (!item || !item.IsValid)
            {
                selectionError = !item ? "No Build Definition" : !item.prefab ? item.name + ": assign 3D Prefab" : !item.icon ? item.name + ": assign Sprite icon" : item.name + ": invalid dimensions, height or power";
                Debug.LogWarning(selectionError,item); return;
            }
            selectionError = null;
            Cancel(); selected = item; activeGrid = shownGrid; rotated = false;
            flow.SetBuildPlacement(true);
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            previewMaterial = new Material(shader); gridMaterial = new Material(shader); SetColor(gridMaterial,new Color(.1f,.7f,.5f));
            preview = new GameObject("Build Preview"); preview.SetActive(false);
            var visual = Instantiate(item.prefab,preview.transform);
            visual.transform.localPosition = item.visualOffset; visual.transform.localEulerAngles = item.visualEuler; visual.transform.localScale = item.visualScale;
            foreach (var behavior in visual.GetComponentsInChildren<Behaviour>(true)) behavior.enabled = false;
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.detectCollisions = false; }
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            { var materials = renderer.sharedMaterials; for (int i = 0; i < materials.Length; i++) materials[i] = previewMaterial; renderer.sharedMaterials = materials; }
            footprint = GameObject.CreatePrimitive(PrimitiveType.Cube); footprint.name = "Build Footprint";
            footprint.GetComponent<Collider>().enabled = false; footprint.GetComponent<Renderer>().sharedMaterial = previewMaterial;
            DrawGrid();
        }
        void DrawGrid()
        {
            gridLines = new GameObject("Build Grid"); gridLines.transform.SetPositionAndRotation(activeGrid.transform.position,activeGrid.transform.rotation);
            var vertices = new List<Vector3>();
            for (int x = 0; x <= activeGrid.columns; x++) { vertices.Add(new Vector3(x*activeGrid.cellSize,.015f,0)); vertices.Add(new Vector3(x*activeGrid.cellSize,.015f,activeGrid.rows*activeGrid.cellSize)); }
            for (int z = 0; z <= activeGrid.rows; z++) { vertices.Add(new Vector3(0,.015f,z*activeGrid.cellSize)); vertices.Add(new Vector3(activeGrid.columns*activeGrid.cellSize,.015f,z*activeGrid.cellSize)); }
            var indices = new int[vertices.Count]; for (int i = 0; i < indices.Length; i++) indices[i] = i;
            gridMesh = new Mesh { name = "Build Grid Lines" }; gridMesh.SetVertices(vertices); gridMesh.SetIndices(indices,MeshTopology.Lines,0);
            gridLines.AddComponent<MeshFilter>().sharedMesh = gridMesh; gridLines.AddComponent<MeshRenderer>().sharedMaterial = gridMaterial;
        }
        static void SetColor(Material material,Color color) { material.color = color; if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",color); }
        public void Cancel()
        {
            bool wasPlacing = selected;
            selected = null; activeGrid = null;
            if (preview) Destroy(preview); if (footprint) Destroy(footprint); if (gridLines) Destroy(gridLines);
            if (previewMaterial) Destroy(previewMaterial); if (gridMaterial) Destroy(gridMaterial); if (gridMesh) Destroy(gridMesh);
            if (wasPlacing && flow && flow.CanBuild) flow.SetBuildPlacement(false);
        }
        void OnDisable() => Cancel();
    }
}
