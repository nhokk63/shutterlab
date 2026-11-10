// ShotController.cs — ShuttleLab (Unity 6, URP)
// Executes a shot for a player: solves the launch velocity with
// ShotSolver (from the racket-head contact point) and launches the shuttle.
// If the requested shot is physically unreachable, falls back to the
// height-appropriate shot (Clear / Lift / Drive) and retries once; if that
// also fails, the shot does not happen (caller keeps the whiff animation).
// Shot statistics are published through OnShotExecuted (HUD / MatchManager
// subscribe); this component owns no UI.

using System;
using UnityEngine;
using ShuttleLab.Players;

namespace ShuttleLab.Gameplay
{
    public class ShotController : MonoBehaviour
    {
        /// <summary>Fired after a shot is successfully launched. Args: type, quality.</summary>
        public event Action<ShotType, ShotQuality> OnShotExecuted;

        public int SmashCount { get; private set; }
        public int ClearCount { get; private set; }
        public int DropCount { get; private set; }
        public int NetShotCount { get; private set; }
        public int ServeCount { get; private set; }
        public int TotalShots { get; private set; }

        private ShuttlecockController _shuttle;
        private PlayerController _owner;
        private RacketController _racket;

        public void Init(ShuttlecockController shuttle, PlayerController owner)
        {
            _shuttle = shuttle;
            _owner = owner;
            _racket = owner != null ? owner.Racket : null;
        }

        /// <summary>
        /// Execute a shot of the given type toward the target with the given
        /// quality. Returns false when the shot could not be launched at all
        /// (unsolvable even after the fallback). The shuttle is snapped to
        /// the racket-head contact point at the contact frame, then launched
        /// — the solver's start is RacketHead.position by contract.
        /// </summary>
        public bool ExecuteShot(ShotType type, Vector3 target, ShotQuality quality)
        {
            if (_shuttle == null || _owner == null) return false;

            Vector3 start = _racket != null
                ? _racket.ContactPoint
                : _owner.transform.position + Vector3.up * 1.5f;
            Vector3 wind = _shuttle.Wind;

            if (!ShotSolver.Solve(start, target, type, quality, wind, out Vector3 v0))
            {
                // Fallback: height-appropriate shot, same target, one retry.
                // Dùng độ cao điểm tiếp xúc (start), không phải vị trí cầu
                // lúc này (cầu chưa snap tới vợt, có thể lệch tới 1.15 m).
                ShotType fallback = ShotSolver.FallbackShot(start.y);
                if (fallback == type) return false;
                if (!ShotSolver.Solve(start, target, fallback, quality, wind, out v0))
                    return false;
                type = fallback;
            }

            _shuttle.transform.position = start;
            _shuttle.Launch(v0, _owner.PlayerIndex, ShotLibrary.IsServe(type));

            CountShot(type);
            OnShotExecuted?.Invoke(type, quality);
            return true;
        }

        private void CountShot(ShotType type)
        {
            TotalShots++;
            switch (type)
            {
                case ShotType.Smash:
                case ShotType.JumpSmash:
                case ShotType.HalfSmash:
                    SmashCount++;
                    break;
                case ShotType.Clear:
                case ShotType.Lift:
                case ShotType.Drive:
                    ClearCount++;
                    break;
                case ShotType.Drop:
                    DropCount++;
                    break;
                case ShotType.NetShot:
                case ShotType.NetKill:
                case ShotType.Push:
                    NetShotCount++;
                    break;
                case ShotType.ServeLow:
                case ShotType.ServeHigh:
                case ShotType.ServeFlick:
                    ServeCount++;
                    break;
            }
        }
    }
}
