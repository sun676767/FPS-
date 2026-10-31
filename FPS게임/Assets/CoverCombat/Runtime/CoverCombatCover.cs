using UnityEngine;
namespace CoverCombat
{
    public sealed class CoverCombatCover : MonoBehaviour
    {
        [Tooltip("Only these renderers disappear while aiming. Colliders remain solid.")]
        public Renderer[] visuals = new Renderer[0];
        bool[] original;
        bool hidden;
        public void SetHidden(bool value)
        {
            if (hidden == value) return;
            if (value)
            {
                original = new bool[visuals.Length];
                for (int i=0;i<visuals.Length;i++) if (visuals[i]) { original[i] = visuals[i].enabled; visuals[i].enabled = false; }
            }
            else if (original != null)
                for (int i=0;i<visuals.Length && i<original.Length;i++) if (visuals[i]) visuals[i].enabled = original[i];
            hidden = value;
        }
        void OnDisable() => SetHidden(false);
    }
}
