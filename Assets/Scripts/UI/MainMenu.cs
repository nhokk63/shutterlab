// MainMenu.cs — ShuttleLab (Unity 6)
//
// Menu chính (scene MainMenu): tên người chơi, chọn độ khó AI, Đấu nhanh,
// Luyện tập, Sự nghiệp, Cài đặt. Nút Sự nghiệp đã được BẬT (2026-11-09)
// vì CareerManager + CareerUI đã tồn tại; scene Career do SceneBuilder tạo.
//
// GameObject & wiring cho SceneBuilder:
//  - Scene MainMenu: GameObject "MainMenu" (root, không cần Canvas sẵn).
//    Component này tự dựng Canvas "MainMenuCanvas" + toàn bộ UI bằng code
//    trong Awake nếu chưa gán qua Inspector — SceneBuilder chỉ cần tạo
//    GameObject rỗng tên "MainMenu" + gắn component.
//  - Panel cài đặt: GameObject "SettingsPanel" (con của MainMenuCanvas,
//    có component SettingsPanel). MainMenu tự tạo nếu chưa có.
//  - GameStateManager là singleton DontDestroyOnLoad; nếu vào thẳng scene
//    MainMenu mà chưa có thì MainMenu tự tạo một cái.
//  - Vào trận: LoadMatch(AppMode.QuickMatch/Practice, độ khó đã chọn).
//    Độ khó cũng được lưu ngay vào GameStateManager.Difficulty.
//
// Phụ thuộc: ShuttleLab.Core (GameStateManager), ShuttleLab.Players
// (PlayerStats.DifficultyName). Không phụ thuộc Match/Players runtime.

using UnityEngine;
using UnityEngine.UI;
using ShuttleLab.Core;
using ShuttleLab.Players;

namespace ShuttleLab.UI
{
    public class MainMenu : MonoBehaviour
    {
        [Header("Gán qua Inspector nếu có — trống thì tự dựng bằng code")]
        public InputField PlayerNameInput;
        public Button QuickMatchButton;
        public Button PracticeButton;
        public Button CareerButton;
        public Button SettingsButton;
        public Text CareerNoteText;

        private Button[] _difficultyButtons = new Button[6];
        private int _difficulty = 2;
        private SettingsPanel _settings;
        private bool _built;

        private void Awake()
        {
            if (GameStateManager.Instance == null)
            {
                var go = new GameObject("GameStateManager");
                go.AddComponent<GameStateManager>();
            }
            if (GameStateManager.Instance != null)
                _difficulty = GameStateManager.Instance.Difficulty;
            BuildIfNeeded();
        }

        // --- Nút (gán qua UnityEvent hoặc code) ------------------------------

        public void OnQuickMatchButton()
        {
            var gs = GameStateManager.Instance;
            if (gs == null) return;
            gs.LoadMatch(GameStateManager.AppMode.QuickMatch, _difficulty);
        }

        public void OnPracticeButton()
        {
            var gs = GameStateManager.Instance;
            if (gs == null) return;
            gs.LoadMatch(GameStateManager.AppMode.Practice, _difficulty);
        }

        public void OnCareerButton()
        {
            var gs = GameStateManager.Instance;
            if (gs == null) return;
            gs.LoadCareer();
        }

        public void OnSettingsButton()
        {
            if (_settings != null) _settings.Show();
        }

        public void OnDifficultyButton(int level)
        {
            _difficulty = Mathf.Clamp(level, 0, 5);
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.Difficulty = _difficulty;
            RefreshDifficultyButtons();
        }

        private void OnPlayerNameEndEdit(string value)
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.PlayerName = value;
        }

        // --- Hiển thị ----------------------------------------------------------

        private void RefreshDifficultyButtons()
        {
            for (int i = 0; i < _difficultyButtons.Length; i++)
            {
                if (_difficultyButtons[i] != null)
                    UIBuilder.SetButtonSelected(_difficultyButtons[i], i == _difficulty);
            }
        }

        // --- Dựng UI bằng code -------------------------------------------------

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            // Đã gán đủ qua Inspector thì không dựng lại (tránh tạo trùng UI
            // và ghi đè field đã gán).
            bool allAssigned = PlayerNameInput != null && QuickMatchButton != null
                && PracticeButton != null && CareerButton != null
                && SettingsButton != null && CareerNoteText != null;
            if (allAssigned && _difficultyButtons != null)
            {
                foreach (var b in _difficultyButtons)
                    if (b == null) { allAssigned = false; break; }
            }
            if (allAssigned) return;

            var canvas = GetComponentInChildren<Canvas>();
            if (canvas == null)
            {
                canvas = UIBuilder.CreateCanvas("MainMenuCanvas");
                canvas.transform.SetParent(transform, false);
            }
            Transform root = canvas.transform;

            // Nền: màu sân tối.
            UIBuilder.CreatePanel(root, "MenuBackground",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.03f, 0.10f, 0.06f, 1f));

            // Tiêu đề.
            UIBuilder.CreateText(root, "TitleText", "SHUTTLELAB",
                64, UIBuilder.AccentText, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -70f), new Vector2(700f, 80f));
            UIBuilder.CreateText(root, "SubtitleText", "Cầu lông 3D  •  Unity 6",
                22, UIBuilder.TextDim, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -120f), new Vector2(700f, 30f));

            // Tên người chơi.
            UIBuilder.CreateText(root, "NameLabel", "Tên người chơi",
                22, UIBuilder.TextWhite, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -175f), new Vector2(400f, 28f));
            string savedName = GameStateManager.Instance != null
                ? GameStateManager.Instance.PlayerName : "Bạn";
            PlayerNameInput = UIBuilder.CreateInputField(root, "PlayerNameInput",
                "Nhập tên của bạn...", savedName, 16,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -215f), new Vector2(360f, 48f));
            PlayerNameInput.onEndEdit.AddListener(OnPlayerNameEndEdit);

            // Chọn độ khó AI: 6 nút.
            UIBuilder.CreateText(root, "DifficultyLabel", "Độ khó AI",
                22, UIBuilder.TextWhite, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -285f), new Vector2(400f, 28f));
            for (int i = 0; i < 6; i++)
            {
                int level = i; // copy cho closure
                float x = -375f + i * 150f;
                _difficultyButtons[i] = UIBuilder.CreateButton(root,
                    $"DifficultyButton{i}", PlayerStats.DifficultyName(i),
                    20, () => OnDifficultyButton(level),
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(x, -325f), new Vector2(140f, 52f));
            }
            RefreshDifficultyButtons();

            // Các nút chế độ chơi (neo theo mép trên để không chồng lên
            // hàng nút độ khó ở y=-325).
            QuickMatchButton = UIBuilder.CreateButton(root, "QuickMatchButton",
                "Đấu nhanh", 26, OnQuickMatchButton,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -395f), new Vector2(340f, 60f));
            PracticeButton = UIBuilder.CreateButton(root, "PracticeButton",
                "Luyện tập", 26, OnPracticeButton,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -470f), new Vector2(340f, 60f));

            // Sự nghiệp: CareerManager + CareerUI đã có (2026-11-09) nên
            // nút được bật và mở scene Career qua GameStateManager.
            CareerButton = UIBuilder.CreateButton(root, "CareerButton",
                "Sự nghiệp", 26, OnCareerButton,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -545f), new Vector2(340f, 60f));
            CareerNoteText = UIBuilder.CreateText(root, "CareerNoteText",
                "Chơi theo tuần: tập luyện rồi thi đấu, thăng hạng qua các cấp giải.",
                18, UIBuilder.TextDim, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -585f), new Vector2(560f, 26f));

            SettingsButton = UIBuilder.CreateButton(root, "SettingsButton",
                "Cài đặt", 24, OnSettingsButton,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -625f), new Vector2(340f, 56f));

            UIBuilder.CreateText(root, "VersionText", "ShuttleLab v0.1.0",
                16, UIBuilder.TextDim, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 18f), new Vector2(300f, 22f));

            // Panel cài đặt (ẩn sẵn, mở bằng nút Cài đặt).
            Transform found = root.Find("SettingsPanel");
            if (found != null) _settings = found.GetComponent<SettingsPanel>();
            if (_settings == null)
            {
                var go = new GameObject("SettingsPanel");
                go.transform.SetParent(root, false);
                _settings = go.AddComponent<SettingsPanel>();
            }
        }
    }
}
