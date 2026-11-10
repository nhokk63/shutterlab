// GameBootstrap.cs — ShuttleLab (Unity 6)
// "Wiring" duy nhất của scene Match: tìm các manager, tạo PlayerStats,
// Init 2 người chơi, nối input/camera vào MatchManager rồi gọi StartMatch.
// Không chứa logic gameplay — logic nằm ở MatchManager / PlayerController /
// OpponentAI.
//
// LƯU Ý:
// - Dùng FindFirstObjectByType (API Unity 6) thay cho FindObjectOfType đã
//   obsolete, cùng ý nghĩa.
// - HUDManager nối kiểu typed trực tiếp qua FindFirstObjectByType
//   (trước đây dùng GameObject.Find + SendMessage vì file HUD chưa có).
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
using ShuttleLab.UI;

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
            _shuttle = _match.Shuttle;

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

            // User: nhân vật đã chọn trong roster (mặc định id 0, kit áo đỏ /
            // quần tối theo refs Pure Badminton). Tên gõ trong menu chỉ thắng
            // khi khác mặc định "Bạn" — nếu không, giữ tên nhân vật trong
            // roster. SceneBuilder không đặt được kit vì humanoid được Build
            // trong PlayerController.Init lúc chạy; kit nằm trong roster,
            // không ghi đè màu áo/quần tại đây nữa.
            PlayerStats userStats = PlayerStats.FromCharacter(ShuttleLab.Systems.CharacterRoster.SelectedId);
            if (gs != null)
            {
                if (!string.IsNullOrEmpty(gs.PlayerName) && gs.PlayerName != "Bạn")
                    userStats.Name = gs.PlayerName;
                // KHÔNG ghi đè LeftHanded bằng gs.LeftHandMode: đó là cài
                // đặt ĐIỀU KHIỂN (đảo tay cầm joystick/nút), còn tay thuận
                // là thuộc tính của nhân vật trong roster (Đức Anh tay trái).
            }
            // Điểm tập đổi chỉ số (10 điểm = +1, tối đa +10 cộng dồn).
            // Cộng vào chỉ số gốc TRƯỚC khi áp trang bị.
            foreach (ShuttleLab.Systems.TrainingManager.TrainableStat stat
                in System.Enum.GetValues(typeof(ShuttleLab.Systems.TrainingManager.TrainableStat)))
            {
                int bonus = ShuttleLab.Systems.TrainingManager.GetStatBonus(stat);
                if (bonus <= 0) continue;
                switch (stat)
                {
                    case ShuttleLab.Systems.TrainingManager.TrainableStat.Power:
                        userStats.Power += bonus; break;
                    case ShuttleLab.Systems.TrainingManager.TrainableStat.Accuracy:
                        userStats.Accuracy += bonus; break;
                    case ShuttleLab.Systems.TrainingManager.TrainableStat.Speed:
                        userStats.Speed += bonus; break;
                    case ShuttleLab.Systems.TrainingManager.TrainableStat.Stamina:
                        userStats.Stamina += bonus; break;
                    case ShuttleLab.Systems.TrainingManager.TrainableStat.Defense:
                        userStats.Defense += bonus; break;
                }
            }
            userStats.ClampAll();
            // Trang bị đã mua: áp chỉ số trước khi Init người chơi.
            var equipment = new ShuttleLab.Systems.EquipmentManager();
            equipment.LoadFromSave(ShuttleLab.Systems.SaveManager.Current);
            userStats = equipment.ApplyTo(userStats) ?? userStats;
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
                _camera.SetTargets(_shuttle.transform);
                int preset = gs != null ? gs.CameraPreset : 0;
                _camera.SetPreset((CameraManager.CamPreset)Mathf.Clamp(preset, 0, 5));
            }

            // HUD (nếu đã có trong scene): nối kiểu typed trực tiếp.
            FindFirstObjectByType<HUDManager>()?.BindMatch(_match);

            // Quick Match cũng phải có ví Career để nhận thưởng cuối trận:
            // không đi qua màn Career nên tự tạo/nạp session tại đây.
            // Trận tập không tạo career. Session giải đấu đang chạy (Career
            // đã có) thì giữ nguyên, không đụng vào.
            if (!practice)
                EnsureCareerSession();

            _match.StartMatch(difficulty, practice);

            // Âm thanh: nối AudioManager vào event của trận (trước đây
            // AudioManager build xong nhưng không ai gọi nên game câm).
            var audio = FindFirstObjectByType<ShuttleLab.Systems.AudioManager>();
            if (audio != null)
            {
                _match.OnScoreChanged += _ => audio.PlayScore();
                _match.OnMessage += _ => audio.PlayWhistle();
                _match.Players[0].ShotCtrl.OnShotExecuted += (type, _) => audio.PlayHit(type);
                _match.Players[1].ShotCtrl.OnShotExecuted += (type, _) => audio.PlayHit(type);
                if (_shuttle != null)
                    _shuttle.OnLanded += (landing, inSingles, inDoubles) => audio.PlayBounce();
            }
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

        /// <summary>
        /// Đảm bảo có Career đang Active để thưởng xu cuối trận (Quick Match
        /// không đi qua màn Career nên phải tự tạo/nạp tại đây). Chưa có
        /// career trong save thì bắt đầu sự nghiệp mới (500 xu khởi đầu).
        /// </summary>
        private void EnsureCareerSession()
        {
            var session = ShuttleLab.Systems.TournamentSession.Instance;
            if (session == null)
            {
                var go = new GameObject("TournamentSession");
                session = go.AddComponent<ShuttleLab.Systems.TournamentSession>();
            }
            if (session.Career == null)
            {
                var career = new ShuttleLab.Systems.CareerManager();
                career.LoadFromSave(ShuttleLab.Systems.SaveManager.Current);
                session.Career = career;
            }
            else
            {
                // Nạp lại từ save: tiền trong session có thể cũ hơn save
                // (mở khóa nhân vật ở menu / mua đồ ở Career ghi thẳng vào
                // save qua CareerManager mới). Không nạp lại thì lần AddMoney
                // sau trận sẽ ghi đè save bằng số tiền cũ, hoàn tiền đã tiêu.
                session.Career.LoadFromSave(ShuttleLab.Systems.SaveManager.Current);
            }
            if (!session.Career.Active)
                session.Career.StartNewCareer();
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
