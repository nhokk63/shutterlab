// AIDifficulty.cs — ShuttleLab (Unity 6)
// Static tuning data for the 6 AI difficulty levels (0..5).
// Pure data, no MonoBehaviour, no scene dependencies. All balancing for
// the AI lives here (ARCHITECTURE.md: "No hard-coded data").
//
// LEVEL NAMES — đã thống nhất (2026-11-10): cả hai file dùng chung
// Beginner / Amateur / Intermediate / Advanced / Pro / Legend.
// (Comment cũ nói PlayerStats dùng tên khác là SAI — PlayerStats đã trả
// đúng 6 tên này; đừng "sửa" theo comment cũ kẻo phá vỡ sự khớp nhau.)

using UnityEngine;

namespace ShuttleLab.AI
{
    /// <summary>
    /// One difficulty level's tuning. Immutable value type — copy it to
    /// derive variants (e.g. PracticeVersion) instead of mutating.
    /// </summary>
    public struct DifficultyConfig
    {
        public readonly int Level;
        public readonly string Name;

        /// <summary>Seconds the AI waits after the opponent's hit before reacting.</summary>
        public readonly float ReactionDelay;

        /// <summary>Multiplier on PlayerStats.MoveSpeed.</summary>
        public readonly float MoveSpeedFactor;

        /// <summary>Target dispersion (m) added to the AI's shots.</summary>
        public readonly float ErrorRadius;

        /// <summary>0..1: how often the AI picks attacking shots over safe ones.</summary>
        public readonly float Aggression;

        /// <summary>0..1: chance to smash a high ball when a smash is available.</summary>
        public readonly float SmashFrequency;

        /// <summary>Prediction noise (m) added to the AI's landing prediction.</summary>
        public readonly float PredictionError;

        public DifficultyConfig(int level, string name, float reactionDelay,
            float moveSpeedFactor, float errorRadius, float aggression,
            float smashFrequency, float predictionError)
        {
            Level = level;
            Name = name;
            ReactionDelay = reactionDelay;
            MoveSpeedFactor = moveSpeedFactor;
            ErrorRadius = errorRadius;
            Aggression = aggression;
            SmashFrequency = smashFrequency;
            PredictionError = predictionError;
        }

        /// <summary>
        /// Practice-mode variant: the AI returns everything it can reach
        /// and does not make intentional errors — smaller dispersion, less
        /// aggression, fewer smashes. Applied by OpponentAI when
        /// MatchManager.IsPractice is true.
        /// </summary>
        public DifficultyConfig PracticeVersion()
        {
            return new DifficultyConfig(Level, Name, ReactionDelay,
                MoveSpeedFactor, ErrorRadius * 0.5f, Aggression * 0.6f,
                SmashFrequency * 0.7f, PredictionError * 0.5f);
        }
    }

    /// <summary>Static lookup for the 6 AI difficulty levels (0..5).</summary>
    public static class AIDifficulty
    {
        private static readonly DifficultyConfig[] Levels =
        {
            //            level  name          react  speed  error  aggr  smash  predErr
            new DifficultyConfig(0, "Beginner",     0.55f, 0.70f, 1.20f, 0.20f, 0.05f, 0.90f),
            new DifficultyConfig(1, "Amateur",      0.45f, 0.78f, 0.90f, 0.35f, 0.15f, 0.65f),
            new DifficultyConfig(2, "Intermediate", 0.35f, 0.85f, 0.60f, 0.50f, 0.30f, 0.45f),
            new DifficultyConfig(3, "Advanced",     0.25f, 0.92f, 0.40f, 0.65f, 0.50f, 0.30f),
            new DifficultyConfig(4, "Pro",          0.17f, 0.99f, 0.25f, 0.80f, 0.70f, 0.18f),
            new DifficultyConfig(5, "Legend",       0.12f, 1.05f, 0.15f, 0.90f, 0.85f, 0.08f),
        };

        /// <summary>Config for a level; out-of-range levels are clamped to 0..5.</summary>
        public static DifficultyConfig ForLevel(int level)
        {
            return Levels[Mathf.Clamp(level, 0, Levels.Length - 1)];
        }

        /// <summary>Display name of a level; out-of-range levels are clamped.</summary>
        public static string Name(int level)
        {
            return ForLevel(level).Name;
        }
    }
}
