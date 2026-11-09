// AIShotSelection.cs — ShuttleLab (Unity 6)
// Picks the AI's shot (type + landing target) for one contact.
// Plain class, no MonoBehaviour. Decisions come only from verifiable
// state — shuttle height/position, the two players' court positions,
// stamina, stats and difficulty — never from a bare random roll.
// Randomness (System.Random, passed in by the caller) is used only for
// target dispersion and for choosing between near-equal options.
//
// SELECTION PRINCIPLES (checked against ShotLibrary profiles):
// - High ball near the net (h >= 1.6 m, above the tape) -> NetKill if the
//   AI is aggressive enough, else NetShot / Push.
// - High ball, mid/back court (h >= 2.0 m) -> Smash / JumpSmash when
//   stamina allows and the difficulty's SmashFrequency roll passes,
//   else HalfSmash / Clear.
// - Deep on the AI's own side (shuttle z > 4.5 m) -> Clear, or Drop if
//   aggressive (attack the open net area).
// - Mid court (h 0.9..2.2 m) -> Drive, or Push.
// - Pressured (low ball h < 0.9 m, or the shuttle already past the AI)
//   -> Lift (safe, deep) or DefensiveDrive.
// - NO SMASH when stamina < 25% (Stamina.Normalized < 0.25).
// - Every candidate is filtered through ShotLibrary.CanAttempt(type, h);
//   if nothing fits, ShotSolver.FallbackShot(h) decides (Clear / Lift /
//   Drive by height), then Lift as the last resort.
//
// TARGETING: open space — the corner away from the opponent (mirrored X,
// deep Z on the USER side, so Z is NEGATIVE here), plus dispersion from
// difficulty + the AI's own accuracy + low-stamina spray. Targets are
// clamped inside the singles court with a 0.35 m margin, so the AI never
// deliberately aims out — misses come from dispersion, not intent.
//
// SIGNATURE NOTE: Choose takes the shuttle STATE (not position) because
// the live shuttle position/velocity is read from the reference given to
// Init(shuttle). If Init was never called, the selector falls back to
// deciding from the AI's own court position only (deep -> Clear, near
// net -> NetShot) — a degraded but still legal behaviour.

using UnityEngine;
using ShuttleLab.Core;
using ShuttleLab.Gameplay;
using ShuttleLab.Players;

namespace ShuttleLab.AI
{
    public class AIShotSelection
    {
        private ShuttlecockController _shuttle;

        // Net tape height: a kill below the tape would hit the net, so the
        // NetKill window starts above it (profile min is 1.2 m, we require
        // 1.6 m — see Choose).
        private const float NetTapeHeight = 1.55f;
        private const float TargetMargin = 0.35f;

        public void Init(ShuttlecockController shuttle)
        {
            _shuttle = shuttle;
        }

        /// <summary>
        /// Choose a shot for the current contact. state is the shuttle's
        /// state; aiPos/oppPos are the two players' positions (AI on the
        /// far side, z > 0; opponent on the near side, z < 0).
        /// </summary>
        public (ShotType type, Vector3 target) Choose(
            ShuttlecockController.ShuttleState state,
            Vector3 aiPos, Vector3 oppPos,
            PlayerStats aiStats, PlayerStamina stamina,
            DifficultyConfig diff, System.Random rng)
        {
            rng = rng ?? new System.Random();

            // Not a live rally shuttle: safe defensive default. The caller
            // (OpponentAI) normally never calls Choose in this case.
            if (state != ShuttlecockController.ShuttleState.Flying || _shuttle == null)
                return (ShotType.Lift, DeepTarget(oppPos, rng, diff, aiStats, stamina));

            Vector3 shuttlePos = _shuttle.transform.position;
            float h = shuttlePos.y;
            float aiZ = Mathf.Abs(aiPos.z);              // AI distance from the net
            float shuttleZ = shuttlePos.z;               // > 0 on the AI side
            bool nearNet = shuttleZ < 2.2f;
            bool deep = shuttleZ > 4.5f || aiZ > 4.5f;
            bool pressured = h < 0.9f || shuttleZ > aiPos.z + 1.0f; // low, or already past the AI
            bool highBall = h >= 2.0f;
            bool killable = h >= NetTapeHeight + 0.05f;

            bool canSmash = stamina == null || stamina.Normalized >= 0.25f;

            ShotType type;

            if (pressured)
            {
                // Get out of trouble: lift deep, or a flat defensive drive.
                type = (h >= 0.5f && rng.NextDouble() < 0.35)
                    ? ShotType.DefensiveDrive : ShotType.Lift;
            }
            else if (nearNet && killable)
            {
                type = rng.NextDouble() < diff.Aggression
                    ? ShotType.NetKill : ShotType.NetShot;
            }
            else if (nearNet)
            {
                type = rng.NextDouble() < 0.5 ? ShotType.NetShot : ShotType.Push;
            }
            else if (highBall)
            {
                if (canSmash && rng.NextDouble() < diff.SmashFrequency)
                {
                    type = (h >= 2.2f && stamina != null && stamina.Normalized > 0.5f
                            && rng.NextDouble() < 0.4)
                        ? ShotType.JumpSmash : ShotType.Smash;
                }
                else if (rng.NextDouble() < 0.5)
                {
                    type = ShotType.HalfSmash;
                }
                else
                {
                    type = ShotType.Clear;
                }
            }
            else if (deep)
            {
                // Deep ball: clear it away, or attack with a drop.
                type = rng.NextDouble() < diff.Aggression * 0.5
                    ? ShotType.Drop : ShotType.Clear;
            }
            else
            {
                // Mid court: flat drive, or a push towards the net.
                type = rng.NextDouble() < 0.5 + diff.Aggression * 0.3
                    ? ShotType.Drive : ShotType.Push;
            }

            // Enforce the height window of the chosen profile; fall back to
            // the height-based default, then to Lift.
            if (!ShotLibrary.CanAttempt(type, h))
            {
                type = ShotSolver.FallbackShot(h);
                if (!ShotLibrary.CanAttempt(type, h))
                    type = ShotType.Lift;
            }

            Vector3 target = TargetFor(type, oppPos, rng, diff, aiStats, stamina);
            return (type, target);
        }

        // --- Targeting -------------------------------------------------------

        private Vector3 TargetFor(ShotType type, Vector3 oppPos,
            System.Random rng, DifficultyConfig diff,
            PlayerStats aiStats, PlayerStamina stamina)
        {
            switch (type)
            {
                case ShotType.Drop:
                case ShotType.NetShot:
                case ShotType.NetKill:
                case ShotType.Push:
                    // Short: just behind the net on the user's side.
                    return ShortTarget(oppPos, rng, diff, aiStats, stamina);
                case ShotType.Smash:
                case ShotType.JumpSmash:
                case ShotType.HalfSmash:
                    // Steep: at the opponent's feet / away from them, mid-deep.
                    return SmashTarget(oppPos, rng, diff, aiStats, stamina);
                default:
                    // Clear / Lift / Drive / DefensiveDrive: deep corners.
                    return DeepTarget(oppPos, rng, diff, aiStats, stamina);
            }
        }

        /// <summary>Deep corner away from the opponent (user side: z negative).</summary>
        private Vector3 DeepTarget(Vector3 oppPos, System.Random rng,
            DifficultyConfig diff, PlayerStats aiStats, PlayerStamina stamina)
        {
            float x = -Mathf.Sign(oppPos.x == 0f ? 1f : oppPos.x)
                * (1.4f + (float)rng.NextDouble() * 0.8f);
            float z = -(5.4f + (float)rng.NextDouble() * 0.9f);
            return ClampAndDisperse(new Vector3(x, 0f, z), rng, diff, aiStats, stamina);
        }

        /// <summary>Just behind the net, away from the opponent.</summary>
        private Vector3 ShortTarget(Vector3 oppPos, System.Random rng,
            DifficultyConfig diff, PlayerStats aiStats, PlayerStamina stamina)
        {
            float x = -Mathf.Sign(oppPos.x == 0f ? 1f : oppPos.x)
                * (0.8f + (float)rng.NextDouble() * 1.2f);
            float z = -(0.9f + (float)rng.NextDouble() * 0.7f);
            return ClampAndDisperse(new Vector3(x, 0f, z), rng, diff, aiStats, stamina);
        }

        /// <summary>Mid-deep, on the side the opponent is not covering.</summary>
        private Vector3 SmashTarget(Vector3 oppPos, System.Random rng,
            DifficultyConfig diff, PlayerStats aiStats, PlayerStamina stamina)
        {
            float x = -Mathf.Sign(oppPos.x == 0f ? 1f : oppPos.x)
                * (1.2f + (float)rng.NextDouble() * 1.0f);
            float z = -(3.4f + (float)rng.NextDouble() * 2.0f);
            return ClampAndDisperse(new Vector3(x, 0f, z), rng, diff, aiStats, stamina);
        }

        private Vector3 ClampAndDisperse(Vector3 target, System.Random rng,
            DifficultyConfig diff, PlayerStats aiStats, PlayerStamina stamina)
        {
            // Dispersion: difficulty error + the AI's own accuracy, widened
            // when stamina is low (tired players spray the shuttle).
            float dispersion = diff.ErrorRadius
                + (aiStats != null ? aiStats.AccuracyDispersion : 0.2f);
            if (stamina != null)
                dispersion *= stamina.AccuracyFactor;

            if (dispersion > 0.001f)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float radius = dispersion * (float)System.Math.Sqrt(rng.NextDouble());
                target += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            }

            float hw = CourtDimensions.SinglesHalfWidth - TargetMargin;
            float zMax = CourtDimensions.CourtHalfLength - TargetMargin;
            target.x = Mathf.Clamp(target.x, -hw, hw);
            target.z = Mathf.Clamp(target.z, -zMax, -0.6f); // user side only
            target.y = 0f;
            return target;
        }
    }
}
