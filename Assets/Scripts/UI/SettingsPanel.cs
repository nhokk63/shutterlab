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
//  - Nút Đóng.
//
// GameObject & wiring cho SceneBuilder:
//  - GameObject "SettingsPanel" (con của Canvas của scene chứa nó).
//    Component này tự dựng UI bằng code trong Awake nếu chưa gán qua
//    Inspector — SceneBuilder chỉ cần tạo GameObject rỗng tên
//    "SettingsPanel" + gắn component. Mặc định ẩn cho tới khi Show() gọi.
//  - LƯU Ý TRUNG THỰC: slider âm lượng chỉ LƯU giá trị vào
//    GameStateManager (PlayerPrefs "shuttlelab_*"); việc phát âm thanh
//    thuộc AudioManager — module đó chưa viết nên chưa có âm thanh nào
//    phát ra để chỉnh. Panel ghi rõ điều này bằng một dòng chú thích nhỏ,
//    không giả vờ đang chỉnh được tiếng.
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
                && LeftHandToggle != null && CloseButton != null)
                return; // đã gán đủ qua Inspector thì không dựng lại

            var bg = UIBuilder.CreatePanel(transform, "SettingsBackground",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0f, 0f, 0f, 0.7f));

            var card = UIBuilder.CreatePanel(bg, "SettingsCard",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(620f, 560f), UIBuilder.PanelSolid);

            UIBuilder.CreateText(card, "SettingsTitle", "Cài đặt",
                32, UIBuilder.TextWhite, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -45f), new Vector2(560f, 44f));

            // Nhạc.
            UIBuilder.CreateText(card, "MusicLabel", "Nhạc nền",
                22, UIBuilder.TextWhite, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(130f, -110f), new Vector2(140f, 30f));
            MusicSlider = UIBuilder.CreateSlider(card, "MusicSlider",
                0f, 1f, 0.8f,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(50f, -110f), new Vector2(300f, 30f));
            MusicSlider.onValueChanged.AddListener(OnMusicSlider);
            MusicValueText = UIBuilder.CreateText(card, "MusicValueText", "80%",
                22, UIBuilder.TextDim, TextAnchor.MiddleLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(230f, -110f), new Vector2(70f, 30f));

            // Hiệu ứng.
            UIBuilder.CreateText(card, "SfxLabel", "Hiệu ứng",
                22, UIBuilder.TextWhite, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(130f, -160f), new Vector2(140f, 30f));
            SfxSlider = UIBuilder.CreateSlider(card, "SfxSlider",
                0f, 1f, 0.8f,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(50f, -160f), new Vector2(300f, 30f));
            SfxSlider.onValueChanged.AddListener(OnSfxSlider);
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
            LeftHandToggle = UIBuilder.CreateToggle(card, "LeftHandToggle",
                "Chế độ tay trái (đổi joystick <-> nút đánh)", 18, false,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(46f, -395f), new Vector2(32f, 32f));
            LeftHandToggle.onValueChanged.AddListener(OnLeftHandToggle);

            // Độ khó (chỉ đọc).
            DifficultyText = UIBuilder.CreateText(card, "DifficultyText",
                "Độ khó AI: Advanced (2) — chọn ở menu chính",
                20, UIBuilder.TextDim, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -445f), new Vector2(560f, 28f));

            // Ghi chú trung thực về âm thanh.
            UIBuilder.CreateText(card, "AudioNoteText",
                "Mức âm lượng được lưu ngay; âm thanh sẽ phát khi có AudioManager.",
                16, UIBuilder.TextDim, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -480f), new Vector2(580f, 24f));

            CloseButton = UIBuilder.CreateButton(card, "CloseButton", "Đóng",
                24, OnCloseButton,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(220f, 52f));
        }
    }
}
