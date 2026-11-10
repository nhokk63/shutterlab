// EquipmentManager.cs — ShuttleLab (Unity 6)
//
// Vợt / giày / áo HƯ CẤU — tên tự đặt (Bão Tố, Phong Vũ, Lôi Đình...),
// KHÔNG dùng thương hiệu thật (Yonex, Victor, Li-Ning, Mizuno...).
// Riêng vợt "quốc dân" lấy cảm hứng từ vợt thật ngoài đời, chỉ đổi nhãn
// hiệu/model một chút để người chơi vẫn nhận ra (Yonexx, Viktor,
// Li-Nang, Mizuna, Apax, Kawasaky) — tên trong game vẫn là hư cấu.
// Mỗi món có modifier nhỏ lên chỉ số (+1..+8, huyền thoại tối đa +8)
// và giá tiền ảo (mua bằng tiền Career), chia 4 bậc hiếm: Thường / Tốt /
// Hiếm / Huyền thoại. Modifier CỐ TÌNH nhỏ: trang bị hỗ trợ nhẹ, không
// bao giờ thay thế kỹ năng — đúng tinh thần "không pay-to-win" của
// ARCHITECTURE.md. Thông số tăng theo giá, luôn có trade-off.
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

    /// <summary>Bậc hiếm đồ (hiển thị màu trong shop).</summary>
    public enum Rarity
    {
        Common,    // Thường — trắng
        Good,      // Tốt — xanh dương
        Rare,      // Hiếm — tím
        Legendary, // Huyền thoại — vàng
    }

    /// <summary>Một món trang bị trong cửa hàng.</summary>
    public class EquipmentItem
    {
        public string Id;
        public string Name;
        public EquipmentSlot Slot;
        public Rarity Rarity;
        public int PowerMod;
        public int SpeedMod;
        public int StaminaMod;
        public int Price; // tiền ảo Career (0 = đồ khởi đầu)
        public string Description;

        public EquipmentItem(string id, string name, EquipmentSlot slot,
            Rarity rarity, int powerMod, int speedMod, int staminaMod, int price, string description)
        {
            Id = id;
            Name = name;
            Slot = slot;
            Rarity = rarity;
            PowerMod = powerMod;
            SpeedMod = speedMod;
            StaminaMod = staminaMod;
            Price = price;
            Description = description;
        }

        public string RarityName()
        {
            switch (Rarity)
            {
                case Rarity.Good: return "Tốt";
                case Rarity.Rare: return "Hiếm";
                case Rarity.Legendary: return "Huyền thoại";
                default: return "Thường";
            }
        }

        public Color RarityColor()
        {
            switch (Rarity)
            {
                case Rarity.Good: return new Color(0.45f, 0.70f, 1f);
                case Rarity.Rare: return new Color(0.75f, 0.50f, 1f);
                case Rarity.Legendary: return new Color(1f, 0.80f, 0.25f);
                default: return Color.white;
            }
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
            // Vợt khởi đầu + dòng hư cấu cũ (giữ nguyên chỉ số/giá).
            new EquipmentItem(StarterRacketId, "Vợt Khởi Đầu", EquipmentSlot.Racket,
                Rarity.Common, 0, 0, 0, 0, "Vợt tập cơ bản, cân bằng."),
            new EquipmentItem("rkt_balance_5", "Cân Bằng 5", EquipmentSlot.Racket,
                Rarity.Common, 1, 1, 1, 500, "Mọi chỉ số +1, dễ làm quen."),
            new EquipmentItem("rkt_wind_77", "Phong Vũ 77", EquipmentSlot.Racket,
                Rarity.Common, 2, 2, 0, 650, "Nhẹ, vung nhanh, đánh linh hoạt."),
            new EquipmentItem("rkt_storm_900", "Bão Tố 900", EquipmentSlot.Racket,
                Rarity.Good, 3, 0, -1, 800, "Đập mạnh nhưng hơi nặng tay."),
            new EquipmentItem("rkt_thunder_x", "Lôi Đình X", EquipmentSlot.Racket,
                Rarity.Rare, 5, -2, 0, 1500, "Sức mạnh tối đa, đánh đổi tốc độ."),
            // Vợt "quốc dân" — lấy cảm hứng từ vợt thật ngoài đời, chỉ đổi
            // nhãn hiệu/model một chút để người chơi vẫn nhận ra (Yonexx ~
            // Yonex, Viktor ~ Victor, Li-Nang ~ Li-Ning, Mizuna ~ Mizuno,
            // Apax ~ Apacs, Kawasaky ~ Kawasaki). KHÔNG dùng thương hiệu
            // thật trong game. Thông số tăng theo giá, luôn có trade-off.
            // Thường (150–600 xu): chỉ số đơn cao nhất +2.
            new EquipmentItem("rkt_muscle_powr_29", "Muscle Powr 29", EquipmentSlot.Racket,
                Rarity.Common, 1, 0, 0, 200, "Vợt cổ điển bền bỉ, ai cũng từng cầm qua."),
            new EquipmentItem("rkt_nanorey_10f", "Nanorey 10F", EquipmentSlot.Racket,
                Rarity.Common, 0, 2, 0, 350, "Nhẹ đầu, vung nhanh, hợp đánh đôi."),
            new EquipmentItem("rkt_windstorme_72", "Windstorme 72", EquipmentSlot.Racket,
                Rarity.Common, 0, 1, 1, 450, "Nhẹ cả cây, chạy bền cả trận."),
            new EquipmentItem("rkt_voltrex_7dg", "Voltrex 7DG", EquipmentSlot.Racket,
                Rarity.Common, 2, -1, 0, 500, "Đầu hơi nặng, đập có lực, giá mềm."),
            new EquipmentItem("rkt_king_k8", "King K8", EquipmentSlot.Racket,
                Rarity.Common, 1, 1, 0, 550, "Cân bằng tốt, giá quốc dân."),
            // Tốt (700–1400 xu): chỉ số đơn cao nhất +3.
            new EquipmentItem("rkt_arcsabre_7pro", "Arcsabre 7 Pro", EquipmentSlot.Racket,
                Rarity.Good, 2, 1, 0, 800, "Kiểm soát tốt, cầu đi đúng ý."),
            new EquipmentItem("rkt_astroz_77pro", "Astroz 77 Pro", EquipmentSlot.Racket,
                Rarity.Good, 3, -1, 0, 1000, "Đập uy lực, hơi chậm một nhịp."),
            new EquipmentItem("rkt_nanoflair_800pro", "Nanoflair 800 Pro", EquipmentSlot.Racket,
                Rarity.Good, 1, 3, 0, 1100, "Vợt tốc độ, thủ cầu như gió."),
            new EquipmentItem("rkt_thrusta_kfalcon", "Thrusta K Falcon", EquipmentSlot.Racket,
                Rarity.Good, 3, 1, -1, 1300, "Cân bằng thiên công, vung đầm tay."),
            new EquipmentItem("rkt_axeforce_90tiger", "Axeforce 90 Tiger", EquipmentSlot.Racket,
                Rarity.Good, 3, 0, 1, 1400, "Đập khỏe mà vẫn bền sức."),
            // Hiếm (1500–2800 xu): chỉ số đơn cao nhất +5.
            new EquipmentItem("rkt_arcsabre_11pro", "Arcsabre 11 Pro", EquipmentSlot.Racket,
                Rarity.Rare, 3, 2, 0, 1600, "Chuẩn kiểm soát của dân chuyên."),
            new EquipmentItem("rkt_astroz_88dpro", "Astroz 88D Pro", EquipmentSlot.Racket,
                Rarity.Rare, 5, -1, 0, 2000, "Vợt đôi công, smash sau lưới nặng trịch."),
            new EquipmentItem("rkt_jetspeede_s12", "Jetspeede S 12", EquipmentSlot.Racket,
                Rarity.Rare, 1, 4, 0, 2000, "Tốc độ cực cao, đánh nhanh thắng nhanh."),
            new EquipmentItem("rkt_halbertex_8000", "Halbertex 8000", EquipmentSlot.Racket,
                Rarity.Rare, 4, 1, 0, 2400, "Công thủ toàn diện, cảm giác cầu rõ."),
            new EquipmentItem("rkt_auraspeede_100x", "Auraspeede 100X", EquipmentSlot.Racket,
                Rarity.Rare, 2, 4, -1, 2800, "Nhanh và hiểm, đổi lại hơi tốn sức."),
            // Huyền thoại (3000–5000 xu): chỉ số đơn cao nhất +8.
            new EquipmentItem("rkt_nanoflair_1000z", "Nanoflair 1000Z", EquipmentSlot.Racket,
                Rarity.Legendary, 2, 6, 0, 3200, "Tốc độ huyền thoại, thủ cầu không góc chết."),
            new EquipmentItem("rkt_astroz_99pro", "Astroz 99 Pro", EquipmentSlot.Racket,
                Rarity.Legendary, 6, -1, 1, 3800, "Cây đập của các tay vợt số 1 thế giới."),
            new EquipmentItem("rkt_thrusta_ryuga2", "Thrusta Ryuga II", EquipmentSlot.Racket,
                Rarity.Legendary, 7, -2, 0, 4200, "Sức mạnh hủy diệt, chỉ dành cho tay to."),
            new EquipmentItem("rkt_axeforce_100", "Axeforce 100", EquipmentSlot.Racket,
                Rarity.Legendary, 6, 1, 1, 4600, "Đỉnh cao dòng tấn công, vừa mạnh vừa bền."),
            new EquipmentItem("rkt_astroz_100zz", "Astroz 100ZZ", EquipmentSlot.Racket,
                Rarity.Legendary, 8, -2, 0, 5000, "Vua của các cây vợt — smash là ghi điểm."),
            // Giày
            new EquipmentItem(StarterShoesId, "Giày Khởi Đầu", EquipmentSlot.Shoes,
                Rarity.Common, 0, 0, 0, 0, "Giày tập cơ bản."),
            new EquipmentItem("sho_grip_pro", "Bám Sân Pro", EquipmentSlot.Shoes,
                Rarity.Good, 0, 3, 0, 400, "Đế bám sân, đổi hướng nhanh."),
            new EquipmentItem("sho_light_air", "Nhẹ Nhàng Air", EquipmentSlot.Shoes,
                Rarity.Good, 0, 2, 1, 550, "Nhẹ chân, di chuyển bền hơn."),
            new EquipmentItem("sho_court_king", "Vua Sân Đấu", EquipmentSlot.Shoes,
                Rarity.Rare, 0, 4, -1, 900, "Tốc độ tối đa, hơi tốn sức."),
            // Áo
            new EquipmentItem(StarterShirtId, "Áo Khởi Đầu", EquipmentSlot.Shirt,
                Rarity.Common, 0, 0, 0, 0, "Áo tập cơ bản."),
            new EquipmentItem("shr_dry_cool", "Thoáng Mát Dry", EquipmentSlot.Shirt,
                Rarity.Common, 0, 0, 2, 250, "Thoát mồ hôi, đỡ mệt hơn."),
            new EquipmentItem("shr_aero_fit", "Aero Fit", EquipmentSlot.Shirt,
                Rarity.Good, 0, 1, 2, 450, "Ôm người, chạy nhanh, bền hơn."),
            new EquipmentItem("shr_champion", "Nhà Vô Địch", EquipmentSlot.Shirt,
                Rarity.Rare, 1, 0, 3, 800, "Thể lực tốt, tự tin hơn khi đập."),
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

        /// <summary>Trả shop về trạng thái đầu (chỉ 3 món khởi đầu) — dùng
        /// khi bắt đầu sự nghiệp mới, để đồ đã mua ở sự nghiệp cũ không
        /// theo sang sự nghiệp mới.</summary>
        public void ResetToStarters()
        {
            _owned.Clear();
            _owned.Add(StarterRacketId);
            _owned.Add(StarterShoesId);
            _owned.Add(StarterShirtId);
            _equipped[EquipmentSlot.Racket] = StarterRacketId;
            _equipped[EquipmentSlot.Shoes] = StarterShoesId;
            _equipped[EquipmentSlot.Shirt] = StarterShirtId;
            WriteToSave();
        }

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
