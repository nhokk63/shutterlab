// WeeklyMissions.cs — ShuttleLab (Unity 6)
//
// Nhiệm vụ tuần: 3 nhiệm vụ xoay theo tuần ISO, thưởng xu vào Career.
//  - Thắng 3 trận trong tuần -> +100 xu
//  - Đập 10 cú smash (chỉ tính của user) trong tuần -> +200 xu
//  - Thắng 1 trận không thua ván nào -> +300 xu
//
// Tiến trình ghi qua RecordMatch(won, smashes, gamesLost, career) —
// MatchManager gọi ở FinishMatch (trận tập IsPractice không tính).
// Thưởng trả NGAY khi nhiệm vụ đủ điều kiện (không có bước "nhận"
// riêng), một lần mỗi tuần mỗi nhiệm vụ.
//
// Lưu trữ: gộp vào file save chính (SaveData.Missions) — một file duy
// nhất, dùng chung cơ chế ghi atom + backup của SaveManager. File riêng
// cũ "shuttlelab_missions_v1.json" (nếu có từ bản trước) được xóa một
// lần khi chạy EnsureWeek.
//
// Tiền chỉ sống trong Career: RecordMatch không làm gì khi career null
// hoặc chưa Active (không có CareerManager thì không có chỗ trả thưởng).
//
// Logic thuần, không Unity scene, không MonoBehaviour. Không phụ thuộc
// Match/UI — MatchManager gọi vào, WeeklyMissions không gọi ngược.

using System.IO;
using UnityEngine;

namespace ShuttleLab.Systems
{
    public static class WeeklyMissions
    {
        public const int WinsTarget = 3;
        public const int WinsReward = 100;
        public const int SmashesTarget = 10;
        public const int SmashesReward = 200;
        public const int CleanWinReward = 300;

        // Tên file riêng cũ (đã bỏ) — chỉ để xóa một lần, không đọc nữa.
        private const string LegacyFileName = "shuttlelab_missions_v1.json";
        private static bool _legacyCleaned;

        /// <summary>Dữ liệu nhiệm vụ trong save chính (nguồn duy nhất).</summary>
        private static MissionSaveData Data => SaveManager.Current.Missions;

        // --- API chính -------------------------------------------------------

        /// <summary>
        /// Ghi kết quả một trận thật (MatchManager gọi ở FinishMatch).
        /// won: user thắng trận; smashes: số cú smash CỦA USER trong trận
        /// (Stats.UserSmashes); gamesLost: số ván user thua
        /// (Rules.Score.Games[1]). Không làm gì khi career null hoặc chưa
        /// Active. Trả thưởng ngay khi nhiệm vụ vừa đủ điều kiện.
        /// </summary>
        public static void RecordMatch(bool won, int smashes, int gamesLost,
            CareerManager career)
        {
            if (career == null || !career.Active) return;
            EnsureWeek();

            var data = Data;
            if (won) data.Wins++;
            if (smashes > 0) data.Smashes += smashes;
            if (won && gamesLost <= 0) data.CleanWinDone = true;

            GrantDueRewards(career);
            SaveManager.Save();
        }

        /// <summary>Trạng thái nhiệm vụ tuần hiện tại, để UI sau này hiển thị.</summary>
        public static string GetStatusText()
        {
            EnsureWeek();
            var data = Data;
            string winsState = data.WinsRewarded
                ? "hoàn thành — đã nhận +100 xu"
                : $"tiến độ {data.Wins}/{WinsTarget}";
            string smashState = data.SmashesRewarded
                ? "hoàn thành — đã nhận +200 xu"
                : $"tiến độ {data.Smashes}/{SmashesTarget}";
            string cleanState = data.CleanWinRewarded
                ? "hoàn thành — đã nhận +300 xu"
                : "chưa xong";
            return $"Nhiệm vụ tuần ({data.WeekKey}):\n"
                + $"• Thắng 3 trận: {winsState}\n"
                + $"• Đập 10 cú smash: {smashState}\n"
                + $"• Thắng 1 trận không thua ván: {cleanState}";
        }

        // --- Tiến trình (đọc cho UI sau này) ---------------------------------

        public static int WinsProgress { get { EnsureWeek(); return Data.Wins; } }
        public static int SmashesProgress { get { EnsureWeek(); return Data.Smashes; } }
        public static bool CleanWinDone { get { EnsureWeek(); return Data.CleanWinDone; } }
        public static bool WinsRewarded { get { EnsureWeek(); return Data.WinsRewarded; } }
        public static bool SmashesRewarded { get { EnsureWeek(); return Data.SmashesRewarded; } }
        public static bool CleanWinRewarded { get { EnsureWeek(); return Data.CleanWinRewarded; } }

        // --- Tuần ISO 8601 ----------------------------------------------------

        /// <summary>
        /// Khóa tuần hiện tại dạng "2026-W46". Tuần chứa thứ Năm quyết định
        /// năm ISO (chuẩn ISO 8601): thứ Hai = 0 .. Chủ nhật = 6.
        /// </summary>
        public static string CurrentWeekKey()
        {
            System.DateTime today = System.DateTime.Today;
            int mondayBasedDay = ((int)today.DayOfWeek + 6) % 7;
            System.DateTime thursday = today.AddDays(3 - mondayBasedDay);
            int isoYear = thursday.Year;
            return isoYear + "-W" + CurrentWeekNumber().ToString("00");
        }

        /// <summary>Số tuần ISO hiện tại (1..53), cùng chuẩn với CurrentWeekKey.</summary>
        public static int CurrentWeekNumber()
        {
            System.DateTime today = System.DateTime.Today;
            int mondayBasedDay = ((int)today.DayOfWeek + 6) % 7;
            System.DateTime thursday = today.AddDays(3 - mondayBasedDay);
            return (thursday.DayOfYear - 1) / 7 + 1;
        }

        // --- Nội bộ -----------------------------------------------------------

        private static void GrantDueRewards(CareerManager career)
        {
            var data = Data;
            if (data.Wins >= WinsTarget && !data.WinsRewarded)
            {
                career.AddMoney(WinsReward);
                data.WinsRewarded = true;
            }
            if (data.Smashes >= SmashesTarget && !data.SmashesRewarded)
            {
                career.AddMoney(SmashesReward);
                data.SmashesRewarded = true;
            }
            if (data.CleanWinDone && !data.CleanWinRewarded)
            {
                career.AddMoney(CleanWinReward);
                data.CleanWinRewarded = true;
            }
        }

        /// <summary>Sang tuần mới: reset tiến trình + cờ thưởng, ghi lại.</summary>
        private static void EnsureWeek()
        {
            if (!_legacyCleaned)
            {
                _legacyCleaned = true;
                try
                {
                    string legacyPath = Path.Combine(
                        Application.persistentDataPath, LegacyFileName);
                    if (File.Exists(legacyPath)) File.Delete(legacyPath);
                    string legacyBackup = legacyPath + ".bak";
                    if (File.Exists(legacyBackup)) File.Delete(legacyBackup);
                }
                catch { /* xóa file cũ thất bại thì bỏ qua */ }
            }

            var data = Data;
            string key = CurrentWeekKey();
            if (data.WeekKey == key) return;
            data.WeekKey = key;
            data.Wins = 0;
            data.Smashes = 0;
            data.CleanWinDone = false;
            data.WinsRewarded = false;
            data.SmashesRewarded = false;
            data.CleanWinRewarded = false;
            SaveManager.Save();
        }
    }
}
