// PlayerStats.cs — ShuttleLab (Unity 6, URP)
// Data class: the 5 core attributes (1-99) of a badminton player plus
// identity/kit data. Pure data + derived values; no MonoBehaviour, no scene
// dependencies. Derived formulas are the v0.1.0 design values from
// ARCHITECTURE.md ("No hard-coded data: stats ... live in data classes").

using UnityEngine;

namespace ShuttleLab.Players
{
    /// <summary>
    /// The 5 core attributes of a player (1-99 each) plus identity and kit.
    /// Derived values (MoveSpeed, ShotPower, ...) are computed from the raw
    /// attributes so balancing stays in one place.
    /// </summary>
    [System.Serializable]
    public class PlayerStats
    {
        // --- Identity -------------------------------------------------------
        public string Name = "Player";
        public string Country = "VIE";

        /// <summary>True = racket in the left hand.</summary>
        public bool LeftHanded = false;

        // --- Core attributes (1-99) -----------------------------------------
        public int Power = 60;
        public int Accuracy = 60;
        public int Speed = 60;
        public int Stamina = 60;
        public int Defense = 60;

        // --- Kit / appearance colours ---------------------------------------
        public Color ShirtColor = new Color(0.10f, 0.35f, 0.75f);   // blue
        public Color ShortsColor = new Color(0.08f, 0.08f, 0.10f);   // near-black
        public Color ShoeColor = Color.white;
        public Color SkinColor = new Color(0.85f, 0.65f, 0.50f);
        public Color HairColor = new Color(0.12f, 0.09f, 0.07f);

        // --- Derived values -------------------------------------------------

        /// <summary>Max court speed in m/s. 60 Speed -&gt; 7.3 m/s.</summary>
        public float MoveSpeed => 5.5f + Speed * 0.03f;

        /// <summary>
        /// Multiplier applied to shot power (used by AI shot selection and
        /// later by the solver power path). 60 Power -&gt; 1.03.
        /// </summary>
        public float ShotPower => 0.85f + Power * 0.003f;

        /// <summary>
        /// Base target dispersion in metres from the player's own accuracy
        /// (before shot-quality dispersion from ShotTiming is added).
        /// 60 Accuracy -&gt; 0.23 m. Floor 0.08 m.
        /// </summary>
        public float AccuracyDispersion => Mathf.Max(0.08f, 0.35f - Accuracy * 0.002f);

        /// <summary>Stamina pool (0-100 scale). 60 Stamina -&gt; 88.</summary>
        public float MaxStamina => 70f + Stamina * 0.3f;

        // --- Factories ------------------------------------------------------

        /// <summary>A balanced ~60-stat player (the default human player).</summary>
        public static PlayerStats DefaultPlayer()
        {
            return new PlayerStats
            {
                Name = "Player 1",
                Country = "VIE",
                LeftHanded = false,
                Power = 60,
                Accuracy = 62,
                Speed = 60,
                Stamina = 60,
                Defense = 58,
                ShirtColor = new Color(0.10f, 0.35f, 0.75f),
                ShortsColor = new Color(0.08f, 0.08f, 0.10f),
            };
        }

        /// <summary>
        /// Stats của nhân vật trong roster (CharacterRoster). Id 0 = nhân
        /// vật mặc định (tương đương DefaultPlayer nhưng tên/kit từ roster);
        /// id lạ -> nhân vật mặc định.
        /// </summary>
        public static PlayerStats FromCharacter(int id)
        {
            var entry = ShuttleLab.Systems.CharacterRoster.Get(id);
            return new PlayerStats
            {
                Name = entry.Name,
                Country = entry.Country,
                LeftHanded = entry.LeftHanded,
                Power = entry.Power,
                Accuracy = entry.Accuracy,
                Speed = entry.Speed,
                Stamina = entry.Stamina,
                Defense = entry.Defense,
                ShirtColor = entry.ShirtColor,
                ShortsColor = entry.ShortsColor,
                ShoeColor = entry.ShoeColor,
                SkinColor = entry.SkinColor,
                HairColor = entry.HairColor,
            };
        }

        /// <summary>
        /// AI stats for a difficulty level 0..5:
        /// 0 Beginner ~35, 1 Amateur ~50, 2 Intermediate ~62,
        /// 3 Advanced ~74, 4 Pro ~84, 5 Legend ~92.
        /// Out-of-range levels are clamped.
        /// </summary>
        public static PlayerStats FromDifficulty(int level)
        {
            level = Mathf.Clamp(level, 0, 5);
            // Base value per level; each attribute jitters a little around it
            // so AI players do not feel identical.
            int[] bases = { 35, 50, 62, 74, 84, 92 };
            int b = bases[level];
            return new PlayerStats
            {
                Name = DifficultyName(level),
                Country = "AI",
                LeftHanded = false,
                Power = ClampStat(b + 2),
                Accuracy = ClampStat(b),
                Speed = ClampStat(b + 1),
                Stamina = ClampStat(b - 2),
                Defense = ClampStat(b),
                ShirtColor = new Color(0.75f, 0.15f, 0.15f),
                ShortsColor = new Color(0.08f, 0.08f, 0.10f),
            };
        }

        // Tên 6 cấp độ — PHẢI khớp AIDifficulty (Beginner/Amateur/
        // Intermediate/Advanced/Pro/Legend). Đừng đổi một bên mà không
        // đổi bên kia; UI hiển thị tên cấp từ AIDifficulty.Name.
        public static string DifficultyName(int level)
        {
            switch (Mathf.Clamp(level, 0, 5))
            {
                case 0: return "Beginner";
                case 1: return "Amateur";
                case 2: return "Intermediate";
                case 3: return "Advanced";
                case 4: return "Pro";
                default: return "Legend";
            }
        }

        public static int ClampStat(int v) => Mathf.Clamp(v, 1, 99);

        /// <summary>Clamp every attribute into 1..99 (call after manual edits).</summary>
        public void ClampAll()
        {
            Power = ClampStat(Power);
            Accuracy = ClampStat(Accuracy);
            Speed = ClampStat(Speed);
            Stamina = ClampStat(Stamina);
            Defense = ClampStat(Defense);
        }
    }
}
