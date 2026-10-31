using UnityEngine;

namespace LectureFPS
{
    // Optional combat rules supplied by a game mode; weapon data and feedback stay in FPS.
    public interface IFpsCombatDriver
    {
        bool CanFire { get; }
        bool ApplyHit(RaycastHit hit, float damage);
        void ShotFired();
    }
}
