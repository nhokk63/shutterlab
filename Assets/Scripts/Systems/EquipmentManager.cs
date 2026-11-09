// EquipmentManager.cs — ShuttleLab (Unity 6)
//
// Vợt / giày / áo HƯ CẤU — tên tự đặt (Bão Tố, Phong Vũ, Lôi Đình...),
// KHÔNG dùng thương hiệu thật (Yonex, Victor, Li-Ning, Mizuno...).
// Mỗi món có modifier nhỏ lên chỉ số (+1..+5) và giá tiền ảo (mua bằng
// tiền Career). Modifier CỐ TÌNH nhỏ: trang bị hỗ trợ nhẹ, không bao giờ
// thay thế kỹ năng — đúng tinh thần "không pay-to-win" của ARCHITECTURE.md.
//
// ApplyTo(PlayerStats) trả về BẢN SAO đã cộng modifier (không sửa stats
// gốc), kẹp 1..99. Mua: Buy(item, career) trừ tiền ảo qua CareerManager.
// Sở hữu / đang đeo lưu qua SaveManager.
//
// Logic thuần, không Unity scene, không MonoBehaviour.
//
// Phụ thuộc: ShuttleLab.Systems (SaveManager, CareerManager),
// ShuttleLab.Players (PlayerStats). Không phụ thuộc Match/UI.

using System.Collections.Generic;
using UnityEngine;

namespace ShuttleLab.Systems
{
    /// <summary>Khe trang bị (mỗi khe chỉ đeo một món).</summary>
    public enum EquipmentSlot
    {
        Racket,
        Shoes,
        Shirt,
    }

    /// <summary>Một món trang bị trong cửa hàng.</summary>
    public class EquipmentItem
    {
        public string Id;
        public string Name;
        public EquipmentSlot Slot;
        public int PowerMod;
        public int SpeedMod;
        public int StaminaMod;
        public int Price; // tiền ảo Career (0 = đồ khởi đầu)
        public string Description;

        public EquipmentItem(string id, string name, EquipmentSlot slot,
            int powerMod, int speedMod, int staminaMod, int price, string description)
        {
            Id = id;
            Name = name;
            Slot = slot;
            PowerMod = powerMod;
            SpeedMod = speedMod;
            StaminaMod = staminaMod;
            Price = price;
            Description = description;
        }

        public string ModText()
        {
            var parts = new List<string>();
            if (PowerMod != 0) parts.Add($"Sức mạnh {(PowerMod > 0 ? "+" : "")}{PowerMod}");
            if (SpeedMod != 0) parts.Add($"Tốc độ {(SpeedMod > 0 ? "+" : "")}{SpeedMod}");
            if (StaminaMod != 0) parts.Add($"Thể lực {(StaminaMod > 0 ? "+" : "")}{StaminaMod}");
            return parts.Count > 0 ? string.Join(", ", parts) : "Không đổi chỉ số";
        }
    }

    /// <summary>Cửa hàng + tủ đồ. Một EquipmentManager giữ trạng thái sở hữu
    /// của một profile (nạp từ SaveManager).</summary>
    public class EquipmentManager
    {
        // Đồ khởi đầu: 0 modifier, giá 0, sở hữu sẵn, đeo sẵn.
        public const string StarterRacketId = "rkt_starter";
        public const string StarterShoesId = "sho_starter";
        public const string StarterShirtId = "shr_starter";

        private static readonly EquipmentItem[] Catalog =
        {
            // Vợt
            new EquipmentItem(StarterRacketId, "Vợt Khởi Đầu", EquipmentSlot.Racket,
                0, 0, 0, 0, "Vợt tập cơ bản, cân bằng."),
            new EquipmentItem("rkt_balance_5", "Cân Bằng 5", EquipmentSlot.Racket,
                1, 1, 1, 500, "Mọi chỉ số +1, dễ làm quen."),
            new EquipmentItem("rkt_wind_77", "Phong Vũ 77", EquipmentSlot.Racket,
                2, 2, 0, 650, "Nhẹ, vung nhanh, đánh linh hoạt."),
            new EquipmentItem("rkt_storm_900", "Bão Tố 900", EquipmentSlot.Racket,
                4, 0, -1, 800, "Đập mạnh nhưng hơi nặng tay."),
            new EquipmentItem("rkt_thunder_x", "Lôi Đình X", EquipmentSlot.Racket,
                5, -2, 0, 1200, "Sức mạnh tối đa, đánh đổi tốc độ."),
            // Giày
            new EquipmentItem(StarterShoesId, "Giày Khởi Đầu", EquipmentSlot.Shoes,
                0, 0, 0, 0, "Giày tập cơ bản."),
            new EquipmentItem("sho_grip_pro", "Bám Sân Pro", EquipmentSlot.Shoes,
                0, 3, 0, 400, "Đế bám sân, đổi hướng nhanh."),
            new EquipmentItem("sho_light_air", "Nhẹ Nhàng Air", EquipmentSlot.Shoes,
                0, 2, 1, 550, "Nhẹ chân, di chuyển bền hơn."),
            new EquipmentItem("sho_court_king", "Vua Sân Đấu", EquipmentSlot.Shoes,
                0, 4, -1, 900, "Tốc độ tối đa, hơi tốn sức."),
            // Áo
            new EquipmentItem(StarterShirtId, "Áo Khởi Đầu", EquipmentSlot.Shirt,
                0, 0, 0, 0, "Áo tập cơ bản."),
            new EquipmentItem("shr_dry_cool", "Thoáng Mát Dry", EquipmentSlot.Shirt,
                0, 0, 2, 250, "Thoát mồ hôi, đỡ mệt hơn."),
            new EquipmentItem("shr_aero_fit", "Aero Fit", EquipmentSlot.Shirt,
                0, 1, 2, 450, "Ôm người, chạy nhanh, bền hơn."),
            new EquipmentItem("shr_champion", "Nhà Vô Địch", EquipmentSlot.Shirt,
                1, 0, 3, 800, "Thể lực tốt, tự tin hơn khi đập."),
        };

        public string LastError { get; private set; } = "";

        private readonly HashSet<string> _owned = new HashSet<string>();
        private readonly Dictionary<EquipmentSlot, string> _equipped =
            new Dictionary<EquipmentSlot, string>();

        public EquipmentManager()
        {
            _owned.Add(StarterRacketId);
            _owned.Add(StarterShoesId);
            _owned.Add(StarterShirtId);
            _equipped[EquipmentSlot.Racket] = StarterRacketId;
            _equipped[EquipmentSlot.Shoes] = StarterShoesId;
            _equipped[EquipmentSlot.Shirt] = StarterShirtId;
        }

        public IReadOnlyList<EquipmentItem> AllItems => Catalog;

        public EquipmentItem GetItem(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var item in Catalog)
            {
                if (item.Id == id) return item;
            }
            return null;
        }

        public List<EquipmentItem> ListBySlot(EquipmentSlot slot)
        {
            var list = new List<EquipmentItem>();
            foreach (var item in Catalog)
            {
                if (item.Slot == slot) list.Add(item);
            }
            return list;
        }

        public bool IsOwned(string itemId) =>
            !string.IsNullOrEmpty(itemId) && _owned.Contains(itemId);

        public bool IsEquipped(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            foreach (var kv in _equipped)
            {
                if (kv.Value == itemId) return true;
            }
            return false;
        }

        public string EquippedId(EquipmentSlot slot)
        {
            string id;
            return _equipped.TryGetValue(slot, out id) ? id : null;
        }

        public EquipmentItem EquippedItem(EquipmentSlot slot) =>
            GetItem(EquippedId(slot));

        /// <summary>
        /// Mua bằng tiền ảo Career. Trả false + LastError nếu chưa đủ tiền
        /// hoặc đã sở hữu. Mua xong tự lưu.
        /// </summary>
        public bool Buy(EquipmentItem item, CareerManager career)
        {
            LastError = "";
            if (item == null) { LastError = "Món đồ không tồn tại."; return false; }
            if (IsOwned(item.Id)) { LastError = "Bạn đã sở hữu món này."; return false; }
            if (career == null) { LastError = "Chưa có sự nghiệp để dùng tiền."; return false; }
            if (!career.SpendMoney(item.Price))
            {
                LastError = $"Không đủ tiền (cần {item.Price} xu).";
                return false;
            }
            _owned.Add(item.Id);
            WriteToSave();
            return true;
        }

        /// <summary>Đeo món đã sở hữu (mỗi khe một món). Trả false nếu chưa sở hữu.</summary>
        public bool Equip(string itemId)
        {
            LastError = "";
            var item = GetItem(itemId);
            if (item == null) { LastError = "Món đồ không tồn tại."; return false; }
            if (!IsOwned(itemId)) { LastError = "Bạn chưa sở hữu món này."; return false; }
            _equipped[item.Slot] = itemId;
            WriteToSave();
            return true;
        }

        /// <summary>Tháo về đồ khởi đầu của khe đó.</summary>
        public void Unequip(EquipmentSlot slot)
        {
            string starter = slot == EquipmentSlot.Racket ? StarterRacketId
                : slot == EquipmentSlot.Shoes ? StarterShoesId : StarterShirtId;
            _equipped[slot] = starter;
            WriteToSave();
        }

        /// <summary>
        /// Trả về BẢN SAO PlayerStats đã cộng modifier của đồ đang đeo
        /// (kẹp 1..99). Stats gốc không bị sửa.
        /// </summary>
        public Players.PlayerStats ApplyTo(Players.PlayerStats baseStats)
        {
            if (baseStats == null) return null;
            var result = new Players.PlayerStats
            {
                Name = baseStats.Name,
                Country = baseStats.Country,
                LeftHanded = baseStats.LeftHanded,
                Power = baseStats.Power,
                Accuracy = baseStats.Accuracy,
                Speed = baseStats.Speed,
                Stamina = baseStats.Stamina,
                Defense = baseStats.Defense,
                ShirtColor = baseStats.ShirtColor,
                ShortsColor = baseStats.ShortsColor,
                ShoeColor = baseStats.ShoeColor,
                SkinColor = baseStats.SkinColor,
                HairColor = baseStats.HairColor,
            };
            foreach (var kv in _equipped)
            {
                var item = GetItem(kv.Value);
                if (item == null) continue;
                result.Power += item.PowerMod;
                result.Speed += item.SpeedMod;
                result.Stamina += item.StaminaMod;
            }
            result.ClampAll();
            return result;
        }

        // --- Lưu / nạp -------------------------------------------------------

        public void LoadFromSave(SaveData data)
        {
            if (data == null) return;
            _owned.Clear();
            _owned.Add(StarterRacketId);
            _owned.Add(StarterShoesId);
            _owned.Add(StarterShirtId);
            if (data.OwnedEquipmentIds != null)
            {
                foreach (var id in data.OwnedEquipmentIds)
                {
                    if (GetItem(id) != null) _owned.Add(id);
                }
            }
            _equipped[EquipmentSlot.Racket] = StarterRacketId;
            _equipped[EquipmentSlot.Shoes] = StarterShoesId;
            _equipped[EquipmentSlot.Shirt] = StarterShirtId;
            if (data.Equipped != null)
            {
                foreach (var e in data.Equipped)
                {
                    EquipmentSlot slot;
                    if (!System.Enum.TryParse(e.Slot, out slot)) continue;
                    if (GetItem(e.ItemId) != null && _owned.Contains(e.ItemId))
                        _equipped[slot] = e.ItemId;
                }
            }
        }

        public void WriteToSave()
        {
            var d = SaveManager.Current;
            d.OwnedEquipmentIds = new List<string>(_owned);
            d.Equipped = new List<EquippedEntry>();
            foreach (var kv in _equipped)
            {
                d.Equipped.Add(new EquippedEntry
                {
                    Slot = kv.Key.ToString(),
                    ItemId = kv.Value,
                });
            }
            SaveManager.Save();
        }
    }
}
