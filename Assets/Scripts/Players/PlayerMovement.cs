// PlayerMovement.cs — ShuttleLab (Unity 6, URP)
// 360-degree court movement with acceleration/inertia: no teleporting, no
// instant direction changes. Runs in FixedUpdate. Clamped to the singles
// court (CourtDimensions). Facing turns toward the movement direction, or
// toward the shuttle when standing still, or toward the net otherwise.
// This component only moves where it is told to go (input / MoveTowards):
// it never picks a target by itself.

using UnityEngine;
using ShuttleLab.Core;

namespace ShuttleLab.Players
{
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Tuning")]
        public float Acceleration = 14f;      // m/s^2 toward the desired velocity
        public float Deceleration = 12f;      // m/s^2 when no input (inertia)
        public float TurnSpeed = 10f;         // rad/s facing turn rate
        public float JumpVelocity = 6f;       // m/s initial vertical speed
        public float Gravity = 20f;           // m/s^2 (snappy arcade feel)
        public float LungeMultiplier = 1.6f;  // speed burst while lunging
        public float LungeDuration = 0.25f;   // seconds
        public float LungeCooldown = 0.8f;    // seconds

        /// <summary>
        /// External speed multiplier (set by PlayerController from
        /// PlayerStamina.SpeedFactor). 1 = full speed.
        /// </summary>
        public float SpeedFactor = 1f;

        /// <summary>-1 = near side, faces +z (toward the net). +1 = far side, faces -z.</summary>
        public int SideSign = -1;

        /// <summary>Current shuttle position, for idle facing. Set by PlayerController.</summary>
        public Vector3 ShuttlePosition { get; set; }

        /// <summary>Whether ShuttlePosition is meaningful right now.</summary>
        public bool HasShuttlePosition { get; set; }

        public float MaxSpeed => (_stats != null ? _stats.MoveSpeed : 6f) * SpeedFactor;

        public Vector3 Velocity => _velocity;

        /// <summary>Last joystick input (world-space XZ, magnitude 0..1).</summary>
        public Vector2 MoveInput => _moveInput;

        public bool IsMoving => new Vector2(_velocity.x, _velocity.z).magnitude > 0.3f;

        public bool IsAirborne => _isAirborne;

        public bool IsLunging => _lungeTimer > 0f;

        public bool HasMoveTarget => _moveTarget.HasValue;

        private PlayerStats _stats;
        private Vector2 _moveInput;                 // world-space XZ, magnitude 0..1
        private Vector3? _moveTarget;               // AI / recovery steering target
        private float _moveTargetSpeedFactor = 1f;
        private Vector3 _velocity;                  // horizontal velocity (y unused)
        private float _height;                      // feet height above the court
        private float _verticalVelocity;
        private bool _isAirborne;
        private float _lungeTimer;
        private float _lungeCooldownTimer;
        private Vector3 _lungeDir = Vector3.forward;
        private bool _recovering;

        public void Init(PlayerStats stats, int sideSign)
        {
            _stats = stats;
            SideSign = sideSign < 0 ? -1 : 1;
        }

        /// <summary>
        /// Joystick input, world-space XZ: x = strafe, y = toward the net
        /// (+z for the near player). The input layer is responsible for
        /// mirroring for the far side if it uses screen-space input.
        /// Clears any MoveTowards target.
        /// </summary>
        public void SetMoveInput(Vector2 input)
        {
            _moveInput = Vector2.ClampMagnitude(input, 1f);
            _moveTarget = null;
            _recovering = false;
        }

        /// <summary>Steer toward a court position (used by AI / recovery).</summary>
        public void MoveTowards(Vector3 target, float speedFactor)
        {
            _moveTarget = CourtDimensions.ClampToCourt(target, doubles: false);
            _moveTargetSpeedFactor = Mathf.Clamp01(speedFactor);
            _moveInput = Vector2.zero;
        }

        /// <summary>Head back to the home (base) position at 0.8x max speed.</summary>
        public void RecoverToHome(Vector3 homePosition)
        {
            _recovering = true;
            MoveTowards(homePosition, 0.8f);
        }

        public void StopMoving()
        {
            _moveInput = Vector2.zero;
            _moveTarget = null;
            _recovering = false;
        }

        /// <summary>
        /// Short speed burst (1.6x) in the given direction for 0.25 s.
        /// Ignored while on cooldown.
        /// </summary>
        public void Lunge(Vector3 dir)
        {
            if (_lungeCooldownTimer > 0f) return;
            Vector3 d = new Vector3(dir.x, 0f, dir.z);
            if (d.sqrMagnitude < 1e-4f) return;
            _lungeDir = d.normalized;
            _lungeTimer = LungeDuration;
            _lungeCooldownTimer = LungeCooldown;
        }

        /// <summary>Full jump (e.g. jump smash). Ignored while airborne.</summary>
        public void Jump()
        {
            if (_isAirborne) return;
            _isAirborne = true;
            _verticalVelocity = JumpVelocity;
        }

        /// <summary>
        /// Split step: a tiny hop in place (called by FootworkController when
        /// the opponent has just hit). Much smaller than Jump().
        /// </summary>
        public void SplitStep()
        {
            if (_isAirborne) return;
            _isAirborne = true;
            _verticalVelocity = 1.4f;
        }

        /// <summary>Hard reposition (point reset only — never used for rally movement).</summary>
        public void Teleport(Vector3 position)
        {
            transform.position = new Vector3(position.x, 0f, position.z);
            _velocity = Vector3.zero;
            _height = 0f;
            _isAirborne = false;
            _verticalVelocity = 0f;
            _moveTarget = null;
            _moveInput = Vector2.zero;
            _lungeTimer = 0f;
        }

        public void ResetState()
        {
            _velocity = Vector3.zero;
            _height = 0f;
            _isAirborne = false;
            _verticalVelocity = 0f;
            _moveTarget = null;
            _moveInput = Vector2.zero;
            _lungeTimer = 0f;
            _lungeCooldownTimer = 0f;
            _recovering = false;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (_lungeCooldownTimer > 0f) _lungeCooldownTimer -= dt;
            if (_lungeTimer > 0f) _lungeTimer -= dt;

            // --- Desired horizontal velocity --------------------------------
            Vector3 desired = Vector3.zero;
            if (_lungeTimer > 0f)
            {
                desired = _lungeDir * MaxSpeed * LungeMultiplier;
            }
            else if (_moveTarget.HasValue)
            {
                Vector3 toTarget = _moveTarget.Value - transform.position;
                toTarget.y = 0f;
                if (toTarget.magnitude < 0.15f)
                {
                    // Arrived.
                    _moveTarget = null;
                    _recovering = false;
                }
                else
                {
                    desired = toTarget.normalized * MaxSpeed * _moveTargetSpeedFactor;
                }
            }
            else if (_moveInput.sqrMagnitude > 1e-4f)
            {
                desired = new Vector3(_moveInput.x, 0f, _moveInput.y) * MaxSpeed;
            }

            // Accelerate toward the desired velocity; decelerate with inertia
            // when there is no desired velocity. The accel limit is what makes
            // direction changes take time instead of snapping.
            float rate = desired.sqrMagnitude > 1e-4f ? Acceleration : Deceleration;
            _velocity = Vector3.MoveTowards(_velocity, desired, rate * dt);

            Vector3 pos = transform.position;
            pos += _velocity * dt;
            pos = CourtDimensions.ClampToCourt(pos, doubles: false);

            // --- Vertical (jump / split step) --------------------------------
            if (_isAirborne)
            {
                _verticalVelocity -= Gravity * dt;
                _height += _verticalVelocity * dt;
                if (_height <= 0f)
                {
                    _height = 0f;
                    _verticalVelocity = 0f;
                    _isAirborne = false;
                }
            }
            pos.y = _height;
            transform.position = pos;

            UpdateFacing(dt);
        }

        private void UpdateFacing(float dt)
        {
            Vector3 faceDir;
            Vector2 flatVel = new Vector2(_velocity.x, _velocity.z);
            if (flatVel.magnitude > 0.5f)
            {
                faceDir = new Vector3(_velocity.x, 0f, _velocity.z).normalized;
            }
            else if (HasShuttlePosition)
            {
                Vector3 toShuttle = ShuttlePosition - transform.position;
                toShuttle.y = 0f;
                faceDir = toShuttle.sqrMagnitude > 0.01f
                    ? toShuttle.normalized
                    : new Vector3(0f, 0f, -SideSign);
            }
            else
            {
                // Face the net.
                faceDir = new Vector3(0f, 0f, -SideSign);
            }

            Quaternion target = Quaternion.LookRotation(faceDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, target, Mathf.Clamp01(TurnSpeed * dt));
        }
    }
}
