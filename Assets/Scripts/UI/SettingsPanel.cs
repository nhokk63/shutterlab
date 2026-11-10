// SettingsPanel.cs — ShuttleLab (Unity 6)
//
// Panel cài đặt dùng chung (mở từ MainMenu; SceneBuilder có thể đặt thêm
// một bản trong scene Match nếu muốn chỉnh giữa trận):
//  - Slider nhạc / hiệu ứng (0..1) -> GameStateManager, lưu ngay.
//  - Chọn preset camera (6 preset) -> GameStateManager.CameraPreset;
//    áp dụng ngay cho CameraManager nếu đang có trong scene.
//  - Toggle tay trái -> GameStateManager.LeftHandMode; áp dụng ngay cho
//    MobileInputManager nếu đang có trong scene (đổi vị trí 2 panel input).
//  - Hiển thị độ khó AI hiện tại (chỉ đọc — độ khó chọn ở MainMenu).
//  - Đồ hoạ: preset Tiết kiệm/Cân bằng/Cao/Tối đa, FPS 30/60/90/120,
//    độ phân giải renderScale 0.6–1.0 + đồng hồ FPS thực tế
//    -> GraphicsQuality (PlayerPrefs "shuttlelab_gfx_*"), áp dụng ngay.
//  - Nút Đóng (góc phải trên).
//
// GameObject & wiring cho SceneBuilder:
//  - GameObject "SettingsPanel" (con của Canvas của scene chứa nó).
//    Component này tự dựng UI bằng code trong Awake nếu chưa gán qua
//    Inspector — SceneBuilder chỉ cần tạo GameObject rỗng tên
//    "SettingsPanel" + gắn component. Mặc định ẩn cho tới khi Show() gọi.
//  - Slider âm lượng lưu vào GameStateManager (PlayerPrefs "shuttlelab_*");
//    hiệu ứng phát qua AudioManager (đã nối trong GameBootstrap), nhạc nền
//    chưa có track. Panel ghi rõ điều này bằng một dòng chú thích nhỏ.
//
// Phụ thuộc: ShuttleLab.Core (GameStateManager),
// ShuttleLab.Camera (CameraManager), ShuttleLab.Input
// (MobileInputManager), ShuttleLab.Players (PlayerStats — tên độ khó).

using UnityEngine;
using UnityEngine.UI;
using ShuttleLab.Core;
using ShuttleLab.Camera;
using ShuttleLab.Input;
using ShuttleLab.Players;
using ShuttleLab.Systems;

namespace ShuttleLab.UI
{
    public class SettingsPanel : MonoBehaviour
    {
        [Header("Gán qua Inspector nếu có — trống thì tự dựng bằng code")]
        public Slider MusicSlider;
        public Slider SfxSlider;
        public Text MusicValueText;
        public Text SfxValueText;
        public Toggle LeftHandToggle;
        public Text DifficultyText;
        public Button CloseButton;

        private static readonly string[] PresetNames =
        {
            "Sau lưng", "Trên cao", "Truyền hình",
            "Theo cầu", "Bên hông", "Chiến thuật"
        };

        private Button[] _presetButtons = new Button[6];
        private Button[] _gfxPresetButtons = new Button[4];
        private Button[] _fpsButtons = new Button[4];
        private Slider _gfxScaleSlider;
        private Text _gfxScaleText;
        private Text _fpsMeterText;
        private int _fpsFrames;
        private float _fpsTime;
        private bool _built;

        private void Awake()
        {
            BuildIfNeeded();
            gameObject.SetActive(false);
        }

        /// <summary>Mở panel (đồng bộ giá trị từ GameStateManager).</summary>
        public void Show()
        {
            BuildIfNeeded();
            gameObject.SetActive(true);
            RefreshFromSettings();
        }

        public void Hide()
        {
            // Lưu một lần khi đóng panel (slider âm lượng không Save() theo
            // từng tick kéo để tránh ghi đĩa liên tục).
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.SaveSettings();
            gameObject.SetActive(false);
        }

        // --- Nút / slider / toggle (gán qua UnityEvent hoặc code) -----------

        public void OnCloseButton()
        {
            Hide();
        }

        public void OnMusicSlider(float value)
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.MusicVolume = value;
            if (MusicValueText != null)
                MusicValueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }

        public void OnSfxSlider(float value)
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.SfxVolume = value;
            if (SfxValueText != null)
                SfxValueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }

        public void OnCameraPresetButton(int preset)
        {
            int p = Mathf.Clamp(preset, 0, 5);
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.CameraPreset = p;
            var cam = FindFirstObjectByType<CameraManager>();
            if (cam != null) cam.SetPreset((CameraManager.CamPreset)p);
            RefreshPresetButtons();
        }

        public void OnLeftHandToggle(bool left)
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.LeftHandMode = left;
            var input = FindFirstObjectByType<MobileInputManager>();
            if (input != null) input.ApplyLeftHandMode(left);
        }

        public void OnGraphicsPresetButton(int tier)
        {
            GraphicsQuality.Apply((GraphicsQuality.Tier)Mathf.Clamp(tier, 0, 3));
            RefreshGraphicsButtons();
            RefreshFpsButtons();
        }

        public void OnFpsButton(int fps)
        {
            GraphicsQuality.ApplyFps(fps);
            RefreshFpsButtons();
        }

        public void OnGraphicsScaleSlider(float value)
        {
            GraphicsQuality.ApplyScale(value);
            if (_gfxScaleText != null)
                _gfxScaleText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }

        // Đo FPS thực tế hiển thị trong panel (chỉ chạy khi panel đang mở).
        private void Update()
        {
            if (_fpsMeterText == null) return;
            _fpsFrames++;
            _fpsTime += Time.unscaledDeltaTime;
            if (_fpsTime >= 1f)
            {
                float fps = _fpsFrames / _fpsTime;
                _fpsMeterText.text = $"{fps:0} FPS";
                _fpsFrames = 0;
                _fpsTime = 0f;
            }
        }

        // --- Đồng bộ hiển thị --------------------------------------------------

        private void RefreshFromSettings()
        {
            var gs = GameStateManager.Instance;
            if (gs == null) return;

            if (MusicSlider != null) MusicSlider.SetValueWithoutNotify(gs.MusicVolume);
            if (SfxSlider != null) SfxSlider.SetValueWithoutNotify(gs.SfxVolume);
            if (MusicValueText != null)
                MusicValueText.text = $"{Mathf.RoundToInt(gs.MusicVolume * 100f)}%";
            if (SfxValueText != null)
                SfxValueText.text = $"{Mathf.RoundToInt(gs.SfxVolume * 100f)}%";
            if (LeftHandToggle != null)
                LeftHandToggle.SetIsOnWithoutNotify(gs.LeftHandMode);
            if (DifficultyText != null)
                DifficultyText.text = $"Độ khó AI: {PlayerStats.DifficultyName(gs.Difficulty)}"
                    + $" ({gs.Difficulty}) — chọn ở menu chính";
            RefreshPresetButtons();
            RefreshGraphicsButtons();
            RefreshFpsButtons();
            if (_gfxScaleSlider != null)
                _gfxScaleSlider.SetValueWithoutNotify(GraphicsQuality.CurrentScale);
            if (_gfxScaleText != null)
                _gfxScaleText.text = $"{Mathf.RoundToInt(GraphicsQuality.CurrentScale * 100f)}%";
        }

        private void RefreshPresetButtons()
        {
            int current = GameStateManager.Instance != null
                ? GameStateManager.Instance.CameraPreset : 0;
            for (int i = 0; i < _presetButtons.Length; i++)
            {
                if (_presetButtons[i] != null)
                    UIBuilder.SetButtonSelected(_presetButtons[i], i == current);
            }
        }

        private void RefreshGraphicsButtons()
        {
            int current = (int)GraphicsQuality.CurrentTier;
            for (int i = 0; i < _gfxPresetButtons.Length; i++)
            {
                if (_gfxPresetButtons[i] != null)
                    UIBuilder.SetButtonSelected(_gfxPresetButtons[i], i == current);
            }
        }

        private void RefreshFpsButtons()
        {
            int current = GraphicsQuality.CurrentFps;
            for (int i = 0; i < _fpsButtons.Length; i++)
            {
                if (_fpsButtons[i] != null)
                    UIBuilder.SetButtonSelected(_fpsButtons[i],
                        GraphicsQuality.FpsOptions[i] == current);
            }
        }

        // --- Dựng UI bằng code -------------------------------------------------

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            // GameObject này phải là RectTransform full-screen dưới một
            // Canvas (giống ResultsScreen).
            var selfRt = GetComponent<RectTransform>();
            if (selfRt == null) selfRt = gameObject.AddComponent<RectTransform>();
            UIBuilder.Place(selfRt, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);

            if (MusicSlider != null && SfxSlider != null
                && MusicValueText != null && SfxValueText != null
                && DifficultyText != null
                && LeftHandToggle != null && CloseButton != null
                && AllPresetButtonsAssigned())
                return; // đã gán đủ qua Inspector thì không dựng lại

            // Panel nền + khung: chỉ tạo khi chưa có; từng field chỉ tạo
            // khi null (tránh ghi đè field đã gán qua Inspector).
            Transform bg = transform.Find("SettingsBackground");
            if (bg == null)
                bg = UIBuilder.CreatePanel(transform, "SettingsBackground",
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                    new Color(0f, 0f, 0f, 0.7f));

            Transform card = bg.Find("SettingsCard");
            if (card == null)
                card = UIBuilder.CreatePanel(bg, "SettingsCard",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(620f, 620f), UIBuilder.PanelSolid);

            UIBuilder.CreateText(card, "SettingsTitle", "Cài đặt",
                32, UIBuilder.TextWhite, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -45f), new Vector2(560f, 44f));

            // Nhạc.
            UIBuilder.CreateText(card, "MusicLabel", "Nhạc nền",
                22, UIBuilder.TextWhite, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(130f, -110f), new Vector2(140f, 30f));
            if (MusicSlider == null)
                MusicSlider = UIBuilder.CreateSlider(card, "MusicSlider",
                    0f, 1f, 0.8f,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(50f, -110f), new Vector2(300f, 30f));
            MusicSlider.onValueChanged.AddListener(OnMusicSlider);
            if (MusicValueText == null)
                MusicValueText = UIBuilder.CreateText(card, "MusicValueText", "80%",
                    22, UIBuilder.TextDim, TextAnchor.MiddleLeft,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(230f, -110f), new Vector2(70f, 30f));

            // Hiệu ứng.
            UIBuilder.CreateText(card, "SfxLabel", "Hiệu ứng",
                22, UIBuilder.TextWhite, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(130f, -160f), new Vector2(140f, 30f));
            if (SfxSlider == null)
                SfxSlider = UIBuilder.CreateSlider(card, "SfxSlider",
                    0f, 1f, 0.8f,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(50f, -160f), new Vector2(300f, 30f));
            SfxSlider.onValueChanged.AddListener(OnSfxSlider);
            if (SfxValueText == null)
                SfxValueText = UIBuilder.CreateText(card, "SfxValueText", "80%",
                    22, UIBuilder.TextDim, TextAnchor.MiddleLeft,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(230f, -160f), new Vector2(70f, 30f));

            // Camera: 6 nút, 2 hàng x 3.
            UIBuilder.CreateText(card, "CameraLabel", "Góc camera",
                22, UIBuilder.TextWhite, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(130f, -215f), new Vector2(200f, 30f));
            for (int i = 0; i < 6; i++)
            {
                if (_presetButtons[i] != null) continue;
                int preset = i; // copy cho closure
                float x = -170f + (i % 3) * 175f;
                float y = -260f - (i / 3) * 60f;
                _presetButtons[i] = UIBuilder.CreateButton(card,
                    $"CameraPresetButton{i}", PresetNames[i],
                    20, () => OnCameraPresetButton(preset),
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(x, y), new Vector2(165f, 48f));
            }

            // Tay trái.
            if (LeftHandToggle == null)
                LeftHandToggle = UIBuilder.CreateToggle(card, "LeftHandToggle",
                    "Chế độ tay trái (đổi joystick <-> nút đánh)", 18, false,
                    new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(46f, -395f), new Vector2(32f, 32f));
            LeftHandToggle.onValueChanged.AddListener(OnLeftHandToggle);

            // Độ khó (chỉ đọc).
            if (DifficultyText == null)
                DifficultyText = UIBuilder.CreateText(card, "DifficultyText",
                    "Độ khó AI: Advanced (2) — chọn ở menu chính",
                    20, UIBuilder.TextDim, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -445f), new Vector2(560f, 28f));

            // Đồ hoạ: preset chất lượng / FPS / độ phân giải (renderScale).
            // (Ghi chú âm thanh cũ đã bỏ khỏi panel — trạng thái âm thanh
            // ghi trong comment đầu file.)
            UIBuilder.CreateText(card, "GraphicsLabel", "Đồ hoạ",
                22, UIBuilder.TextWhite, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(130f, -480f), new Vector2(200f, 30f));
            for (int i = 0; i < 4; i++)
            {
                if (_gfxPresetButtons[i] != null) continue;
                int tier = i; // copy cho closure
                float x = -210f + i * 140f;
                _gfxPresetButtons[i] = UIBuilder.CreateButton(card,
                    $"GfxPresetButton{i}", GraphicsQuality.TierNames[i],
                    18, () => OnGraphicsPresetButton(tier),
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(x, -515f), new Vector2(132f, 40f));
            }

            // FPS: 30 / 60 / 90 / 120 + đồng hồ FPS thực tế.
            UIBuilder.CreateText(card, "FpsLabel", "FPS",
                22, UIBuilder.TextWhite, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(130f, -558f), new Vector2(60f, 30f));
            for (int i = 0; i < 4; i++)
            {
                if (_fpsButtons[i] != null) continue;
                int fps = GraphicsQuality.FpsOptions[i];
                float x = -168f + i * 72f;
                _fpsButtons[i] = UIBuilder.CreateButton(card,
                    $"FpsButton{i}", $"{fps}",
                    18, () => OnFpsButton(fps),
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(x, -558f), new Vector2(64f, 40f));
            }
            if (_fpsMeterText == null)
                _fpsMeterText = UIBuilder.CreateText(card, "FpsMeterText", "— FPS",
                    20, UIBuilder.TextDim, TextAnchor.MiddleLeft,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(150f, -558f), new Vector2(110f, 30f));

            // Độ phân giải render (60–100%).
            UIBuilder.CreateText(card, "ScaleLabel", "Độ phân giải",
                22, UIBuilder.TextWhite, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(130f, -598f), new Vector2(140f, 30f));
            if (_gfxScaleSlider == null)
            {
                _gfxScaleSlider = UIBuilder.CreateSlider(card, "GfxScaleSlider",
                    0.6f, 1.0f, GraphicsQuality.CurrentScale,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(20f, -598f), new Vector2(240f, 30f));
                _gfxScaleSlider.onValueChanged.AddListener(OnGraphicsScaleSlider);
            }
            if (_gfxScaleText == null)
                _gfxScaleText = UIBuilder.CreateText(card, "GfxScaleText",
                    $"{Mathf.RoundToInt(GraphicsQuality.CurrentScale * 100f)}%",
                    20, UIBuilder.TextDim, TextAnchor.MiddleLeft,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(180f, -598f), new Vector2(60f, 30f));

            // Nút đóng góc phải trên (để chừa chỗ cho mục đồ hoạ ở dưới).
            if (CloseButton == null)
                CloseButton = UIBuilder.CreateButton(card, "CloseButton", "✕",
                    24, OnCloseButton,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(270f, -45f), new Vector2(48f, 44f));
        }

        private bool AllPresetButtonsAssigned()
        {
            if (_presetButtons == null) return false;
            for (int i = 0; i < _presetButtons.Length; i++)
                if (_presetButtons[i] == null) return false;
            return true;
        }
    }
}
