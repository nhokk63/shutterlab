// HUDManager.cs — ShuttleLab (Unity 6)
//
// HUD của scene Match: điểm số, tên người chơi, thể lực user, thông báo
// (toast), nút Pause, panel tạm dừng, panel kết quả (ủy quyền cho
// ResultsScreen).
//
// GameObject & wiring cho SceneBuilder:
//  - Scene Match: GameObject "HUDManager" (root, không cần Canvas sẵn).
//    Component này tự dựng Canvas "HUDCanvas" + toàn bộ UI bằng code
//    trong Awake nếu chưa gán qua Inspector — SceneBuilder chỉ cần tạo
//    GameObject rỗng tên "HUDManager" + gắn component.
//  - GameBootstrap nối match bằng SendMessage("BindMatch", match) tới
//    GameObject "HUDManager". Nếu Start chạy mà chưa ai Bind thì HUD tự
//    tìm MatchManager bằng FindFirstObjectByType và bind.
//  - Panel nhập liệu (JoystickPanel / ShotButtonsPanel) KHÔNG thuộc HUD:
//    MobileInputManager tự tìm chúng theo tên — SceneBuilder dựng chúng
//    ở phần input (kèm VirtualJoystick / ShotButton).
//  - Panel kết quả: GameObject "ResultsScreen" (con của HUDCanvas, có
//    component ResultsScreen). HUD tự tạo nếu chưa có.
//
// Luồng dữ liệu (chỉ đọc, không chạm vật lý / điểm số):
//  - OnScoreChanged -> Scoreboard (bản sao ScoreState) -> hiển thị.
//  - OnMessage -> toast 1.5 s (đếm bằng unscaled time nên vẫn chạy khi pause).
//  - OnMatchEnd -> ResultsScreen.Show(winner, score, stats, tên 2 bên).
//  - Thể lực: Players[0].Stamina.Normalized (0..1), đổi màu theo mức.
//  - Pause: đồng bộ cả khi bấm Esc/phím P (MatchManager tự Pause) — HUD
//    poll Phase mỗi frame để mở/đóng panel tạm dừng cho khớp.
//
// Phụ thuộc: ShuttleLab.Core (GameStateManager), ShuttleLab.Match
// (MatchManager, MatchRules, MatchStats, Scoreboard), ShuttleLab.Players
// (PlayerStats — tên độ khó AI; PlayerStamina — thanh thể lực).

using UnityEngine;
using UnityEngine.UI;
using ShuttleLab.Core;
using ShuttleLab.Match;
using ShuttleLab.Players;

namespace ShuttleLab.UI
{
    public class HUDManager : MonoBehaviour
    {
        [Header("Gán qua Inspector nếu có — trống thì tự dựng bằng code")]
        public Text ScoreText;
        public Text NamesText;
        public Text SubText;
        public Text DeuceText;
        public Text ToastText;
        public Image StaminaFill;
        public Button PauseButton;
        public GameObject PausePanel;
        public ResultsScreen Results;

        private MatchManager _match;
        private readonly Scoreboard _board = new Scoreboard();
        private Canvas _canvas;
        private GameObject _toastPanel;
        private GameObject _staminaPanel;
        private Text _staminaLabel;
        private float _toastTimer;
        private bool _built;
        private bool _bound;

        private void Awake()
        {
            BuildIfNeeded();
        }

        private void Start()
        {
            // GameBootstrap thường Bind trước qua SendMessage; nếu vào thẳng
            // scene Match mà chưa ai Bind thì tự tìm.
            if (!_bound)
            {
                var match = FindFirstObjectByType<MatchManager>();
                if (match != null) BindMatch(match);
            }
        }

        private void Update()
        {
            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.unscaledDeltaTime;
                if (_toastTimer <= 0f && _toastPanel != null)
                    _toastPanel.SetActive(false);
            }

            if (_match != null)
            {
                // Giữ panel tạm dừng đồng bộ với Phase (kể cả pause bằng Esc/P).
                bool paused = _match.Phase == MatchManager.MatchPhase.Paused;
                if (PausePanel != null)
                {
                    bool resultsOpen = Results != null && Results.gameObject.activeSelf;
                    if (paused && !PausePanel.activeSelf && !resultsOpen)
                        PausePanel.SetActive(true);
                    else if (!paused && PausePanel.activeSelf)
                        PausePanel.SetActive(false);
                }
                UpdateStamina();
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }

        // --- Nối MatchManager -------------------------------------------------

        /// <summary>
        /// GameBootstrap gọi qua SendMessage("BindMatch", match).
        /// Đăng ký OnScoreChanged / OnMessage / OnMatchEnd.
        /// </summary>
        public void BindMatch(MatchManager match)
        {
            if (match == null) return;
            if (_bound && _match == match) return;
            Unbind();
            _match = match;
            _match.OnScoreChanged += OnScoreChanged;
            _match.OnMessage += OnMessageReceived;
            _match.OnMatchEnd += OnMatchEnd;
            _bound = true;
            RefreshNames();
            OnScoreChanged(_match.Rules != null ? _match.Rules.Score : null);
        }

        private void Unbind()
        {
            if (!_bound || _match == null) return;
            _match.OnScoreChanged -= OnScoreChanged;
            _match.OnMessage -= OnMessageReceived;
            _match.OnMatchEnd -= OnMatchEnd;
            _match = null;
            _bound = false;
        }

        // --- Event từ MatchManager --------------------------------------------

        private void OnScoreChanged(MatchRules.ScoreState state)
        {
            _board.Update(state);
            if (ScoreText != null) ScoreText.text = _board.FormatScore();
            if (SubText != null)
            {
                SubText.text = $"Ván {_board.Score.CurrentGame}  •  "
                    + $"Thắng ván: {_board.FormatGames()}  •  {_board.ServerIndicator()}";
            }
            if (DeuceText != null)
            {
                if (_board.IsDeuce())
                {
                    DeuceText.text = "Deuce — phải hơn 2 điểm!";
                    DeuceText.gameObject.SetActive(true);
                }
                else if (_board.IsMatchPoint(0))
                {
                    DeuceText.text = "Match point — Bạn!";
                    DeuceText.gameObject.SetActive(true);
                }
                else if (_board.IsMatchPoint(1))
                {
                    DeuceText.text = "Match point — Đối thủ!";
                    DeuceText.gameObject.SetActive(true);
                }
                else
                {
                    DeuceText.gameObject.SetActive(false);
                }
            }
        }

        private void OnMessageReceived(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (ToastText != null) ToastText.text = message;
            if (_toastPanel != null) _toastPanel.SetActive(true);
            _toastTimer = 1.5f;
        }

        private void OnMatchEnd(int winner)
        {
            if (PausePanel != null) PausePanel.SetActive(false);
            if (Results == null || _match == null) return;
            string userName = PlayerName(0);
            string aiName = PlayerName(1);
            Results.Show(winner,
                _match.Rules != null ? _match.Rules.Score : null,
                _match.Stats, userName, aiName);
            // Ghi kết quả vào bracket giải (nếu trận này thuộc tuần giải).
            var session = ShuttleLab.Systems.TournamentSession.Instance;
            if (session != null && session.Tournament != null && session.Tournament.IsActive)
            {
                session.Tournament.ReportPlayerMatch(won: winner == 0);
                if (session.Career != null)
                    session.Career.RecordTournamentResult(session.Tournament.PlayerPlacement,
                        session.Career.CurrentTier, session.Tournament.PlayerMatchesWon,
                        session.Tournament.PlayerMatchesLost);
            }
        }

        // --- Nút (gán qua UnityEvent hoặc code) ------------------------------

        public void OnPauseButton()
        {
            if (_match == null) return;
            _match.Pause(_match.Phase != MatchManager.MatchPhase.Paused);
        }

        public void OnResumeButton()
        {
            if (_match == null) return;
            _match.Pause(false);
        }

        public void OnRestartButton()
        {
            if (_match == null) return;
            Time.timeScale = 1f; // Pause() left timeScale at 0 — restore it first.
            if (PausePanel != null) PausePanel.SetActive(false);
            if (Results != null) Results.Hide();
            _match.StartMatch(_match.DifficultyLevel, _match.IsPractice);
            OnMessageReceived("Trận mới bắt đầu.");
        }

        public void OnQuitButton()
        {
            if (_match == null)
            {
                if (GameStateManager.Instance != null)
                    GameStateManager.Instance.LoadMenu();
                return;
            }
            _match.QuitToMenu();
        }

        // --- Hiển thị phụ ------------------------------------------------------

        private void RefreshNames()
        {
            if (NamesText == null) return;
            NamesText.text = $"{PlayerName(0)}  vs  {PlayerName(1)}";
        }

        private string PlayerName(int index)
        {
            if (_match != null && _match.Players != null
                && _match.Players.Length > index && _match.Players[index] != null
                && _match.Players[index].Stats != null
                && !string.IsNullOrEmpty(_match.Players[index].Stats.Name))
                return _match.Players[index].Stats.Name;
            if (index == 0)
                return GameStateManager.Instance != null
                    ? GameStateManager.Instance.PlayerName : "Bạn";
            return _match != null
                ? PlayerStats.DifficultyName(_match.DifficultyLevel)
                : "Đối thủ";
        }

        private void UpdateStamina()
        {
            if (_staminaPanel == null || _match == null) return;
            var players = _match.Players;
            if (players == null || players.Length < 1 || players[0] == null
                || players[0].Stamina == null)
            {
                _staminaPanel.SetActive(false);
                if (_staminaLabel != null) _staminaLabel.gameObject.SetActive(false);
                return;
            }
            _staminaPanel.SetActive(true);
            if (_staminaLabel != null) _staminaLabel.gameObject.SetActive(true);
            float n = Mathf.Clamp01(players[0].Stamina.Normalized);
            if (StaminaFill != null)
            {
                StaminaFill.fillAmount = n;
                StaminaFill.color = n > 0.3f ? UIBuilder.BarGood
                    : n > 0.1f ? UIBuilder.BarLow : UIBuilder.BarEmpty;
            }
            if (_staminaLabel != null)
                _staminaLabel.text = players[0].Stamina.IsLow ? "Thể lực (thấp!)" : "Thể lực";
        }

        // --- Dựng UI bằng code -------------------------------------------------

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            _canvas = GetComponentInChildren<Canvas>();
            if (_canvas == null)
            {
                _canvas = UIBuilder.CreateCanvas("HUDCanvas");
                _canvas.transform.SetParent(transform, false);
            }
            Transform root = _canvas.transform;

            // --- Thanh điểm trên cùng ---
            // Panel chỉ tạo khi còn thiếu text; từng text chỉ tạo khi null
            // (tránh ghi đè field đã gán qua Inspector).
            if (ScoreText == null || NamesText == null || SubText == null)
            {
                Transform topBar = root.Find("TopBar");
                if (topBar == null)
                    topBar = UIBuilder.CreatePanel(root, "TopBar",
                        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -52f), new Vector2(480f, 96f), UIBuilder.PanelDark);
                if (NamesText == null)
                    NamesText = UIBuilder.CreateText(topBar, "NamesText", "Bạn  vs  Đối thủ",
                        20, UIBuilder.TextDim, TextAnchor.MiddleCenter,
                        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -16f), new Vector2(460f, 26f));
                if (ScoreText == null)
                    ScoreText = UIBuilder.CreateText(topBar, "ScoreText", "0-0",
                        34, UIBuilder.TextWhite, TextAnchor.MiddleCenter,
                        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -50f), new Vector2(460f, 44f));
                if (SubText == null)
                    SubText = UIBuilder.CreateText(topBar, "SubText", string.Empty,
                        18, UIBuilder.TextDim, TextAnchor.MiddleCenter,
                        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -82f), new Vector2(460f, 24f));
            }

            // --- Nhãn Deuce / Match point ---
            if (DeuceText == null)
            {
                DeuceText = UIBuilder.CreateText(root, "DeuceText", string.Empty,
                    22, UIBuilder.AccentText, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -118f), new Vector2(480f, 30f));
                DeuceText.gameObject.SetActive(false);
            }

            // --- Toast thông báo ---
            if (ToastText == null || _toastPanel == null)
            {
                _toastPanel = UIBuilder.CreatePanel(root, "ToastPanel",
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -160f), new Vector2(600f, 44f),
                    UIBuilder.PanelDark).gameObject;
                ToastText = UIBuilder.CreateText(_toastPanel.transform, "ToastText",
                    string.Empty, 22, UIBuilder.TextWhite, TextAnchor.MiddleCenter,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                _toastPanel.SetActive(false);
            }

            // --- Nút Pause góc phải ---
            if (PauseButton == null)
            {
                PauseButton = UIBuilder.CreateButton(root, "PauseButton", "II",
                    28, OnPauseButton,
                    new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(-48f, -48f), new Vector2(64f, 64f));
            }

            // --- Thanh thể lực góc trái dưới ---
            if (StaminaFill == null || _staminaPanel == null)
            {
                _staminaPanel = UIBuilder.CreatePanel(root, "StaminaPanel",
                    Vector2.zero, Vector2.zero,
                    new Vector2(120f, 30f), new Vector2(220f, 24f),
                    new Color(0f, 0f, 0f, 0.55f)).gameObject;
                var fillGo = new GameObject("StaminaFill");
                fillGo.transform.SetParent(_staminaPanel.transform, false);
                StaminaFill = fillGo.AddComponent<Image>();
                StaminaFill.color = UIBuilder.BarGood;
                StaminaFill.type = Image.Type.Filled;
                StaminaFill.fillMethod = Image.FillMethod.Horizontal;
                var fillRt = fillGo.GetComponent<RectTransform>();
                fillRt.anchorMin = Vector2.zero;
                fillRt.anchorMax = Vector2.one;
                fillRt.anchoredPosition = Vector2.zero;
                fillRt.sizeDelta = Vector2.zero;
                _staminaLabel = UIBuilder.CreateText(root, "StaminaLabel", "Thể lực",
                    18, UIBuilder.TextDim, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.zero,
                    new Vector2(70f, 66f), new Vector2(140f, 24f));
                // Ẩn tới khi có trận đấu thật (UpdateStamina mở lại).
                _staminaPanel.SetActive(false);
                _staminaLabel.gameObject.SetActive(false);
            }

            // --- Panel tạm dừng ---
            if (PausePanel == null)
            {
                PausePanel = UIBuilder.CreatePanel(root, "PausePanel",
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                    new Color(0f, 0f, 0f, 0.6f)).gameObject;
                var card = UIBuilder.CreatePanel(PausePanel.transform, "PauseCard",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(400f, 340f), UIBuilder.PanelSolid);
                UIBuilder.CreateText(card, "PauseTitle", "Tạm dừng",
                    32, UIBuilder.TextWhite, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -45f), new Vector2(360f, 44f));
                UIBuilder.CreateButton(card, "ResumeButton", "Tiếp tục",
                    24, OnResumeButton,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -120f), new Vector2(280f, 56f));
                UIBuilder.CreateButton(card, "RestartButton", "Chơi lại",
                    24, OnRestartButton,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -190f), new Vector2(280f, 56f));
                var quitBtn = UIBuilder.CreateButton(card, "QuitButton", "Về menu",
                    24, OnQuitButton,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -260f), new Vector2(280f, 56f));
                var quitImage = quitBtn.GetComponent<Image>();
                if (quitImage != null) quitImage.color = UIBuilder.Danger;
                PausePanel.SetActive(false);
            }

            // --- Panel kết quả (ủy quyền cho ResultsScreen) ---
            if (Results == null)
            {
                Transform found = root.Find("ResultsScreen");
                if (found != null) Results = found.GetComponent<ResultsScreen>();
                if (Results == null)
                {
                    var go = new GameObject("ResultsScreen");
                    go.transform.SetParent(root, false);
                    Results = go.AddComponent<ResultsScreen>();
                }
            }
        }
    }
}
