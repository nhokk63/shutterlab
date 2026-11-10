// PlayerController.cs — ShuttleLab (Unity 6, URP)
// Orchestrator for one player: owns the humanoid (HumanoidBuilder), the
// movement / footwork / stamina / animation / racket / shot components and
// wires them together in Init. Other modules (OpponentAI, MatchManager,
// MobileInputManager) drive this controller only through its public API.
// Update does NOT decide anything for the AI: the AI calls MoveTowards /
// TryHit itself. User input arrives via SetMoveInput (called by the input
// layer); PlayerMovement consumes it in FixedUpdate.
// Shot quality is computed from contact timing + position + stamina — never
// from a bare random roll (ARCHITECTURE.md).

using UnityEngine;
using ShuttleLab.Gameplay;

namespace ShuttleLab.Players
{
    public class PlayerController : MonoBehaviour
    {
        /// <summary>0 = near side (user), 1 = far side (AI).</summary>
        public int PlayerIndex { get; set; }

        public bool IsUserControlled { get; set; }

        public PlayerStats Stats;
        public PlayerStamina Stamina;
        public PlayerMovement Movement;
        public PlayerAnimationController Anim;
        public RacketController Racket;
        public ShotController ShotCtrl;

        public HumanoidRig Rig { get; private set; }
        public FootworkController Footwork { get; private set; }
        public ShuttlecockController Shuttle { get; private set; }

        private Vector3 _homePosition;
        public Vector3 HomePosition
        {
            get => _homePosition;
            set
            {
                _homePosition = value;
                Footwork?.SetHomePosition(value);
            }
        }

        /// <summary>Reach radius (m) from the racket head for CanHitShuttle.</summary>
        public float ReachRadius = 1.15f;

        /// <summary>True while a swing animation is playing.</summary>
        public bool IsSwinging { get; private set; }

        /// <summary>Shuttle heights this player can reach (m).</summary>
        public const float MinHitHeight = 0.15f;
        public const float MaxHitHeight = 3.20f;

        // --- Setup ------------------------------------------------------------

        /// <summary>
        /// Build the humanoid, attach and wire all sub-components.
        /// HomePosition is set later via ResetForPoint (or the property).
        /// </summary>
        public void Init(int index, PlayerStats stats, ShuttlecockController shuttle, bool userControlled)
        {
            PlayerIndex = index;
            IsUserControlled = userControlled;
            Stats = stats ?? PlayerStats.DefaultPlayer();
            Shuttle = shuttle;

            // Model CC0 import (Quaternius, CC0) nếu GameObject có
            // CC0CharacterRig và model load được; bất cứ lỗi nào
            // (thiếu file/thiếu xương) cũng fallback sang humanoid
            // procedural — xem CC0CharacterRig.cs. Không throw ở đây.
            var cc0 = GetComponent<CC0CharacterRig>();
            HumanoidRig cc0Rig = null;
            bool useCC0 = cc0 != null && cc0.TryBuildRig(this, Stats, out cc0Rig);
            if (!useCC0)
                Rig = HumanoidBuilder.Build(gameObject, Stats, Stats.LeftHanded);
            else
                Rig = cc0Rig;

            Movement = GetComponent<PlayerMovement>() ?? gameObject.AddComponent<PlayerMovement>();
            Anim = GetComponent<PlayerAnimationController>() ?? gameObject.AddComponent<PlayerAnimationController>();
            Stamina = GetComponent<PlayerStamina>() ?? gameObject.AddComponent<PlayerStamina>();
            Racket = GetComponent<RacketController>() ?? gameObject.AddComponent<RacketController>();
            ShotCtrl = GetComponent<ShotController>() ?? gameObject.AddComponent<ShotController>();

            int sideSign = PlayerIndex == 0 ? -1 : 1;
            Movement.Init(Stats, sideSign);
            Stamina.Init(Stats);
            Anim.Bind(Rig);
            // Rig CC0: animation procedural phải đi qua retargeter (bind
            // pose T-pose khác rig procedural) — chỉ set khi CC0 dùng được.
            if (useCC0 && cc0.Retargeter != null)
                Anim.Retargeter = cc0.Retargeter;
            Racket.Bind(Rig, Anim);
            ShotCtrl.Init(Shuttle, this);

            Footwork = new FootworkController(Movement, _homePosition);
        }

        private void Update()
        {
            // Stamina regen: slow during a rally, faster between rallies.
            if (Stamina != null && Shuttle != null)
            {
                bool inRally = Shuttle.State == ShuttlecockController.ShuttleState.Flying;
                Stamina.Regen(Time.deltaTime, inRally);
                if (Movement != null) Movement.SpeedFactor = Stamina.SpeedFactor;
            }

            Footwork?.Update(Time.deltaTime);

            // Idle facing: let Movement look at the shuttle when standing still.
            if (Movement != null && Shuttle != null)
            {
                Movement.ShuttlePosition = Shuttle.transform.position;
                Movement.HasShuttlePosition =
                    Shuttle.State == ShuttlecockController.ShuttleState.Flying;
            }

            // Locomotion pose follows actual movement (user and AI alike):
            // Run while moving, back to Ready when still. Shot swings set
            // IsSwinging and take over the animation while they play.
            if (!IsSwinging && Anim != null && Movement != null)
            {
                if (Movement.IsMoving || Movement.HasMoveTarget)
                {
                    Anim.PlayLoop("Run");
                    Anim.SetRunSpeed(Mathf.Clamp01(
                        Movement.Velocity.magnitude / Mathf.Max(0.1f, Movement.MaxSpeed)));
                }
                else if (Anim.CurrentClipName == "Run")
                {
                    Anim.PlayLoop("Ready");
                }
            }

            // NOTE: user move input arrives via SetMoveInput (input layer) and
            // is consumed by PlayerMovement in FixedUpdate (read back above
            // through Movement.MoveInput / Velocity). The AI does nothing
            // here — OpponentAI drives MoveTowards / TryHit itself.
        }

        // --- Hitting ----------------------------------------------------------

        /// <summary>
        /// Can this player reach the shuttle right now? The shuttle must be
        /// within ReachRadius of the racket head and at a hittable height.
        /// distance = 3D distance from the shuttle to the racket head.
        /// </summary>
        public bool CanHitShuttle(Vector3 shuttlePos, out float distance)
        {
            Vector3 contact = Racket != null
                ? Racket.ContactPoint
                : transform.position + Vector3.up * 1.2f;
            distance = Vector3.Distance(shuttlePos, contact);
            if (distance > ReachRadius) return false;
            if (shuttlePos.y < MinHitHeight || shuttlePos.y > MaxHitHeight) return false;
            return true;
        }

        /// <summary>
        /// Attempt a shot. Returns false when the shot cannot be attempted
        /// (out of reach, wrong turn, already swinging, shuttle not flying).
        /// Returns true when a swing was executed — including a Miss, which
        /// plays the whiff animation and never touches the shuttle.
        /// On a good swing the shot is launched from the animation's
        /// onContact callback, at the clip's ContactTime.
        /// </summary>
        public bool TryHit(ShotType type, Vector3 target)
        {
            if (IsSwinging) return false;
            if (Shuttle == null) return false;
            if (Shuttle.State != ShuttlecockController.ShuttleState.Flying) return false;
            if (Shuttle.LastHitter == PlayerIndex) return false; // not our turn to hit

            Vector3 shuttlePos = Shuttle.transform.position;
            if (!CanHitShuttle(shuttlePos, out float distance)) return false;

            ShotQuality quality = EvaluateQuality(type, shuttlePos, distance);
            ShotProfile profile = ShotLibrary.Get(type);

            if (type == ShotType.JumpSmash) Movement?.Jump();

            if (quality == ShotQuality.Miss)
            {
                // Whiff: swing and miss, no contact, small stamina cost.
                IsSwinging = true;
                Stamina?.Drain(profile.StaminaCost * 0.4f, CurrentIntensity);
                Racket.PlaySwing(type, SwingDuration(type), null,
                    () => { IsSwinging = false; Footwork?.StartRecovery(); });
                return true;
            }

            IsSwinging = true;
            Stamina?.Drain(profile.StaminaCost, CurrentIntensity);
            ShotType shotType = type;
            Vector3 shotTarget = target;
            ShotQuality shotQuality = quality;
            Racket.PlaySwing(type, SwingDuration(type),
                onContact: () => ShotCtrl.ExecuteShot(shotType, shotTarget, shotQuality),
                onEnd: () => { IsSwinging = false; Footwork?.StartRecovery(); });
            return true;
        }

        /// <summary>
        /// Serve into the target service box. The shuttle must be Held;
        /// it is placed at the racket contact point, then launched from the
        /// Serve clip's onContact callback. Serve quality is deterministic
        /// (Perfect when stamina is high, else Good).
        /// </summary>
        public bool Serve(ShotType serveType, Vector3 targetBox)
        {
            if (!ShotLibrary.IsServe(serveType)) serveType = ShotType.ServeLow;
            if (IsSwinging) return false;
            if (Shuttle == null) return false;

            if (Shuttle.State == ShuttlecockController.ShuttleState.Held && Racket != null)
                Shuttle.HoldAt(Racket.ContactPoint);

            ShotQuality quality = (Stamina != null && Stamina.Current >= 70f)
                ? ShotQuality.Perfect
                : ShotQuality.Good;

            IsSwinging = true;
            Stamina?.Drain(ShotLibrary.Get(serveType).StaminaCost, CurrentIntensity);
            ShotType type = serveType;
            Racket.PlaySwing(type, Anim != null ? Anim.GetClipDuration("Serve") : 0.8f,
                onContact: () => ShotCtrl.ExecuteShot(type, targetBox, quality),
                onEnd: () => { IsSwinging = false; });
            return true;
        }

        // --- Movement (input / AI) --------------------------------------------

        /// <summary>Joystick input from the user (world-space XZ). See PlayerMovement.SetMoveInput.</summary>
        public void SetMoveInput(Vector2 input)
        {
            Movement?.SetMoveInput(input);
        }

        /// <summary>AI steering: move toward a court position. Footwork slows the player into small adjustment steps near the target.</summary>
        public void MoveTowards(Vector3 target, float speedFactor)
        {
            if (Movement == null) return;
            float factor = speedFactor;
            if (Footwork != null) factor *= Footwork.AdjustmentSpeedFactor(target);
            Movement.MoveTowards(target, factor);
            // The Run pose is switched on by Update from the actual movement.
        }

        /// <summary>Called when the opponent has just hit: split step.</summary>
        public void NotifyOpponentHit()
        {
            Footwork?.OnOpponentHit();
        }

        // --- Point lifecycle / reactions --------------------------------------

        /// <summary>
        /// Reset for a new point: teleport to the given position (point
        /// resets are the only teleport allowed), face the net, recover a
        /// little stamina, return to the Ready pose.
        /// </summary>
        public void ResetForPoint(Vector3 position)
        {
            HomePosition = position;
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(
                new Vector3(0f, 0f, PlayerIndex == 0 ? 1f : -1f), Vector3.up);
            Movement?.Teleport(position);
            IsSwinging = false;
            Anim?.PlayLoop("Ready");
            Stamina?.Restore(6f);
        }

        public void PlayCelebrate()
        {
            if (Anim == null) return;
            AnimClip clip = Anim.GetClip("Celebrate");
            if (clip != null) Anim.PlayClip(clip, 1f, null, null);
        }

        public void PlayDisappointed()
        {
            if (Anim == null) return;
            AnimClip clip = Anim.GetClip("Disappointed");
            if (clip != null) Anim.PlayClip(clip, 1f, null, null);
        }

        public void SetIntensity(IntensityMode mode)
        {
            if (Stamina != null) Stamina.Mode = mode;
        }

        private IntensityMode CurrentIntensity =>
            Stamina != null ? Stamina.Mode : IntensityMode.Default;

        // --- Quality evaluation -------------------------------------------------

        /// <summary>
        /// Quality from contact timing + position + stamina. Timing error is
        /// derived from the distance to the ideal contact point and whether
        /// the shuttle is still approaching (Early) or already going past
        /// (Late). Poor height or low stamina degrades the result one step.
        /// </summary>
        private ShotQuality EvaluateQuality(ShotType type, Vector3 shuttlePos, float distance)
        {
            Vector3 contact = Racket != null
                ? Racket.ContactPoint
                : transform.position + Vector3.up * 1.2f;
            Vector3 toRacket = contact - shuttlePos;
            float approach = toRacket.sqrMagnitude > 1e-6f
                ? Vector3.Dot(Shuttle.Velocity, toRacket.normalized)
                : 0f;

            // ~0.25 s of error per metre away from the ideal 0.35 m contact.
            float error = (distance - 0.35f) * 0.25f;
            error = approach >= 0f ? -Mathf.Abs(error) : Mathf.Abs(error);
            ShotQuality quality = ShotTiming.FromTimingError(error);

            ShotProfile profile = ShotLibrary.Get(type);
            if (shuttlePos.y < profile.MinShuttleHeight || shuttlePos.y > profile.MaxShuttleHeight)
                quality = Degrade(quality);

            if (Stamina != null && Stamina.PowerFactor < 0.9f)
                quality = Degrade(quality);

            return quality;
        }

        private static ShotQuality Degrade(ShotQuality q)
        {
            switch (q)
            {
                case ShotQuality.Perfect: return ShotQuality.Good;
                case ShotQuality.Good: return ShotQuality.Late;
                case ShotQuality.Late: return ShotQuality.Miss;
                case ShotQuality.Early: return ShotQuality.Miss;
                default: return ShotQuality.Miss;
            }
        }

        /// <summary>Swing duration (s) for a shot type: its clip duration.</summary>
        private float SwingDuration(ShotType type)
        {
            if (Anim == null) return 0.5f;
            return Anim.GetClipDuration(RacketController.ClipNameForShot(type));
        }
    }
}
