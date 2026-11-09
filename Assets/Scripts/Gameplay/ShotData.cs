using System.Collections.Generic;
using UnityEngine;

namespace ShuttleLab.Gameplay
{
    public enum ShotType
    {
        Clear, Drop, Smash, JumpSmash, HalfSmash,
        Drive, Lift, NetShot, NetKill, Push, Block, DefensiveDrive,
        ServeLow, ServeHigh, ServeFlick
    }

    public enum ShotQuality { Miss, Early, Late, Good, Perfect }

    /// <summary>
    /// Timing window around the ideal contact moment (seconds). Quality is
    /// computed from timing + contact position + posture + stamina — never
    /// from a bare random roll.
    /// </summary>
    public static class ShotTiming
    {
        public const float PerfectWindow = 0.040f;
        public const float GoodWindow = 0.090f;
        public const float LateWindow = 0.160f;

        public static ShotQuality FromTimingError(float errorSeconds)
        {
            float e = Mathf.Abs(errorSeconds);
            if (e <= PerfectWindow) return ShotQuality.Perfect;
            if (e <= GoodWindow) return ShotQuality.Good;
            if (e <= LateWindow) return errorSeconds > 0 ? ShotQuality.Late : ShotQuality.Early;
            return ShotQuality.Miss;
        }

        /// <summary>Target dispersion (metres) caused by quality.</summary>
        public static float Dispersion(ShotQuality q)
        {
            switch (q)
            {
                case ShotQuality.Perfect: return 0.08f;
                case ShotQuality.Good: return 0.18f;
                case ShotQuality.Late: return 0.55f;
                case ShotQuality.Early: return 0.60f;
                default: return 1.20f;
            }
        }

        /// <summary>Power factor caused by quality (1 = full).</summary>
        public static float PowerFactor(ShotQuality q)
        {
            switch (q)
            {
                case ShotQuality.Perfect: return 1.00f;
                case ShotQuality.Good: return 0.96f;
                case ShotQuality.Late: return 0.88f;
                case ShotQuality.Early: return 0.85f;
                default: return 0.60f;
            }
        }
    }

    /// <summary>Tunable profile per shot type. Values are design decisions for
    /// v0.1.0, to be refined against the Physics Calibration Scene (CSV logs).</summary>
    public class ShotProfile
    {
        public ShotType Type;
        public float FlightTime;      // target flight time (s)
        public float ApexHeight;      // target apex above the court (m)
        public float MinShuttleHeight;// min shuttle height to attempt (m)
        public float MaxShuttleHeight;// max shuttle height to attempt (m)
        public float StaminaCost;     // 0..100 scale
        public bool Overhead;         // hit above the head
        public bool Attacking;        // aggressive intent

        public ShotProfile(ShotType type, float flightTime, float apexHeight,
            float minH, float maxH, float stamina, bool overhead, bool attacking)
        {
            Type = type; FlightTime = flightTime; ApexHeight = apexHeight;
            MinShuttleHeight = minH; MaxShuttleHeight = maxH;
            StaminaCost = stamina; Overhead = overhead; Attacking = attacking;
        }
    }

    public static class ShotLibrary
    {
        private static readonly Dictionary<ShotType, ShotProfile> Profiles =
            new Dictionary<ShotType, ShotProfile>
        {
            { ShotType.Clear,        new ShotProfile(ShotType.Clear,        1.15f, 7.5f, 1.6f, 9.0f, 6f,  true,  false) },
            { ShotType.Drop,         new ShotProfile(ShotType.Drop,         0.95f, 2.6f, 1.6f, 9.0f, 6f,  true,  true) },
            { ShotType.Smash,        new ShotProfile(ShotType.Smash,        0.45f, 2.2f, 2.0f, 9.0f, 14f, true,  true) },
            { ShotType.JumpSmash,    new ShotProfile(ShotType.JumpSmash,    0.42f, 2.4f, 2.2f, 9.0f, 18f, true,  true) },
            { ShotType.HalfSmash,    new ShotProfile(ShotType.HalfSmash,    0.55f, 2.4f, 1.9f, 9.0f, 11f, true,  true) },
            { ShotType.Drive,        new ShotProfile(ShotType.Drive,        0.50f, 1.9f, 0.9f, 2.2f, 7f,  false, true) },
            { ShotType.Lift,         new ShotProfile(ShotType.Lift,         1.05f, 6.5f, 0.2f, 1.6f, 7f,  false, false) },
            { ShotType.NetShot,      new ShotProfile(ShotType.NetShot,      0.60f, 1.9f, 0.9f, 1.7f, 4f,  false, false) },
            { ShotType.NetKill,      new ShotProfile(ShotType.NetKill,      0.35f, 1.8f, 1.2f, 2.0f, 8f,  false, true) },
            { ShotType.Push,         new ShotProfile(ShotType.Push,         0.55f, 2.0f, 0.9f, 1.8f, 5f,  false, true) },
            { ShotType.Block,        new ShotProfile(ShotType.Block,        0.55f, 2.1f, 0.5f, 2.0f, 4f,  false, false) },
            { ShotType.DefensiveDrive, new ShotProfile(ShotType.DefensiveDrive, 0.55f, 2.0f, 0.5f, 2.0f, 6f, false, false) },
            { ShotType.ServeLow,     new ShotProfile(ShotType.ServeLow,     0.80f, 1.9f, 0.9f, 1.3f, 2f,  false, false) },
            { ShotType.ServeHigh,    new ShotProfile(ShotType.ServeHigh,    1.20f, 7.0f, 0.9f, 1.3f, 3f,  false, false) },
            { ShotType.ServeFlick,   new ShotProfile(ShotType.ServeFlick,   0.90f, 4.5f, 0.9f, 1.3f, 3f,  false, true) },
        };

        public static ShotProfile Get(ShotType type) => Profiles[type];

        public static bool IsOverhead(ShotType t) => Profiles[t].Overhead;
        public static bool IsAttacking(ShotType t) => Profiles[t].Attacking;

        public static bool IsNetShot(ShotType t)
            => t == ShotType.NetShot || t == ShotType.NetKill || t == ShotType.Push;

        public static bool IsServe(ShotType t)
            => t == ShotType.ServeLow || t == ShotType.ServeHigh || t == ShotType.ServeFlick;

        /// <summary>Can this shot be attempted from the shuttle's current height?</summary>
        public static bool CanAttempt(ShotType t, float shuttleHeight)
        {
            var p = Profiles[t];
            return shuttleHeight >= p.MinShuttleHeight && shuttleHeight <= p.MaxShuttleHeight;
        }
    }
}
