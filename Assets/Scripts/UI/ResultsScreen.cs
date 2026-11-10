// ResultsScreen.cs — ShuttleLab (Unity 6)
//
// Panel kết quả cuối trận, dùng chung cho mọi nơi cần hiện kết quả:
//  - HUDManager gọi Show() khi MatchManager phát OnMatchEnd.
//  - Nút Rematch: đấu lại cùng độ khó (MatchManager.StartMatch).
//  - Nút Menu: về MainMenu qua GameStateManager.
//
// GameObject & wiring cho SceneBuilder:
//  - Scene Match: GameObject "ResultsScreen" (con của Canvas HUD, cùng
//    Canvas với HUDManager). Component này tự dựng UI bằng code trong
//    Awake nếu chưa gán qua Inspector — SceneBuilder chỉ cần tạo
//    GameObject rỗng tên "ResultsScreen" + gắn component, KHÔNG cần dựng
//    UI tay. Mặc định ẩn (SetActive(false)) cho tới khi Show() được gọi.
//  - Không tự tìm MatchManager trong Awake (trận có thể chưa bắt đầu);
//    Rematch tìm MatchManager lúc bấm nút.
//  - Dữ liệu hiển thị lấy 100% từ tham số Show() — không bịa số.
//
// Phụ thuộc: ShuttleLab.Core (GameStateManager), ShuttleLab.Match
// (MatchManager, MatchRules, MatchStats). MatchStats là class độc lập
// trong namespace ShuttleLab.Match (KHÔNG phải MatchManager.MatchStats).

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ShuttleLab.Core;
using ShuttleLab.Match;

namespace ShuttleLab.UI
{
    public class ResultsScreen : MonoBehaviour
    {
        [Header("Gán qua Inspector nếu có — trống thì tự dựng bằng code")]
        public Text TitleText;
        public Text ScoreText;
        public Text StatsText;
        public Button RematchButton;
        public Button MenuButton;

        private bool _built;

        private void Awake()
        {
            BuildIfNeeded();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Hiện kết quả. winner: 0 = user thắng, 1 = AI thắng.
        /// score: ScoreState cuối trận; stats: MatchStats của trận;
        /// playerName/aiName: tên hiển thị (aiName thường là tên độ khó).
        /// </summary>
        public void Show(int winner, MatchRules.ScoreState score,
            MatchStats stats, string playerName, string aiName)
        {
            BuildIfNeeded();
            gameObject.SetActive(true);

            string pName = string.IsNullOrEmpty(playerName) ? "Bạn" : playerName;
            string aName = string.IsNullOrEmpty(aiName) ? "Đối thủ" : aiName;

            if (TitleText != null)
            {
                TitleText.text = winner == 0
                    ? $"{pName} thắng trận!"
                    : $"{aName} thắng trận!";
                TitleText.color = winner == 0
                    ? UIBuilder.AccentText : new Color(1f, 0.55f, 0.45f);
            }

            if (ScoreText != null && score != null)
            {
                ScoreText.text = $"Tỉ số ván: {score.Games[0]}-{score.Games[1]}"
                    + $"  •  Điểm ván cuối: {score.Points[0]}-{score.Points[1]}";
            }
            else if (ScoreText != null)
                ScoreText.text = string.Empty;

            if (StatsText != null)
            {
                if (stats != null)
                {
                    int minutes = Mathf.FloorToInt(stats.MatchTime / 60f);
                    int seconds = Mathf.FloorToInt(stats.MatchTime % 60f);
                    StatsText.text =
                        $"Thời gian: {minutes:00}:{seconds:00}\n"
                        + $"Tổng cú đánh qua lại: {stats.Rallies}\n"
                        + $"Cú đánh đã thực hiện: {stats.ShotsHit}\n"
                        + $"Cú đánh tốt: {stats.ShotsGood} ({stats.Accuracy * 100f:0}%)\n"
                        + $"Giao cầu tốt: {stats.ServesGood}\n"
                        + $"Smash: {stats.Smashes}\n"
                        + $"Lỗi: {stats.Errors}\n"
                        + $"Rally dài nhất: {stats.LongestRally} cú đánh";
                }
                else
                {
                    StatsText.text = "Không có thống kê trận đấu.";
                }
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // --- Nút (gán qua UnityEvent hoặc code) ------------------------------

        public void OnRematchButton()
        {
            var match = FindFirstObjectByType<MatchManager>();
            if (match == null)
            {
                Debug.LogWarning("[ResultsScreen] Không tìm thấy MatchManager để đấu lại.");
                return;
            }
            Time.timeScale = 1f;
            Hide();
            match.StartMatch(match.DifficultyLevel, match.IsPractice);
        }

        public void OnMenuButton()
        {
            Time.timeScale = 1f;
            Hide();
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.LoadMenu();
            else
                SceneManager.LoadScene("MainMenu");
        }

        // --- Dựng UI bằng code ------------------------------------------------

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            // GameObject này phải là RectTransform full-screen dưới một
            // Canvas (HUD canvas hoặc Canvas riêng). Nếu SceneBuilder tạo
            // nó như GameObject thường, tự đổi sang RectTransform.
            var selfRt = GetComponent<RectTransform>();
            if (selfRt == null) selfRt = gameObject.AddComponent<RectTransform>();
            UIBuilder.Place(selfRt, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);

            if (TitleText != null && ScoreText != null && StatsText != null
                && RematchButton != null && MenuButton != null)
                return; // đã gán đủ qua Inspector thì không dựng lại

            // Panel nền + khung: chỉ tạo khi chưa có; từng field chỉ tạo
            // khi null (tránh ghi đè field đã gán qua Inspector).
            Transform bg = transform.Find("ResultsBackground");
            if (bg == null)
                bg = UIBuilder.CreatePanel(transform, "ResultsBackground",
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                    new Color(0f, 0f, 0f, 0.75f));

            // Khung kết quả ở giữa.
            Transform card = bg.Find("ResultsCard");
            if (card == null)
                card = UIBuilder.CreatePanel(bg, "ResultsCard",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(560f, 520f), UIBuilder.PanelSolid);

            if (TitleText == null)
                TitleText = UIBuilder.CreateText(card, "TitleText", "Kết quả",
                    36, UIBuilder.AccentText, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -50f), new Vector2(520f, 50f));

            if (ScoreText == null)
                ScoreText = UIBuilder.CreateText(card, "ScoreText", string.Empty,
                    24, UIBuilder.TextWhite, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -105f), new Vector2(520f, 34f));

            if (StatsText == null)
                StatsText = UIBuilder.CreateText(card, "StatsText", string.Empty,
                    22, UIBuilder.TextDim, TextAnchor.UpperLeft,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -280f), new Vector2(480f, 300f));

            if (RematchButton == null)
                RematchButton = UIBuilder.CreateButton(card, "RematchButton", "Đấu lại",
                    24, OnRematchButton,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(-140f, 45f), new Vector2(230f, 56f));

            if (MenuButton == null)
                MenuButton = UIBuilder.CreateButton(card, "MenuButton", "Về menu",
                    24, OnMenuButton,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(140f, 45f), new Vector2(230f, 56f));
        }
    }
}
