// TrainingManager.cs — ShuttleLab (Unity 6)
//
// 18 bài tập (drill) có mục tiêu ĐO ĐƯỢC: số lần chạm đích / số cú vào
// vùng / thời gian hoàn thành. Mỗi bài: tên, nhóm, mô tả, mục tiêu, đơn vị
// chấm điểm. Kỷ lục lưu qua SaveManager (RecordTrainingBest).
//
// StartDrill chỉ trả về DrillSession = CẤU HÌNH chấm điểm (bài nào, đích
// bao nhiêu, giới hạn bao lâu, kỷ lục hiện tại). Nó KHÔNG tự chạy trận
// đấu, không tự sinh đối thủ — việc chơi bài tập (Practice tích hợp
// drill) là bước sau, dùng DrillSession này làm đầu vào.
//
// Logic thuần, không Unity scene, không MonoBehaviour.
//
// Phụ thuộc: ShuttleLab.Systems (SaveManager). Không phụ thuộc Match/UI.

using System.Collections.Generic;
using UnityEngine;

namespace ShuttleLab.Systems
{
    /// <summary>Đơn vị chấm điểm của một bài tập.</summary>
    public enum DrillUnit
    {
        Reps,       // số lần làm đúng (vd: 10 cú clear vào vùng đích)
        Seconds,    // thời gian hoàn thành (nhỏ hơn = tốt hơn) — xem lưu ý
        Accuracy,   // % trúng đích (0..100)
    }

    /// <summary>Định nghĩa một bài tập.</summary>
    public class DrillDef
    {
        public string Id;
        public string Name;
        public string Category;   // Footwork / Clear / Drop / Smash / Net / Serve / Defence / Stamina
        public string Description;
        public string TargetText; // mục tiêu đọc được, vd "10 cú clear vào vùng đích"
        public int TargetCount;   // đích cần đạt (số lần hoặc %)
        public float TimeLimitSeconds; // 0 = không giới hạn thời gian
        public DrillUnit Unit;

        public DrillDef(string id, string name, string category, string description,
            string targetText, int targetCount, float timeLimitSeconds, DrillUnit unit)
        {
            Id = id;
            Name = name;
            Category = category;
            Description = description;
            TargetText = targetText;
            TargetCount = targetCount;
            TimeLimitSeconds = timeLimitSeconds;
            Unit = unit;
        }
    }

    /// <summary>
    /// Một buổi tập đã bắt đầu (cấu hình chấm điểm). Gameplay dùng nó để
    /// biết khi nào tính điểm, khi nào kết thúc buổi tập.
    /// </summary>
    public class DrillSession
    {
        public DrillDef Drill;
        public int TargetCount;
        public float TimeLimitSeconds;
        public float BestScore;   // kỷ lục đã lưu (0 = chưa có)
        public string ScoringText;

        public DrillSession(DrillDef drill)
        {
            Drill = drill;
            TargetCount = drill.TargetCount;
            TimeLimitSeconds = drill.TimeLimitSeconds;
            BestScore = SaveManager.GetTrainingBest(drill.Id);
            ScoringText = drill.Unit == DrillUnit.Accuracy
                ? $"Đạt {drill.TargetCount}% trúng đích. Kỷ lục: {BestScore:0}%."
                : drill.Unit == DrillUnit.Seconds
                    ? $"Hoàn thành trong {drill.TargetCount}s. Kỷ lục: {(BestScore > 0 ? BestScore.ToString("0.0") + "s" : "chưa có")}."
                    : $"Làm đúng {drill.TargetCount} lần. Kỷ lục: {BestScore:0} lần.";
        }
    }

    /// <summary>Kho định nghĩa 18 bài tập + API mở buổi tập.</summary>
    public static class TrainingManager
    {
        public static readonly string[] Categories =
        {
            "Footwork", "Clear", "Drop", "Smash",
            "Net", "Serve", "Defence", "Stamina",
        };

        // LƯU Ý TRUNG THỰC: các bài Unit=Seconds chấm theo thời gian hoàn
        // thành — nhỏ hơn là tốt hơn, nên RecordBest cho nhóm này so sánh
        // ngược (xem RecordBest). Mọi bài khác: lớn hơn là tốt hơn.
        private static readonly DrillDef[] Drills =
        {
            // Footwork (2)
            new DrillDef("footwork_corners", "Chạy 4 góc cơ bản", "Footwork",
                "Di chuyển chạm đủ 4 góc sân rồi về vị trí giữa, lặp lại.",
                "Chạm đủ 4 góc 5 lượt trong 60 giây", 5, 60f, DrillUnit.Seconds),
            new DrillDef("footwork_split", "Split-step phản xạ", "Footwork",
                "Bật split-step ngay khi đối thủ chạm cầu, rồi lao đúng hướng.",
                "Phản ứng đúng hướng 10 lần liên tiếp", 10, 0f, DrillUnit.Reps),

            // Clear (2)
            new DrillDef("clear_deep", "Clear sâu cuối sân", "Clear",
                "Đánh clear cao, sâu vào vùng cuối sân đối thủ.",
                "10 cú clear vào vùng đích cuối sân", 10, 0f, DrillUnit.Reps),
            new DrillDef("clear_cross", "Clear chéo sân", "Clear",
                "Clear chéo từ góc này sang góc đối diện cuối sân.",
                "8 cú clear chéo vào vùng đích", 8, 0f, DrillUnit.Reps),

            // Drop (2)
            new DrillDef("drop_tight", "Drop sát lưới", "Drop",
                "Bỏ nhỏ rơi sát lưới, cầu vừa qua lưới là rơi.",
                "10 cú drop rơi trong 0,5m sau lưới", 10, 0f, DrillUnit.Reps),
            new DrillDef("drop_slice", "Drop cắt chéo", "Drop",
                "Cắt cầu chéo sang góc đối diện gần lưới.",
                "8 cú drop chéo vào vùng đích", 8, 0f, DrillUnit.Reps),

            // Smash (3)
            new DrillDef("smash_straight", "Smash dọc biên", "Smash",
                "Đập dọc biên, cầu cắm xuống gần đường biên.",
                "10 cú smash dọc biên vào vùng đích", 10, 0f, DrillUnit.Reps),
            new DrillDef("smash_cross", "Smash chéo sân", "Smash",
                "Đập chéo sang góc đối diện.",
                "8 cú smash chéo vào vùng đích", 8, 0f, DrillUnit.Reps),
            new DrillDef("smash_jump", "Nhảy smash", "Smash",
                "Bật nhảy đập cầu ở điểm cao nhất.",
                "10 cú nhảy smash trúng đích", 10, 0f, DrillUnit.Reps),

            // Net (3)
            new DrillDef("net_tumble", "Bỏ nhỏ xoay lưới", "Net",
                "Bỏ nhỏ để cầu xoay sát lưới rồi rơi.",
                "10 cú bỏ nhỏ xoay lưới thành công", 10, 0f, DrillUnit.Reps),
            new DrillDef("net_kill", "Chặn lưới ghi điểm", "Net",
                "Chặn cầu cao gần lưới, đập xuống sân đối thủ.",
                "10 cú chặn lưới ghi điểm", 10, 0f, DrillUnit.Reps),
            new DrillDef("net_lift", "Hất cầu cứu lưới", "Net",
                "Hất cầu từ sát lưới lên cao sâu cuối sân.",
                "10 cú hất cầu vào vùng đích", 10, 0f, DrillUnit.Reps),

            // Serve (3)
            new DrillDef("serve_low", "Giao cầu thấp", "Serve",
                "Giao thấp, sát lưới, vào đúng ô giao chéo.",
                "10 cú giao thấp vào ô đích", 10, 0f, DrillUnit.Reps),
            new DrillDef("serve_high", "Giao cầu cao sâu", "Serve",
                "Giao cao, sâu về cuối sân đối thủ.",
                "10 cú giao cao vào vùng đích", 10, 0f, DrillUnit.Reps),
            new DrillDef("serve_flick", "Giao cầu búng", "Serve",
                "Búng nhanh về cuối sân khi đối thủ đứng sát lưới.",
                "8 cú giao búng vào vùng đích", 8, 0f, DrillUnit.Reps),

            // Defence (2)
            new DrillDef("defence_block", "Chặn smash", "Defence",
                "Chặn smash của đối thủ, đưa cầu qua lưới an toàn.",
                "Chặn thành công 10 cú smash liên tiếp", 10, 0f, DrillUnit.Reps),
            new DrillDef("defence_drive", "Phản đòn drive", "Defence",
                "Đỡ drive rồi phản drive nhanh về cuối sân.",
                "10 cú phản drive vào vùng đích", 10, 0f, DrillUnit.Reps),

            // Stamina (1)
            new DrillDef("stamina_rally", "Rally bền 30 cú", "Stamina",
                "Giữ rally liên tục 30 cú không lỗi, không nghỉ.",
                "Hoàn thành rally 30 cú không lỗi", 30, 0f, DrillUnit.Reps),
        };

        public static int DrillCount => Drills.Length; // 18

        public static IReadOnlyList<DrillDef> ListDrills() => Drills;

        public static List<DrillDef> ListByCategory(string category)
        {
            var list = new List<DrillDef>();
            foreach (var d in Drills)
            {
                if (d.Category == category) list.Add(d);
            }
            return list;
        }

        public static DrillDef GetDrill(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var d in Drills)
            {
                if (d.Id == id) return d;
            }
            return null;
        }

        /// <summary>
        /// Mở một buổi tập: trả về cấu hình chấm điểm. Null nếu id sai.
        /// (Chưa chạy gameplay — Practice tích hợp drill sẽ dùng DrillSession.)
        /// </summary>
        public static DrillSession StartDrill(string id)
        {
            var def = GetDrill(id);
            return def != null ? new DrillSession(def) : null;
        }

        /// <summary>
        /// Ghi điểm buổi tập. Trả true nếu là kỷ lục mới. Bài Seconds:
        /// thời gian NHỎ hơn mới là kỷ lục (so sánh ngược, không dùng
        /// SaveManager.RecordTrainingBest vì nó chỉ giữ số lớn hơn).
        /// </summary>
        public static bool RecordBest(string drillId, float score)
        {
            var def = GetDrill(drillId);
            if (def == null) return false;
            if (def.Unit == DrillUnit.Seconds)
            {
                float best = SaveManager.GetTrainingBest(drillId);
                if (score <= 0) return false;
                if (best <= 0f || score < best)
                {
                    var entry = SaveManager.Current.TrainingBests
                        .Find(e => e.DrillId == drillId);
                    if (entry == null)
                        SaveManager.Current.TrainingBests.Add(
                            new TrainingBestEntry { DrillId = drillId, BestScore = score });
                    else
                        entry.BestScore = score;
                    SaveManager.Save();
                    return true;
                }
                return false;
            }
            return SaveManager.RecordTrainingBest(drillId, score);
        }

        // --- Đổi điểm tập lấy chỉ số (PROGRESSION_SPEC mục 4) --------------

        /// <summary>Chỉ số có thể tăng bằng điểm tập (khớp tên field PlayerStats).</summary>
        public enum TrainableStat
        {
            Power,
            Speed,
            Stamina,
            Accuracy,
            Defense,
        }

        public const int PointsPerBonus = 10; // 10 điểm tập = +1 chỉ số
        public const int MaxTotalBonus = 10;  // tối đa +10 chỉ số cộng dồn

        /// <summary>
        /// Đổi 10 điểm tập lấy +1 chỉ số tự chọn (tối đa +10 cộng dồn mọi
        /// chỉ số). Trả false nếu chưa đủ điểm, đã chạm cap, hoặc career
        /// không Active. Không có UI — UI sau này gọi method này.
        /// Điểm cộng lưu trong PlayerPrefs (khóa shuttlelab_tp_bonus_*);
        /// áp vào PlayerStats lúc bắt đầu trận ở GameBootstrap.Start
        /// (cộng vào chỉ số gốc trước khi áp trang bị).
        /// </summary>
        public static bool ConvertPoints(TrainableStat stat, CareerManager career)
        {
            if (career == null || !career.Active) return false;
            if (TotalBonus() >= MaxTotalBonus) return false;
            if (!career.SpendTrainingPoints(PointsPerBonus)) return false;
            string key = BonusKey(stat);
            PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 0) + 1);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Điểm cộng đã mua cho một chỉ số (0 nếu chưa mua).</summary>
        public static int GetStatBonus(TrainableStat stat) =>
            PlayerPrefs.GetInt(BonusKey(stat), 0);

        /// <summary>Tổng điểm cộng đã mua mọi chỉ số (cap MaxTotalBonus).</summary>
        public static int TotalBonus()
        {
            int total = 0;
            foreach (TrainableStat stat in System.Enum.GetValues(typeof(TrainableStat)))
                total += GetStatBonus(stat);
            return total;
        }

        /// <summary>Xóa hết điểm cộng chỉ số đã đổi — dùng khi bắt đầu sự
        /// nghiệp mới, để bonus cũ không theo sang sự nghiệp mới.</summary>
        public static void ClearAllBonuses()
        {
            foreach (TrainableStat stat in System.Enum.GetValues(typeof(TrainableStat)))
                PlayerPrefs.DeleteKey(BonusKey(stat));
            PlayerPrefs.Save();
        }

        private static string BonusKey(TrainableStat stat) =>
            "shuttlelab_tp_bonus_" + stat.ToString().ToLowerInvariant();
    }
}
