// TournamentManager.cs — ShuttleLab (Unity 6)
//
// Bracket loại trực tiếp 8 / 16 / 32 người, seeding theo hạng.
//  - Trận AI-vs-AI: MÔ PHỎNG bằng System.Random có seed, công thức thắng
//    ghi rõ bên dưới (hạt giống cao hơn thì mạnh hơn). Đây là mô phỏng,
//    KHÔNG phải trận đấu thật — UI phải ghi rõ "kết quả mô phỏng" nếu
//    hiển thị các trận AI.
//  - Trận của user: KHÔNG mô phỏng. User đánh thật qua MatchManager;
//    kết quả thật được đưa vào bằng ReportPlayerMatch(won). Cho tới khi
//    GameBootstrap nối MatchManager.OnMatchEnd -> ReportPlayerMatch,
//    bracket chỉ cập nhật khi CareerUI (hoặc code gọi) báo kết quả.
//
// Logic thuần, không Unity scene, không MonoBehaviour. Không chạm điểm
// số trận đấu (điểm số thuộc MatchRules/MatchManager).
//
// Phụ thuộc: ShuttleLab.Systems (CareerManager.TournamentPlacement).
// Không phụ thuộc Match/Players.
//
// RÚT GỌN v0.1.0:
//  - Bracket chỉ sống trong bộ nhớ (không lưu vào SaveManager) — thoát
//    scene Career giữa giải thì bracket mất, phải tạo lại ở tuần giải.
//  - Không có trận tranh hạng 3.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShuttleLab.Systems
{
    /// <summary>Vòng đấu hiện tại của user trong giải.</summary>
    public enum TournamentRound
    {
        RoundOf32,
        RoundOf16,
        Quarterfinal,
        Semifinal,
        Final,
        Champion,
        Eliminated,
    }

    /// <summary>Một tay vợt trong bracket (user hoặc AI hư cấu).</summary>
    [Serializable]
    public class Entrant
    {
        public string Name;
        public int Seed;        // 1 = hạt giống mạnh nhất
        public bool IsPlayer;
        public int Ranking;     // hạng hiển thị (chỉ để seeding/xếp hạng)

        public Entrant(string name, int seed, bool isPlayer, int ranking)
        {
            Name = name;
            Seed = seed;
            IsPlayer = isPlayer;
            Ranking = ranking;
        }
    }

    /// <summary>Một cặp đấu trong một vòng.</summary>
    public class BracketMatch
    {
        public Entrant A;
        public Entrant B;
        public Entrant Winner;  // null = chưa đánh
        public bool Played => Winner != null;
        public bool IsPlayerMatch => (A != null && A.IsPlayer) || (B != null && B.IsPlayer);
    }

    /// <summary>Bracket loại trực tiếp. Tạo bằng CreateBracket, mô phỏng các
    /// trận AI bằng SimulateRound, báo kết quả trận của user bằng
    /// ReportPlayerMatch.</summary>
    public class TournamentManager
    {
        // Tên AI HƯ CẤU (không phải tay vợt thật ngoài đời). Đủ cho bracket 32.
        private static readonly string[] FictionalNames =
        {
            "Minh Quân", "Hải Đăng", "An Nhiên", "Tuấn Anh", "Phương Linh",
            "Đức Huy", "Thu Hằng", "Quốc Bảo", "Lan Anh", "Văn Thắng",
            "Ngọc Trâm", "Hoàng Nam", "Diệu Linh", "Thanh Sơn", "Kim Ngân",
            "Bảo Long", "Hồng Nhung", "Mạnh Cường", "Thùy Dương", "Đình Phúc",
            "Mai Phương", "Gia Hưng", "Thanh Thảo", "Nhật Minh", "Quỳnh Anh",
            "Thế Vinh", "Bích Ngọc", "Hữu Phước", "Tuyết Mai", "Công Danh",
            "Xuân Trường",
        };

        public int BracketSize { get; private set; }
        public bool IsActive { get; private set; }
        public TournamentRound CurrentRound { get; private set; }
        public Entrant Player { get; private set; }
        public int PlayerMatchesWon { get; private set; }
        public int PlayerMatchesLost { get; private set; }

        public bool IsPlayerEliminated => CurrentRound == TournamentRound.Eliminated;
        public bool IsPlayerChampion => CurrentRound == TournamentRound.Champion;

        /// <summary>Thứ hạng user đạt được tới lúc này (dùng cho CareerManager).
        /// Tính theo vòng xa nhất user tới được, không theo vòng hiện tại
        /// (thua ở bán kết vẫn là Semifinalist, không phải EarlyExit).</summary>
        public TournamentPlacement PlayerPlacement
        {
            get
            {
                if (IsPlayerChampion) return TournamentPlacement.Champion;
                switch (_playerBestRound)
                {
                    case TournamentRound.Final: return TournamentPlacement.Finalist;
                    case TournamentRound.Semifinal: return TournamentPlacement.Semifinalist;
                    case TournamentRound.Quarterfinal: return TournamentPlacement.Quarterfinalist;
                    default: return TournamentPlacement.EarlyExit;
                }
            }
        }

        private List<BracketMatch> _currentRoundMatches = new List<BracketMatch>();
        private List<Entrant> _alive = new List<Entrant>();
        private TournamentRound _playerBestRound;
        private System.Random _rng;

        public TournamentManager()
        {
            IsActive = false;
            CurrentRound = TournamentRound.RoundOf32;
        }

        /// <summary>
        /// Tạo bracket mới. size chỉ nhận 8/16/32 (khác thì kẹp về gần nhất).
        /// playerSeed 1..size (1 mạnh nhất). rngSeed cố định -> các trận AI
        /// mô phỏng lại được cùng kết quả với cùng seed.
        /// </summary>
        public void CreateBracket(int size, string playerName, int playerSeed, int rngSeed)
        {
            if (size <= 8) BracketSize = 8;
            else if (size <= 16) BracketSize = 16;
            else BracketSize = 32;

            _rng = new System.Random(rngSeed);
            _alive = new List<Entrant>();
            PlayerMatchesWon = 0;
            PlayerMatchesLost = 0;

            playerSeed = Mathf.Clamp(playerSeed, 1, BracketSize);
            Player = new Entrant(
                string.IsNullOrEmpty(playerName) ? "Bạn" : playerName,
                playerSeed, isPlayer: true, ranking: playerSeed);

            // Hạt giống: user giữ seed của mình, AI còn lại lấp các seed khác.
            var names = new List<string>(FictionalNames);
            Shuffle(names);
            int nameIdx = 0;
            for (int seed = 1; seed <= BracketSize; seed++)
            {
                if (seed == playerSeed)
                {
                    _alive.Add(Player);
                }
                else
                {
                    string n = names[nameIdx % names.Count];
                    nameIdx++;
                    _alive.Add(new Entrant(n, seed, isPlayer: false, ranking: seed));
                }
            }

            CurrentRound = FirstRoundForSize(BracketSize);
            _playerBestRound = CurrentRound;
            BuildRoundMatches();
            IsActive = true;
        }

        private static TournamentRound FirstRoundForSize(int size)
        {
            switch (size)
            {
                case 8: return TournamentRound.Quarterfinal;
                case 16: return TournamentRound.RoundOf16;
                default: return TournamentRound.RoundOf32;
            }
        }

        private void Shuffle(List<string> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                string tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        private void BuildRoundMatches()
        {
            _currentRoundMatches = new List<BracketMatch>();
            // Ghép hạt giống cao vs thấp (1 vs N, 2 vs N-1, ...) — chuẩn bracket.
            var ordered = new List<Entrant>(_alive);
            ordered.Sort((a, b) => a.Seed.CompareTo(b.Seed));
            int n = ordered.Count;
            for (int i = 0; i < n / 2; i++)
            {
                _currentRoundMatches.Add(new BracketMatch
                {
                    A = ordered[i],
                    B = ordered[n - 1 - i],
                });
            }
        }

        /// <summary>Tên vòng hiện tại để hiển thị (QF/SF/F...).</summary>
        public string CurrentRoundName()
        {
            switch (CurrentRound)
            {
                case TournamentRound.RoundOf32: return "Vòng 1/16";
                case TournamentRound.RoundOf16: return "Vòng 1/8";
                case TournamentRound.Quarterfinal: return "Tứ kết";
                case TournamentRound.Semifinal: return "Bán kết";
                case TournamentRound.Final: return "Chung kết";
                case TournamentRound.Champion: return "Vô địch";
                default: return "Đã bị loại";
            }
        }

        /// <summary>Đối thủ tiếp theo của user (null nếu user đã xong giải).</summary>
        public Entrant NextOpponent()
        {
            if (!IsActive || IsPlayerEliminated || IsPlayerChampion) return null;
            foreach (var m in _currentRoundMatches)
            {
                if (m.IsPlayerMatch && !m.Played)
                    return m.A.IsPlayer ? m.B : m.A;
            }
            return null;
        }

        /// <summary>Trận hiện tại của user (null nếu không có).</summary>
        public BracketMatch PlayerMatch()
        {
            if (!IsActive) return null;
            foreach (var m in _currentRoundMatches)
            {
                if (m.IsPlayerMatch) return m;
            }
            return null;
        }

        /// <summary>
        /// Mô phỏng TẤT CẢ trận AI-vs-AI của vòng hiện tại (không chạm trận
        /// của user). Công thức: sức mạnh = (BracketSize - Seed + 1);
        /// P(A thắng) = sức A / (sức A + sức B). Seed cố định -> lặp lại được.
        /// Sau khi mô phỏng xong mà trận user vẫn chưa đánh thì vòng CHƯA
        /// khép — phải ReportPlayerMatch rồi mới AdvanceRound.
        /// </summary>
        public void SimulateRound()
        {
            if (!IsActive) return;
            foreach (var m in _currentRoundMatches)
            {
                if (m.Played || m.IsPlayerMatch) continue;
                float strengthA = BracketSize - m.A.Seed + 1;
                float strengthB = BracketSize - m.B.Seed + 1;
                double roll = _rng.NextDouble();
                m.Winner = roll < strengthA / (strengthA + strengthB) ? m.A : m.B;
            }
        }

        /// <summary>
        /// Báo kết quả TRẬN THẬT của user (đánh qua MatchManager).
        /// won=true: user đi tiếp (vô địch nếu đây là chung kết).
        /// won=false: user bị loại. Sau khi báo xong, nếu mọi trận của vòng
        /// đã xong thì tự sang vòng sau.
        /// Trả về true chỉ khi ghi NHẬN MỚI một kết quả (trận chưa đấu);
        /// false khi giải không active hoặc trận đã ghi rồi — để HUD chỉ
        /// thưởng tiền/hạng/danh hiệu một lần, chống farm bằng nút Đấu lại.
        /// Khi giải khép (vô địch hoặc bị loại) thì IsActive=false để các
        /// trận Quick Match sau không bị nhận nhầm là trận giải nữa.
        /// </summary>
        public bool ReportPlayerMatch(bool won)
        {
            if (!IsActive) return false;
            var match = PlayerMatch();
            if (match == null || match.Played) return false;

            if (won)
            {
                match.Winner = Player;
                PlayerMatchesWon++;
                if (CurrentRound == TournamentRound.Final)
                {
                    CurrentRound = TournamentRound.Champion;
                    IsActive = false;
                    return true;
                }
            }
            else
            {
                match.Winner = match.A.IsPlayer ? match.B : match.A;
                PlayerMatchesLost++;
                _playerBestRound = CurrentRound; // thua ở vòng này
                CurrentRound = TournamentRound.Eliminated;
                IsActive = false;
                return true;
            }

            _playerBestRound = CurrentRound; // thắng vòng này -> đã tới vòng này
            TryAdvanceRound();
            return true;
        }

        private void TryAdvanceRound()
        {
            foreach (var m in _currentRoundMatches)
            {
                if (!m.Played) return; // còn trận chưa xong (thường là trận AI)
            }
            // Mọi trận xong -> người thắng vào vòng sau.
            _alive = new List<Entrant>();
            foreach (var m in _currentRoundMatches) _alive.Add(m.Winner);

            switch (CurrentRound)
            {
                case TournamentRound.RoundOf32: CurrentRound = TournamentRound.RoundOf16; break;
                case TournamentRound.RoundOf16: CurrentRound = TournamentRound.Quarterfinal; break;
                case TournamentRound.Quarterfinal: CurrentRound = TournamentRound.Semifinal; break;
                case TournamentRound.Semifinal: CurrentRound = TournamentRound.Final; break;
                default: return;
            }
            BuildRoundMatches();
        }

        /// <summary>
        /// Dòng mô tả bracket vòng hiện tại cho UI. Trận AI ghi rõ là mô phỏng.
        /// </summary>
        public List<string> GetBracketLines()
        {
            var lines = new List<string>();
            if (!IsActive) return lines;
            lines.Add($"{CurrentRoundName()} — {BracketSize} tay vợt");
            foreach (var m in _currentRoundMatches)
            {
                string tag = m.IsPlayerMatch ? " (trận của bạn)" : " (mô phỏng AI)";
                string result = m.Played ? $" -> thắng: {m.Winner.Name}" : " -> chưa đấu";
                lines.Add($"{m.A.Name} (hạt giống {m.A.Seed}) vs {m.B.Name} (hạt giống {m.B.Seed}){result}{tag}");
            }
            return lines;
        }

        // --- Giải đặc biệt theo tuần (PROGRESSION_SPEC mục 3, chỉ dữ liệu) --

        /// <summary>Giải đặc biệt của một tuần: tên hư cấu + cấp + seed tuần.</summary>
        public struct WeeklyEvent
        {
            public string Name;
            public TournamentTier Tier;
            public int WeekSeed;
        }

        // Tên giải HƯ CẤU, xoay theo tuần (không dùng tên giải BWF thật).
        private static readonly string[] WeeklyEventNames =
        {
            "Cúp Ánh Sao",
            "Giải Mở Rộng Mùa Thu",
            "Cúp Tốc Độ",
            "Giải Thăng Hạng",
            "Cúp Đêm Hội",
        };

        /// <summary>
        /// Event đặc biệt của tuần hiện tại (chỉ dữ liệu — chưa nối vào
        /// bracket/UI). Seed = số tuần ISO (cùng chuẩn với WeeklyMissions):
        /// tên xoay mỗi tuần, cấp xoay mỗi 5 tuần.
        /// </summary>
        public static WeeklyEvent GetWeeklyEvent()
        {
            int seed = WeeklyMissions.CurrentWeekNumber();
            return GetWeeklyEventForSeed(seed);
        }

        /// <summary>Cùng logic GetWeeklyEvent nhưng nhận seed trực tiếp (để test).</summary>
        public static WeeklyEvent GetWeeklyEventForSeed(int seed)
        {
            int s = seed < 0 ? 0 : seed;
            return new WeeklyEvent
            {
                Name = WeeklyEventNames[s % WeeklyEventNames.Length],
                Tier = (TournamentTier)((s / WeeklyEventNames.Length) % 5),
                WeekSeed = s,
            };
        }
    }
}
