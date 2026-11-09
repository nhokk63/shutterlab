// PlayerStamina.cs — ShuttleLab (Unity 6, URP)
// Stamina pool (0-100) with intensity modes. MonoBehaviour so it can live on
// the PlayerController GameObject next to the other player components.
// Factors degrade smoothly once stamina drops below 30; OnExhausted fires
// once when stamina crosses below 10 (re-arms above 15).

using System;
using UnityEngine;

namespace ShuttleLab.Players
{
    /// <summary>How hard the player is pushing. Affects drain, power and regen.</summary>
    public enum IntensityMode
    {
        /// <summary>Drain x1.4, power x1.08, more jumps/smashes. Burns out fast.</summary>
        Aggressive,
        /// <summary>No modifiers.</summary>
        Default,
        /// <summary>Drain x0.7, power x0.94, regenerates faster. For long rallies.</summary>
        Efficient
    }

    public class PlayerStamina : MonoBehaviour
    {
        /// <summary>Fired once when stamina drops below 10 (re-arms above 15).</summary>
        public event Action OnExhausted;

        /// <summary>Low-stamina threshold: factors start degrading below this.</summary>
        public const float LowThreshold = 30f;

        /// <summary>Exhaustion threshold: OnExhausted fires below this.</summary>
        public const float ExhaustedThreshold = 10f;

        public IntensityMode Mode { get; set; } = IntensityMode.Default;

        public float MaxStamina { get; private set; } = 88f;

        public float Current { get; private set; }

        public float Normalized => MaxStamina > 0f ? Current / MaxStamina : 0f;

        public bool IsExhausted => Current < ExhaustedThreshold;

        public bool IsLow => Current < LowThreshold;

        private bool _exhaustedFired;

        private void Awake()
        {
            Current = MaxStamina;
        }

        /// <summary>Initialise the pool from the player's stats.</summary>
        public void Init(PlayerStats stats)
        {
            MaxStamina = stats != null ? stats.MaxStamina : 88f;
            Current = MaxStamina;
            _exhaustedFired = false;
        }

        /// <summary>
        /// Spend stamina. The cost is scaled by the given intensity mode
        /// (Aggressive x1.4, Efficient x0.7). Clamped at 0.
        /// </summary>
        public void Drain(float cost, IntensityMode mode)
        {
            if (cost <= 0f) return;
            float multiplier = mode == IntensityMode.Aggressive ? 1.4f
                : mode == IntensityMode.Efficient ? 0.7f : 1f;
            Current = Mathf.Max(0f, Current - cost * multiplier);
            if (Current < ExhaustedThreshold && !_exhaustedFired)
            {
                _exhaustedFired = true;
                OnExhausted?.Invoke();
            }
        }

        /// <summary>
        /// Regenerate over time. In-rally regen is slow (2.5/s); between
        /// rallies it is faster (6/s). Efficient mode regenerates 1.5x faster.
        /// Called every frame by PlayerController.
        /// </summary>
        public void Regen(float dt, bool inRally)
        {
            if (dt <= 0f) return;
            float rate = inRally ? 2.5f : 6f;
            if (Mode == IntensityMode.Efficient) rate *= 1.5f;
            Current = Mathf.Min(MaxStamina, Current + rate * dt);
            if (Current > 15f) _exhaustedFired = false;
        }

        /// <summary>Top the pool up (e.g. a small breather between points).</summary>
        public void Restore(float amount)
        {
            if (amount <= 0f) return;
            Current = Mathf.Min(MaxStamina, Current + amount);
            if (Current > 15f) _exhaustedFired = false;
        }

        /// <summary>
        /// Movement speed multiplier. 1.0 at/above 30 stamina, sliding down
        /// to 0.70 at 0 stamina.
        /// </summary>
        public float SpeedFactor =>
            Current >= LowThreshold ? 1f : Mathf.Lerp(0.70f, 1f, Current / LowThreshold);

        /// <summary>
        /// Shot power multiplier: low-stamina penalty (0.80 at 0 stamina)
        /// times the intensity-mode modifier (Aggressive x1.08,
        /// Efficient x0.94).
        /// </summary>
        public float PowerFactor
        {
            get
            {
                float baseFactor = Current >= LowThreshold
                    ? 1f
                    : Mathf.Lerp(0.80f, 1f, Current / LowThreshold);
                float modeFactor = Mode == IntensityMode.Aggressive ? 1.08f
                    : Mode == IntensityMode.Efficient ? 0.94f : 1f;
                return baseFactor * modeFactor;
            }
        }

        /// <summary>
        /// Dispersion multiplier for shot accuracy. 1.0 at/above 30 stamina,
        /// rising to 1.6x at 0 stamina (tired players spray the shuttle).
        /// Multiply the base dispersion by this.
        /// </summary>
        public float AccuracyFactor =>
            Current >= LowThreshold ? 1f : Mathf.Lerp(1.6f, 1f, Current / LowThreshold);
    }
}
