// FootworkController.cs — ShuttleLab (Unity 6, URP)
// Plain class (not a MonoBehaviour) owned by PlayerController. Handles the
// three footwork behaviours from the architecture doc:
//   - split step: a tiny hop in place the moment the opponent hits
//     (0.12 s window, gives the player a loaded, ready stance);
//   - recovery step: walk back to HomePosition at 0.8x max speed after
//     the player has hit;
//   - adjustment steps: small, slower steps when close to the pickup point.
// Drives PlayerMovement; it never moves the transform itself.

using UnityEngine;

namespace ShuttleLab.Players
{
    public class FootworkController
    {
        /// <summary>How long the split-step "loaded" window lasts.</summary>
        public const float SplitStepDuration = 0.12f;

        /// <summary>Recovery speed as a fraction of max speed.</summary>
        public const float RecoverySpeedFactor = 0.8f;

        /// <summary>Within this distance of the target, use small adjustment steps.</summary>
        public const float AdjustmentStepRange = 0.6f;

        /// <summary>Speed factor while taking adjustment steps.</summary>
        public const float AdjustmentSpeedFactorValue = 0.45f;

        private readonly PlayerMovement _movement;
        private Vector3 _homePosition;
        private float _splitTimer;
        private bool _recovering;

        public bool IsSplitStepping => _splitTimer > 0f;

        public bool IsRecovering => _recovering;

        public FootworkController(PlayerMovement movement, Vector3 homePosition)
        {
            _movement = movement;
            _homePosition = homePosition;
        }

        public void SetHomePosition(Vector3 homePosition)
        {
            _homePosition = homePosition;
        }

        /// <summary>
        /// Called the moment the opponent hits the shuttle: split step
        /// (small hop in place) so the player is loaded and ready to move.
        /// </summary>
        public void OnOpponentHit()
        {
            _splitTimer = SplitStepDuration;
            _movement?.SplitStep();
        }

        /// <summary>Start walking back to the home (base) position after a hit.</summary>
        public void StartRecovery()
        {
            if (_movement == null) return;
            _recovering = true;
            _movement.RecoverToHome(_homePosition);
        }

        /// <summary>
        /// Speed factor for approaching a target: full speed far away, small
        /// adjustment steps (0.45x) when within AdjustmentStepRange so the
        /// player does not overshoot the pickup point.
        /// </summary>
        public float AdjustmentSpeedFactor(Vector3 target)
        {
            if (_movement == null) return 1f;
            Vector3 toTarget = target - _movement.transform.position;
            toTarget.y = 0f;
            return toTarget.magnitude < AdjustmentStepRange
                ? AdjustmentSpeedFactorValue
                : 1f;
        }

        /// <summary>Called every frame by PlayerController.Update.</summary>
        public void Update(float dt)
        {
            if (_splitTimer > 0f) _splitTimer -= dt;

            if (_recovering && _movement != null)
            {
                Vector3 toHome = _homePosition - _movement.transform.position;
                toHome.y = 0f;
                if (toHome.magnitude < 0.15f || !_movement.HasMoveTarget)
                {
                    _recovering = false;
                    _movement.StopMoving();
                }
            }
        }
    }
}
