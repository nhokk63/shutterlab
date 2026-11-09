// GameBootstrap.cs — ShuttleLab (Unity 6)
// "Wiring" duy nhất của scene Match: tìm các manager, tạo PlayerStats,
// Init 2 người chơi, nối input/camera vào MatchManager rồi gọi StartMatch.
// Không chứa logic gameplay — logic nằm ở MatchManager / PlayerController /
// OpponentAI.
//
// LƯU Ý:
// - Dùng FindFirstObjectByType (API Unity 6) thay cho FindObjectOfType đã
//   obsolete, cùng ý nghĩa.
// - HUDManager chưa tồn tại lúc viết file này nên KHÔNG tham chiếu kiểu
//   HUDManager trực tiếp (sẽ vỡ biên dịch nếu UI agent chưa giao file).
//   Thay vào đó: tìm GameObject tên "HUDManager" và SendMessage("BindMatch")
//   nếu có; cách nối chính là HUD tự tìm MatchManager và đăng ký các event
//   OnScoreChanged / OnMessage / OnMatchEnd của nó.
// - OpponentAI (2026-11-09, AI module): được AddComponent lên GameObject
//   của AI player (index 1) + Init với difficulty từ GameStateManager,
//   trước khi StartMatch chạy. AI KHÔNG tự giao cầu — MatchManager đã
//   giao cầu cho AI (xem OpponentAI.cs).
// - Nút Pause/Quit trên UI nối vào 2 hàm public OnPauseButton /
//   OnQuitButton (gán qua Inspector/UnityEvent lúc SceneBuilder dựng HUD).

using UnityEngine;
using ShuttleLab.AI;
using ShuttleLab.Camera;
using ShuttleLab.Gameplay;
using ShuttleLab.Input;
using ShuttleLab.Match;
using ShuttleLab.Players;

namespace ShuttleLab.Core
{
    public class GameBootstrap : MonoBehaviour
    {
        private MatchManager _match;
        private ShuttlecockController _shuttle;
        private MobileInputManager _input;
        private CameraManager _camera;

        private void Awake()
        {
            // Vào thẳng scene Match (không qua menu) thì chưa có singleton.
            if (GameStateManager.Instance == null)
            {
                var go = new GameObject("GameStateManager");
                go.AddComponent<GameStateManager>();
            }
        }

        private void Start()
        {
            _match = FindFirstObjectByType<MatchManager>();
            _shuttle = FindFirstObjectByType<ShuttlecockController>();
            _input = FindFirstObjectByType<MobileInputManager>();
            _camera = FindFirstObjectByType<CameraManager>();

            if (_match == null)
            {
                Debug.LogError("[GameBootstrap] Không tìm thấy MatchManager trong scene Match.");
                return;
            }

            if (_shuttle != null && _match.Shuttle == null)
                _match.Shuttle = _shuttle;

            EnsurePlayers();

            if (_match.Players == null || _match.Players.Length < 2
                || _match.Players[0] == null || _match.Players[1] == null
                || _shuttle == null)
            {
                Debug.LogError("[GameBootstrap] Thiếu người chơi hoặc cầu — không thể bắt đầu trận.");
                return;
            }

            GameStateManager gs = GameStateManager.Instance;
            int difficulty = gs != null ? gs.Difficulty : 2;
            bool practice = gs != null && gs.CurrentMode == GameStateManager.AppMode.Practice;

            // User: DefaultPlayer, tay phải/trái theo setting sẽ áp sau qua stats.
            PlayerStats userStats = PlayerStats.DefaultPlayer();
            if (gs != null)
            {
                userStats.Name = gs.PlayerName;
                userStats.LeftHanded = gs.LeftHandMode;
            }
            // Kit theo refs (Pure Badminton press kit, chỉ tham khảo màu):
            // user áo đỏ / quần tối, AI áo trắng / quần xanh. DefaultPlayer
            // cho áo xanh và FromDifficulty cho áo đỏ — ngược với refs — nên
            // ghi đè tại đây. SceneBuilder không đặt được kit vì humanoid
            // được Build trong PlayerController.Init lúc chạy.
            userStats.ShirtColor = new Color(0.75f, 0.15f, 0.15f); // đỏ
            userStats.ShortsColor = new Color(0.08f, 0.08f, 0.10f); // tối
            PlayerStats aiStats = PlayerStats.FromDifficulty(difficulty);
            aiStats.Name = PlayerStats.DifficultyName(difficulty);
            aiStats.ShirtColor = new Color(0.92f, 0.92f, 0.95f);   // trắng
            aiStats.ShortsColor = new Color(0.10f, 0.35f, 0.75f);  // xanh

            _match.Players[0].Init(0, userStats, _shuttle, userControlled: true);
            _match.Players[1].Init(1, aiStats, _shuttle, userControlled: false);

            // AI đối thủ: lái Players[1] qua PlayerController (không chạm
            // điểm số / luật). Phải Init trước StartMatch để Update của nó
            // kịp quan sát phase Serving/Rally từ điểm đầu tiên.
            // GetComponent trước để không gắn trùng nếu Start chạy lại.
            OpponentAI opponentAI = _match.Players[1].gameObject.GetComponent<OpponentAI>()
                ?? _match.Players[1].gameObject.AddComponent<OpponentAI>();
            opponentAI.Init(_match.Players[1], _match, difficulty);

            if (_input != null)
            {
                _input.Bind(_match.Players[0], _match);
                _match.BindInput(_input);
            }

            if (_camera != null)
            {
                _camera.SetTargets(_shuttle.transform,
                    _match.Players[0].transform, _match.Players[1].transform);
                int preset = gs != null ? gs.CameraPreset : 0;
                _camera.SetPreset((CameraManager.CamPreset)Mathf.Clamp(preset, 0, 5));
            }

            // HUD (nếu đã có trong scene): nối kiểu không phụ thuộc type.
            GameObject hudGO = GameObject.Find("HUDManager");
            if (hudGO != null)
                hudGO.SendMessage("BindMatch", _match, SendMessageOptions.DontRequireReceiver);

            _match.StartMatch(difficulty, practice);
        }

        /// <summary>
        /// Players thường đã được gán sẵn trong Inspector bởi SceneBuilder.
        /// Fallback: tìm PlayerController trong scene, người ở nửa sân gần
        /// (z nhỏ hơn) là user (index 0), người còn lại là AI (index 1).
        /// </summary>
        private void EnsurePlayers()
        {
            if (_match.Players == null || _match.Players.Length < 2)
                _match.Players = new PlayerController[2];

            if (_match.Players[0] == null || _match.Players[1] == null)
            {
                var found = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                System.Array.Sort(found,
                    (a, b) => a.transform.position.z.CompareTo(b.transform.position.z));
                if (_match.Players[0] == null && found.Length > 0)
                    _match.Players[0] = found[0];
                if (_match.Players[1] == null && found.Length > 1)
                    _match.Players[1] = found[1];
            }
        }

        // --- Nút UI (gán qua Inspector/UnityEvent) ---------------------------

        /// <summary>Nút Pause: bật/tắt tạm dừng qua MatchManager.</summary>
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

        /// <summary>Nút Quit: về menu chính qua MatchManager.</summary>
        public void OnQuitButton()
        {
            if (_match == null) return;
            _match.QuitToMenu();
        }
    }
}
