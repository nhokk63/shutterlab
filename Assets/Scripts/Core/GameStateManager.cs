// GameStateManager.cs — ShuttleLab (Unity 6)
// Singleton duy nhất giữ flow của app: menu <-> match <-> career, cùng các
// setting bền vững (độ khó, tên người chơi, preset camera, âm lượng, tay trái).
// Setting được lưu qua PlayerPrefs với key "shuttlelab_*".
// Chỉ gọi SceneManager.LoadScene với tên scene — scene do SceneBuilder tạo;
// file này không tự tạo scene, không chứa logic gameplay.

using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShuttleLab.Core
{
    public class GameStateManager : MonoBehaviour
    {
        public enum AppMode { Menu, QuickMatch, Practice, Career, Tournament }

        public static GameStateManager Instance { get; private set; }
        public static bool HasInstance => Instance != null;

        public const string SceneMainMenu = "MainMenu";
        public const string SceneMatch = "Match";
        public const string SceneCareer = "Career";

        private const string KeyDifficulty = "shuttlelab_difficulty";
        private const string KeyPlayerName = "shuttlelab_player_name";
        private const string KeyCameraPreset = "shuttlelab_camera_preset";
        private const string KeyMusicVolume = "shuttlelab_music_volume";
        private const string KeySfxVolume = "shuttlelab_sfx_volume";
        private const string KeyLeftHand = "shuttlelab_left_hand";

        public AppMode CurrentMode { get; private set; } = AppMode.Menu;

        private int _difficulty = 2;
        /// <summary>Độ khó AI 0..5 (mặc định 2 = Normal/Advanced).</summary>
        public int Difficulty
        {
            get => _difficulty;
            set
            {
                _difficulty = Mathf.Clamp(value, 0, 5);
                PlayerPrefs.SetInt(KeyDifficulty, _difficulty);
                PlayerPrefs.Save();
            }
        }

        private string _playerName = "Bạn";
        public string PlayerName
        {
            get => _playerName;
            set
            {
                _playerName = string.IsNullOrEmpty(value) ? "Bạn" : value;
                PlayerPrefs.SetString(KeyPlayerName, _playerName);
                PlayerPrefs.Save();
            }
        }

        private int _cameraPreset;
        /// <summary>Preset camera 0..5 (xem CameraManager.CamPreset).</summary>
        public int CameraPreset
        {
            get => _cameraPreset;
            set
            {
                _cameraPreset = Mathf.Clamp(value, 0, 5);
                PlayerPrefs.SetInt(KeyCameraPreset, _cameraPreset);
                PlayerPrefs.Save();
            }
        }

        private float _musicVolume = 0.8f;
        public float MusicVolume
        {
            get => _musicVolume;
            set
            {
                _musicVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(KeyMusicVolume, _musicVolume);
                PlayerPrefs.Save();
            }
        }

        private float _sfxVolume = 0.8f;
        public float SfxVolume
        {
            get => _sfxVolume;
            set
            {
                _sfxVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(KeySfxVolume, _sfxVolume);
                PlayerPrefs.Save();
            }
        }

        private bool _leftHandMode;
        public bool LeftHandMode
        {
            get => _leftHandMode;
            set
            {
                _leftHandMode = value;
                PlayerPrefs.SetInt(KeyLeftHand, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
        }

        public void LoadSettings()
        {
            // Ghi thẳng vào backing field để không Save() thừa trong lúc load.
            _difficulty = Mathf.Clamp(PlayerPrefs.GetInt(KeyDifficulty, 2), 0, 5);
            _playerName = PlayerPrefs.GetString(KeyPlayerName, "Bạn");
            if (string.IsNullOrEmpty(_playerName)) _playerName = "Bạn";
            _cameraPreset = Mathf.Clamp(PlayerPrefs.GetInt(KeyCameraPreset, 0), 0, 5);
            _musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMusicVolume, 0.8f));
            _sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfxVolume, 0.8f));
            _leftHandMode = PlayerPrefs.GetInt(KeyLeftHand, 0) != 0;
        }

        public void SaveSettings()
        {
            PlayerPrefs.SetInt(KeyDifficulty, _difficulty);
            PlayerPrefs.SetString(KeyPlayerName, _playerName);
            PlayerPrefs.SetInt(KeyCameraPreset, _cameraPreset);
            PlayerPrefs.SetFloat(KeyMusicVolume, _musicVolume);
            PlayerPrefs.SetFloat(KeySfxVolume, _sfxVolume);
            PlayerPrefs.SetInt(KeyLeftHand, _leftHandMode ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void LoadMenu()
        {
            CurrentMode = AppMode.Menu;
            SceneManager.LoadScene(SceneMainMenu);
        }

        public void LoadMatch(AppMode mode, int difficulty)
        {
            CurrentMode = mode;
            Difficulty = difficulty;
            SaveSettings();
            SceneManager.LoadScene(SceneMatch);
        }

        public void LoadCareer()
        {
            CurrentMode = AppMode.Career;
            SaveSettings();
            SceneManager.LoadScene(SceneCareer);
        }
    }
}
