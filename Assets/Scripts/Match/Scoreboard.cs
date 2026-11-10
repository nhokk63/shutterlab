// Scoreboard.cs — ShuttleLab (Unity 6)
// Class thường (không MonoBehaviour): giữ bản sao ScoreState để HUD đọc.
// MatchManager phát OnScoreChanged -> HUD gọi Scoreboard.Update(state).
// Mọi chuỗi đều là tiếng Việt, theo quy ước UI của dự án.

namespace ShuttleLab.Match
{
    public class Scoreboard
    {
        public MatchRules.ScoreState Score { get; private set; } = new MatchRules.ScoreState();

        public void Update(MatchRules.ScoreState score)
        {
            Score = score != null ? score.Clone() : new MatchRules.ScoreState();
        }

        /// <summary>Điểm ván hiện tại, vd "21-15".</summary>
        public string FormatScore() => $"{Score.Points[0]}-{Score.Points[1]}";

        /// <summary>Số ván đã thắng, vd "1-0".</summary>
        public string FormatGames() => $"{Score.Games[0]}-{Score.Games[1]}";

        /// <summary>Ai đang giao cầu.</summary>
        public string ServerIndicator() =>
            Score.Server == 0 ? "Giao cầu: Bạn" : "Giao cầu: Đối thủ";

        /// <summary>Cả hai đều >= 20 điểm (deuce, phải hơn 2 mới thắng ván).</summary>
        public bool IsDeuce() => Score.Points[0] >= 20 && Score.Points[0] == Score.Points[1];

        /// <summary>
        /// Player có đang ở match point không — tức thắng điểm này thì thắng
        /// luôn ván VÀ ván đó là ván quyết định trận đấu.
        /// </summary>
        public bool IsMatchPoint(int player)
        {
            if (player < 0 || player > 1) return false;
            int other = 1 - player;
            int p = Score.Points[player] + 1;
            int q = Score.Points[other];
            bool winsGame = (p >= MatchRules.PointsToWinGame && p - q >= MatchRules.WinBy)
                || p == MatchRules.MaxPoints;
            if (!winsGame) return false;
            return Score.Games[player] + 1 >= MatchRules.GamesToWinMatch;
        }
    }
}
