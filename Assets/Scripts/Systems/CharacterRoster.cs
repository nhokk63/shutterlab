// CharacterRoster.cs — ShuttleLab (Unity 6)
//
// Roster 6 nhân vật hư cấu mở khoá (PROGRESSION_SPEC.md §2): mỗi người
// một phân bổ chỉ số + màu áo/quần/giày/tóc/da riêng. Mở bằng xu (Career)
// hoặc hạng/vô địch giải. KHÔNG dùng tên người thật / thương hiệu thật.
//
// Lưu trữ: PlayerPrefs (chủ duy nhất, giống setting GameStateManager):
//  - "shuttlelab_character_id": id nhân vật đang chọn (0 = mặc định).
//  - "shuttlelab_characters_unlocked": id đã mở, dạng "0,2,5".
// Không chạm SaveManager để khỏi đổi schema save v1.
//
// Phụ thuộc: UnityEngine (Color, PlayerPrefs, Mathf), ShuttleLab.Systems
// (CareerManager cho điều kiện mở khoá). Không phụ thuộc Players (tránh
// vòng lặp): PlayerStats.FromCharacter đọc roster này.

using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ShuttleLab.Systems
{
    /// <summary>Một nhân vật trong roster.</summary>
    public class CharacterEntry
    {
        public int Id;
        public string Name;
        public string Country;
        public bool LeftHanded;
        public int Power;
        public int Accuracy;
        public int Speed;
        public int Stamina;
        public int Defense;
        public Color ShirtColor;
        public Color ShortsColor;
        public Color ShoeColor;
        public Color SkinColor;
        public Color HairColor;
        /// <summary>Xu cần để mở (0 = không dùng điều kiện xu).</summary>
        public int UnlockCost;
        /// <summary>Hạng cần đạt (nhỏ hơn hoặc bằng; 0 = không dùng).</summary>
        public int UnlockRank;
        /// <summary>Số danh hiệu cần (0 = không dùng).</summary>
        public int UnlockTitles;
        /// <summary>Mô tả điều kiện mở khoá hiển thị trong UI (vd "1000 xu").</summary>
        public string UnlockText;
    }

    /// <summary>Roster nhân vật + trạng thái mở khoá / đang chọn.</summary>
    public static class CharacterRoster
    {
        public const string KeySelectedId = "shuttlelab_character_id";
        public const string KeyUnlockedIds = "shuttlelab_characters_unlocked";

        private static readonly CharacterEntry[] _entries =
        {
            // 0 — mặc định, cân bằng, mở sẵn. Kit áo đỏ / quần tối giữ đúng
            // màu user hiện tại (GameBootstrap từng ghi đè đỏ/tối tại đây).
            new CharacterEntry
            {
                Id = 0, Name = "Minh Quân", Country = "VIE", LeftHanded = false,
                Power = 60, Accuracy = 62, Speed = 60, Stamina = 60, Defense = 58,
                ShirtColor = new Color(0.75f, 0.15f, 0.15f),
                ShortsColor = new Color(0.08f, 0.08f, 0.10f),
                ShoeColor = Color.white,
                SkinColor = new Color(0.85f, 0.65f, 0.50f),
                HairColor = new Color(0.12f, 0.09f, 0.07f),
                UnlockCost = 0, UnlockRank = 0, UnlockTitles = 0,
                UnlockText = "Đã mở",
            },
            // 1 — tay đập mạnh: sức mạnh cao, đánh đổi chính xác/phòng thủ.
            new CharacterEntry
            {
                Id = 1, Name = "Bảo Long", Country = "VIE", LeftHanded = false,
                Power = 78, Accuracy = 55, Speed = 58, Stamina = 62, Defense = 52,
                ShirtColor = new Color(0.85f, 0.45f, 0.10f),
                ShortsColor = new Color(0.08f, 0.08f, 0.10f),
                ShoeColor = Color.white,
                SkinColor = new Color(0.80f, 0.60f, 0.45f),
                HairColor = new Color(0.25f, 0.15f, 0.08f),
                UnlockCost = 1000, UnlockRank = 0, UnlockTitles = 0,
                UnlockText = "1000 xu",
            },
            // 2 — nhanh nhẹn: tốc độ cao nhất roster, sức mạnh thấp.
            new CharacterEntry
            {
                Id = 2, Name = "Thanh Thảo", Country = "VIE", LeftHanded = false,
                Power = 52, Accuracy = 70, Speed = 80, Stamina = 65, Defense = 60,
                ShirtColor = new Color(0.15f, 0.60f, 0.30f),
                ShortsColor = new Color(0.90f, 0.90f, 0.92f),
                ShoeColor = Color.white,
                SkinColor = new Color(0.88f, 0.68f, 0.52f),
                HairColor = new Color(0.10f, 0.08f, 0.06f),
                UnlockCost = 0, UnlockRank = 256, UnlockTitles = 0,
                UnlockText = "hạng 256 trở lên",
            },
            // 3 — tường phòng thủ, tay trái: phòng thủ + thể lực cao.
            new CharacterEntry
            {
                Id = 3, Name = "Đức Anh", Country = "VIE", LeftHanded = true,
                Power = 55, Accuracy = 66, Speed = 60, Stamina = 72, Defense = 82,
                ShirtColor = new Color(0.85f, 0.75f, 0.15f),
                ShortsColor = new Color(0.10f, 0.15f, 0.35f),
                ShoeColor = Color.white,
                SkinColor = new Color(0.82f, 0.62f, 0.46f),
                HairColor = new Color(0.12f, 0.09f, 0.07f),
                UnlockCost = 2500, UnlockRank = 0, UnlockTitles = 0,
                UnlockText = "2500 xu",
            },
            // 4 — kiểm soát: chính xác cao nhất roster.
            new CharacterEntry
            {
                Id = 4, Name = "Hải Yến", Country = "VIE", LeftHanded = false,
                Power = 58, Accuracy = 84, Speed = 66, Stamina = 60, Defense = 64,
                ShirtColor = new Color(0.45f, 0.25f, 0.65f),
                ShortsColor = new Color(0.08f, 0.08f, 0.10f),
                ShoeColor = Color.white,
                SkinColor = new Color(0.86f, 0.66f, 0.50f),
                HairColor = new Color(0.30f, 0.18f, 0.10f),
                UnlockCost = 0, UnlockRank = 0, UnlockTitles = 1,
                UnlockText = "vô địch 1 giải đấu",
            },
            // 5 — tinh hoa toàn diện: mọi chỉ số đều cao, giá đắt nhất.
            new CharacterEntry
            {
                Id = 5, Name = "Quốc Huy", Country = "VIE", LeftHanded = false,
                Power = 76, Accuracy = 74, Speed = 74, Stamina = 72, Defense = 72,
                ShirtColor = new Color(0.92f, 0.92f, 0.95f),
                ShortsColor = new Color(0.75f, 0.60f, 0.20f),
                ShoeColor = Color.white,
                SkinColor = new Color(0.84f, 0.64f, 0.48f),
                HairColor = new Color(0.12f, 0.09f, 0.07f),
                UnlockCost = 4000, UnlockRank = 0, UnlockTitles = 0,
                UnlockText = "4000 xu",
            },
        };

        public static int Count => _entries.Length;

        /// <summary>Lấy entry theo id; id lạ -> nhân vật mặc định (id 0).</summary>
        public static CharacterEntry Get(int id)
        {
            if (id < 0 || id >= _entries.Length) return _entries[0];
            return _entries[id];
        }

        /// <summary>Id nhân vật đang chọn (lưu PlayerPrefs, mặc định 0).</summary>
        public static int SelectedId
        {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(KeySelectedId, 0), 0, _entries.Length - 1); }
            set
            {
                PlayerPrefs.SetInt(KeySelectedId, Mathf.Clamp(value, 0, _entries.Length - 1));
                PlayerPrefs.Save();
            }
        }

        /// <summary>Nhân vật đã mở chưa (id 0 luôn mở).</summary>
        public static bool IsUnlocked(int id)
        {
            if (id == 0) return true;
            if (id < 0 || id >= _entries.Length) return false;
            return LoadUnlocked().Contains(id);
        }

        /// <summary>Đánh dấu đã mở (không kiểm tra điều kiện — gọi sau TryUnlock).</summary>
        public static void Unlock(int id)
        {
            if (id <= 0 || id >= _entries.Length) return;
            var set = LoadUnlocked();
            if (set.Add(id)) SaveUnlocked(set);
        }

        /// <summary>
        /// Điều kiện mở khoá đã đủ chưa (chưa trừ tiền). career null hoặc
        /// chưa bắt đầu sự nghiệp -> chưa đủ (trừ nhân vật mở sẵn).
        /// </summary>
        public static bool IsUnlockConditionMet(int id, CareerManager career)
        {
            if (id < 0 || id >= _entries.Length) return false;
            CharacterEntry entry = _entries[id];
            if (entry.UnlockCost <= 0 && entry.UnlockRank <= 0 && entry.UnlockTitles <= 0)
                return true;
            if (career == null || !career.Active) return false;
            if (entry.UnlockCost > 0) return career.Money >= entry.UnlockCost;
            if (entry.UnlockRank > 0) return career.Ranking <= entry.UnlockRank;
            if (entry.UnlockTitles > 0) return career.Titles >= entry.UnlockTitles;
            return false;
        }

        /// <summary>
        /// Mở khoá nhân vật: kiểm tra điều kiện, trừ xu nếu cần (qua
        /// CareerManager.SpendMoney — tự ghi save), rồi đánh dấu đã mở.
        /// Trả false nếu chưa đủ điều kiện / không đủ xu.
        /// </summary>
        public static bool TryUnlock(int id, CareerManager career)
        {
            if (id < 0 || id >= _entries.Length) return false;
            if (IsUnlocked(id)) return true;
            if (!IsUnlockConditionMet(id, career)) return false;
            CharacterEntry entry = _entries[id];
            if (entry.UnlockCost > 0)
            {
                if (career == null) return false;
                bool paid = career.SpendMoney(entry.UnlockCost);
                if (!paid) return false;
            }
            Unlock(id);
            return true;
        }

        private static HashSet<int> LoadUnlocked()
        {
            var set = new HashSet<int>();
            set.Add(0);
            string raw = PlayerPrefs.GetString(KeyUnlockedIds, "");
            if (string.IsNullOrEmpty(raw)) return set;
            string[] parts = raw.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                int parsed;
                if (!int.TryParse(parts[i], out parsed)) continue;
                if (parsed >= 0 && parsed < _entries.Length)
                    set.Add(parsed);
            }
            return set;
        }

        private static void SaveUnlocked(HashSet<int> set)
        {
            var sb = new StringBuilder();
            bool first = true;
            foreach (int id in set)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(id);
            }
            PlayerPrefs.SetString(KeyUnlockedIds, sb.ToString());
            PlayerPrefs.Save();
        }
    }
}
