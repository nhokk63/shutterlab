// CareerManager.cs — ShuttleLab (Unity 6)
//
// Sự nghiệp theo tuần: 1 tuần = tập luyện (3 tuần) + giải đấu (tuần thứ 4).
// Giải giả tưởng theo cấp Super 100 -> 300 -> 500 -> 750 -> 1000 với TÊN
// GIẢI HƯ CẤU (Cúp Sao Mai, Cúp Sông Hồng, Cúp Hoa Sen, Cúp Rồng Vàng,
// Cúp Vô Địch ShuttleLab) — KHÔNG dùng tên giải BWF thật (All England,
// Indonesia Open, Malaysia Open...). Điểm ranking, tiền thưởng ảo, thăng
// hạng khi vô địch một cấp.
//
// Logic thuần, không Unity scene, không MonoBehaviour. Kết quả trận đấu
// (thắng/thua, thứ hạng đạt được) được đưa vào qua
// RecordTournamentResult — CareerManager KHÔNG tự bịa kết quả trận.
// Trận của user do MatchManager + TournamentManager xử lý.
//
// Công thức hạng (v0.1.0, ghi rõ để cân bằng sau):
//   Rank = max(1, 512 - RankingPoints / 2)
//   (vô địch Super 100: +100 điểm -> hạng ~462; vô địch Super 1000:
//   +1000 điểm -> hạng ~12.)
//
// RÚT GỌN v0.1.0:
//  - Không có mùa giải (52 tuần rồi reset) — tuần cứ tăng, ghi chú sau.
//  - Điểm tập luyện (TrainingPoints) chỉ là số tích lũy hiển thị; chưa
//    đổi thành tăng chỉ số vì chưa có luồng chơi bài tập thật để chấm.
//  - Tiền thưởng chỉ là tiền ảo trong Career, không mua được gì ngoài
//    cửa hàng trang bị (EquipmentManager).
//
// Phụ thuộc: ShuttleLab.Systems (SaveManager). Không phụ thuộc Match/UI.

using System;
using UnityEngine;

namespace ShuttleLab.Systems
{
    /// <summary>Cấp giải đấu (tăng dần). Thứ tự enum = thứ tự thăng hạng.</summary>
    public enum TournamentTier
    {
        Super100 = 0,
        Super300 = 1,
        Super500 = 2,
        Super750 = 3,
        Super1000 = 4,
    }

    /// <summary>Thứ hạng đạt được trong một giải (do TournamentManager báo về).</summary>
    public enum TournamentPlacement
    {
        EarlyExit,      // thua trước tứ kết
        Quarterfinalist,
        Semifinalist,
        Finalist,
        Champion,
    }

    /// <summary>Thông tin tĩnh của một cấp giải (tên hư cấu, điểm, tiền).</summary>
    public struct TierInfo
    {
        public readonly TournamentTier Tier;
        public readonly string EventName;
        public readonly int WinPoints;
        public readonly int PrizeMoney;

        public TierInfo(TournamentTier tier, string eventName, int winPoints, int prizeMoney)
        {
            Tier = tier;
            EventName = eventName;
            WinPoints = winPoints;
            PrizeMoney = prizeMoney;
        }
    }

    /// <summary>
    /// Chủ sở hữu trạng thái sự nghiệp. Một CareerManager = một sự nghiệp.
    /// Tạo bằng new, nạp từ SaveManager, ghi lại sau mỗi thay đổi.
    /// </summary>
    public class CareerManager
    {
        public const int StartRank = 512;
        public const int StartMoney = 500;
        public const int WeeksPerCycle = 4; // 3 tuần tập + 1 tuần giải

        private static readonly TierInfo[] Tiers =
        {
            new TierInfo(TournamentTier.Super100,  "Cúp Sao Mai",          100,  200),
            new TierInfo(TournamentTier.Super300,  "Cúp Sông Hồng",        300,  500),
            new TierInfo(TournamentTier.Super500,  "Cúp Hoa Sen",          500, 1000),
            new TierInfo(TournamentTier.Super750,  "Cúp Rồng Vàng",        750, 2000),
            new TierInfo(TournamentTier.Super1000, "Cúp Vô Địch ShuttleLab", 1000, 5000),
        };

        public static TierInfo InfoForTier(TournamentTier tier) =>
            Tiers[(int)tier];

        public static TierInfo InfoForTierIndex(int index) =>
            Tiers[Mathf.Clamp(index, 0, Tiers.Length - 1)];

        public static int TierCount => Tiers.Length;

        public bool Active { get; private set; }
        public int Week { get; private set; }
        public int RankingPoints { get; private set; }
        public int Money { get; private set; }
        public int Wins { get; private set; }
        public int Losses { get; private set; }
        public int Titles { get; private set; }
        public int CurrentTierIndex { get; private set; }
        public int TrainingPoints { get; private set; }
        public int TournamentsPlayed { get; private set; }

        /// <summary>Hạng hiện tại theo công thức ghi ở đầu file.</summary>
        public int Ranking => Mathf.Max(1, StartRank - RankingPoints / 2);

        public TournamentTier CurrentTier => (TournamentTier)CurrentTierIndex;
        public TierInfo CurrentTierInfo => InfoForTierIndex(CurrentTierIndex);

        /// <summary>Tuần này có phải tuần giải đấu không (tuần chia hết cho 4).</summary>
        public bool IsTournamentWeek => Active && Week > 0 && Week % WeeksPerCycle == 0;

        /// <summary>Giải sắp tới: giải của tuần giải gần nhất phía trước.</summary>
        public TierInfo NextTournamentInfo => CurrentTierInfo;

        /// <summary>Số tuần nữa tới tuần giải (0 = đang là tuần giải).</summary>
        public int WeeksUntilTournament =>
            !Active ? WeeksPerCycle : (WeeksPerCycle - (Week % WeeksPerCycle)) % WeeksPerCycle;

        public event Action<int> OnWeekAdvanced; // tuần mới

        public CareerManager()
        {
            Active = false;
        }

        /// <summary>Bắt đầu sự nghiệp mới (xóa tiến trình cũ trong bộ nhớ).</summary>
        public void StartNewCareer()
        {
            Active = true;
            Week = 1;
            RankingPoints = 0;
            Money = StartMoney;
            Wins = 0;
            Losses = 0;
            Titles = 0;
            CurrentTierIndex = 0;
            TrainingPoints = 0;
            TournamentsPlayed = 0;
            WriteToSave();
        }

        /// <summary>
        /// Sang tuần mới. Tuần tập: +2 điểm tập luyện, +50 tiền trợ cấp.
        /// Tuần giải: chỉ đánh dấu (kết quả giải vào qua
        /// RecordTournamentResult). Trả về true nếu tuần mới là tuần giải.
        /// </summary>
        public bool AdvanceWeek()
        {
            if (!Active) return false;
            Week++;
            if (IsTournamentWeek)
            {
                TournamentsPlayed++;
            }
            else
            {
                TrainingPoints += 2;
                Money += 50; // trợ cấp tập luyện hàng tuần
            }
            WriteToSave();
            OnWeekAdvanced?.Invoke(Week);
            return IsTournamentWeek;
        }

        /// <summary>
        /// Ghi kết quả một giải đã đánh xong. matchesWon/Lost đếm theo
        /// TournamentManager. Vô địch -> thăng một cấp (tối đa Super 1000)
        /// và +1 danh hiệu. Điểm/tiền theo thứ hạng đạt được.
        /// </summary>
        public void RecordTournamentResult(TournamentPlacement placement,
            TournamentTier tier, int matchesWon, int matchesLost)
        {
            if (!Active) return;
            var info = InfoForTier(tier);
            Wins += Mathf.Max(0, matchesWon);
            Losses += Mathf.Max(0, matchesLost);

            float pointFactor;
            float moneyFactor;
            switch (placement)
            {
                case TournamentPlacement.Champion:
                    pointFactor = 1.00f; moneyFactor = 1.00f; break;
                case TournamentPlacement.Finalist:
                    pointFactor = 0.60f; moneyFactor = 0.50f; break;
                case TournamentPlacement.Semifinalist:
                    pointFactor = 0.35f; moneyFactor = 0.25f; break;
                case TournamentPlacement.Quarterfinalist:
                    pointFactor = 0.20f; moneyFactor = 0.125f; break;
                default:
                    pointFactor = 0.05f; moneyFactor = 0f; break;
            }
            RankingPoints += Mathf.RoundToInt(info.WinPoints * pointFactor);
            Money += Mathf.RoundToInt(info.PrizeMoney * moneyFactor);

            if (placement == TournamentPlacement.Champion)
            {
                Titles++;
                if (CurrentTierIndex < Tiers.Length - 1)
                    CurrentTierIndex++;
            }
            WriteToSave();
        }

        /// <summary>Trừ tiền ảo (mua trang bị). Trả false nếu không đủ tiền.</summary>
        public bool SpendMoney(int amount)
        {
            if (amount < 0) amount = 0;
            if (Money < amount) return false;
            Money -= amount;
            WriteToSave();
            return true;
        }

        public void AddMoney(int amount)
        {
            if (amount <= 0) return;
            Money += amount;
            WriteToSave();
        }

        /// <summary>Thêm điểm tập luyện (gọi khi hoàn thành bài tập có chấm điểm).</summary>
        public void AddTrainingPoints(int points)
        {
            if (points <= 0) return;
            TrainingPoints += points;
            WriteToSave();
        }

        public string SummaryLine() =>
            Active
                ? $"Tuần {Week} • Hạng {Ranking} • {RankingPoints} điểm • {Money} xu • {Titles} danh hiệu"
                : "Chưa bắt đầu sự nghiệp";

        // --- Lưu / nạp -------------------------------------------------------

        public void LoadFromSave(SaveData data)
        {
            if (data == null || data.Career == null) return;
            var c = data.Career;
            Active = c.Active;
            Week = c.Week;
            RankingPoints = c.RankingPoints;
            Money = c.Money;
            Wins = c.Wins;
            Losses = c.Losses;
            Titles = c.Titles;
            CurrentTierIndex = Mathf.Clamp(c.CurrentTierIndex, 0, Tiers.Length - 1);
            TrainingPoints = c.TrainingPoints;
            TournamentsPlayed = c.TournamentsPlayed;
        }

        public void WriteToSave()
        {
            var c = SaveManager.Current.Career;
            c.Active = Active;
            c.Week = Week;
            c.RankingPoints = RankingPoints;
            c.Money = Money;
            c.Wins = Wins;
            c.Losses = Losses;
            c.Titles = Titles;
            c.CurrentTierIndex = CurrentTierIndex;
            c.TrainingPoints = TrainingPoints;
            c.TournamentsPlayed = TournamentsPlayed;
            SaveManager.Save();
        }
    }
}
