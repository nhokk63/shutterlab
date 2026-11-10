// MatchManager.cs — ShuttleLab (Unity 6)
// State machine của trận đấu: Serving -> Rally -> PointEnd/GameEnd ->
// MatchEnd. Sở hữu điểm số (qua MatchRules), lượt giao cầu, thống kê trận.
// KHÔNG điều khiển AI di chuyển — OpponentAI tự đọc Phase/State và lái
// Players[1] qua PlayerController. UI (HUD) chỉ đọc qua các event.
//
// QUAN TRỌNG — HỢP ĐỒNG EvaluateLanding (ĐÃ CHỐT 2026-11-09, agent chính):
// MatchRules KHÔNG còn trả -2. Cầu chạm sân là rally kết thúc ngay vì
// ShuttlecockController đặt State = Dead: giao cầu tốt rơi trong ô mà đối
// thủ không đỡ kịp = ăn điểm trực tiếp; cú đánh rơi trong sân = winner —
// cả hai đều trả về lastHitter. Nhánh `result == -2` ở OnShuttleLanded
// chỉ còn là safety net phòng khi Rules cũ lọt vào, không phải luồng chính.
//
// CÁCH ĐẾM THỐNG KÊ (đều quan sát được từ MatchManager, không bịa số):
// - Rallies: mỗi lần LastHitter đổi trong lúc Phase == Rally (một cú đánh
//   qua lại thật sự; quả giao cầu không tính vì nó phát trong phase Serving).
// - ShotsHit: mọi cú đánh + giao cầu đã thực hiện (giao cầu đếm ở DoServe,
//   cú đánh đếm khi quan sát được LastHitter đổi).
// - ShotsGood: cú đánh mà đối thủ đỡ được tiếp, hoặc cú đánh ghi điểm.
//   ServesGood: giao cầu không lỗi (đối thủ đỡ được, hoặc ăn điểm trực tiếp).
// - Errors: điểm kết thúc do lỗi của người đánh cuối (đánh ra ngoài, vướng
//   lưới, giao cầu lỗi).
// - Smashes: đếm ở ShotController.OnShotExecuted (MatchManager đăng ký
//   trong StartMatch) — chỉ đếm khi cầu thật sự launch sau fallback,
//   không đếm vung hụt hay solver fail.
// - Serve/Giao cầu của PlayerController là BẤT ĐỒNG BỘ: Serve()/TryHit()
//   chỉ phát swing, cầu được Launch ở callback onContact của animation.
//   Vì vậy MatchManager không bao giờ kiểm tra Shuttle.State ngay sau khi
//   gọi Serve/TryHit; việc đếm cú đánh dựa vào LastHitter đổi ở Update.

using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using ShuttleLab.Core;
using ShuttleLab.Gameplay;
using ShuttleLab.Input;
using ShuttleLab.Players;

namespace ShuttleLab.Match
{
    [Serializable]
    public class MatchStats
    {
        public int Rallies;       // tổng số cú đánh qua lại trong các rally
        public int Smashes;       // số cú Smash / JumpSmash đã thực hiện
        public int Errors;        // điểm thua do lỗi của người đánh cuối
        public int ShotsHit;      // tổng cú đánh + giao cầu đã thực hiện
        public int ShotsGood;     // cú đánh đối thủ đỡ được hoặc ghi điểm
        public int ServesGood;    // giao cầu không lỗi
        public float MatchTime;   // thời gian thi đấu (giây, không tính pause)
        public int LongestRally;  // rally dài nhất (số cú đánh)

        public float Accuracy => ShotsHit > 0 ? (float)ShotsGood / ShotsHit : 0f;
    }

    public class MatchManager : MonoBehaviour
    {
        public enum MatchPhase { Idle, Serving, Rally, PointEnd, GameEnd, MatchEnd, Paused }

        private const float PointEndDelay = 1.2f;
        private const float GameEndDelay = 2.0f;   // có thời gian đọc "thắng ván"
        private const float AiServeDelay = 0.8f;
        private const float StuckRallyTimeout = 3.0f;

        public MatchPhase Phase { get; private set; } = MatchPhase.Idle;
        public MatchRules Rules { get; private set; }
        public ShuttlecockController Shuttle;
        public PlayerController[] Players = new PlayerController[2];

        public int DifficultyLevel;
        public bool IsPractice;

        public MatchStats Stats { get; private set; }

        public event Action<string> OnMessage;
        public event Action<MatchRules.ScoreState> OnScoreChanged;
        public event Action<int> OnMatchEnd; // winner index

        private MobileInputManager _input;
        private MatchPhase _resumePhase = MatchPhase.Serving;

        private int _lastSeenHitter = -1;
        private int _currentRallyHits;
        private int _pendingShotHitter = -1;
        private bool _pendingIsServe;

        private float _aiServeTimer;
        private float _pointEndTimer;
        private int _pendingPointWinner;
        private bool _pendingMatchWon;
        private float _rallyStuckTimer;
        private bool _shuttleSubscribed;
        private bool _shotsSubscribed;

        private void Awake()
        {
            Rules = new MatchRules();
            Stats = new MatchStats();
            SubscribeShuttle();
        }

        private void Start()
        {
            // Fallback nếu scene không có GameBootstrap: tự nối input khi có.
            if (_input == null)
            {
                var found = FindFirstObjectByType<MobileInputManager>();
                if (found != null) BindInput(found);
            }
        }

        private void OnDestroy()
        {
            UnbindInput();
            UnsubscribeShuttle();
        }

        // --- Public API -----------------------------------------------------

        public void StartMatch(int difficulty, bool practice = false)
        {
            DifficultyLevel = difficulty;
            IsPractice = practice;
            Rules = new MatchRules();
            Rules.Reset();
            Stats = new MatchStats();
            _pendingShotHitter = -1;
            _pendingIsServe = false;

            if (Players != null && Players.Length >= 2
                && Players[0] != null && Players[1] != null)
            {
                Players[0].HomePosition = new Vector3(0f, 0f, -3.0f);
                Players[1].HomePosition = new Vector3(0f, 0f, 3.0f);
            }

            SubscribeShuttle();
            SubscribeShotExecuted();
            Phase = MatchPhase.Serving;
            OnScoreChanged?.Invoke(Rules.Score);
            StartPoint();
        }

        public void StartPoint()
        {
            if (Phase == MatchPhase.MatchEnd) return;
            if (Rules == null) Rules = new MatchRules();
            if (Players == null || Players.Length < 2
                || Players[0] == null || Players[1] == null || Shuttle == null)
            {
                Debug.LogWarning("[MatchManager] StartPoint: Players/Shuttle chưa được gán.");
                return;
            }

            int server = Rules.Score.Server;
            int receiver = Rules.Score.Receiver;
            int serverSide = server == 0 ? -1 : 1;
            int serverCourtX = Rules.ServerCourtXSign();

            Vector3 serverPos = CourtDimensions.ServerPosition(serverSide, serverCourtX);
            Vector3 receiverPos = CourtDimensions.ReceiverPosition(
                Rules.ReceiverSideSign(), Rules.TargetBoxXSign());

            Players[server].ResetForPoint(serverPos);
            Players[receiver].ResetForPoint(receiverPos);

            // Cầu đặt trước mặt server, cao 1.1 m, hơi hướng về lưới.
            Vector3 holdPos = new Vector3(serverPos.x, 1.1f, serverPos.z - serverSide * 0.5f);
            Shuttle.HoldAt(holdPos);

            _currentRallyHits = 0;
            _lastSeenHitter = server;
            _pendingShotHitter = -1;
            _pendingIsServe = false;
            _rallyStuckTimer = 0f;

            Phase = MatchPhase.Serving;
            if (server == 1) _aiServeTimer = AiServeDelay;
            OnScoreChanged?.Invoke(Rules.Score);
        }

        public void DoServe(ShotType serveType)
        {
            if (Phase != MatchPhase.Serving || Rules == null
                || Players == null || Players.Length < 2 || Shuttle == null) return;
            if (!ShotLibrary.IsServe(serveType)) serveType = ShotType.ServeLow;

            int server = Rules.Score.Server;
            if (Players[server] == null) return;

            Vector3 target = CourtDimensions.ServiceBoxCentre(
                Rules.ReceiverSideSign(), Rules.TargetBoxXSign(), doubles: false);

            // Serve() của PlayerController là bất đồng bộ: cầu Launch ở
            // callback onContact của animation, không phải ngay lúc này.
            // Nếu Serve trả false (đang vung / không có cầu), không đổi
            // phase và không đếm stat — để watchdog giao lại sau.
            if (!Players[server].Serve(serveType, target)) return;
            Stats.ShotsHit++;
            _pendingShotHitter = server;
            _pendingIsServe = true;
            _lastSeenHitter = server;
            Phase = MatchPhase.Rally;
        }

        /// <summary>Giao cầu của user (nút Serve / phím Space). Chỉ khi tới lượt user.</summary>
        public void RequestUserServe()
        {
            if (Phase != MatchPhase.Serving) return;
            if (Rules != null && Rules.Score.Server != 0) return;
            DoServe(ShotType.ServeLow);
        }

        public void Pause(bool paused)
        {
            if (paused)
            {
                if (Phase == MatchPhase.Paused || Phase == MatchPhase.Idle
                    || Phase == MatchPhase.MatchEnd) return;
                _resumePhase = Phase;
                Phase = MatchPhase.Paused;
                Time.timeScale = 0f;
            }
            else
            {
                if (Phase != MatchPhase.Paused) return;
                Phase = _resumePhase;
                Time.timeScale = 1f;
            }
        }

        public void QuitToMenu()
        {
            Time.timeScale = 1f;
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.LoadMenu();
            else
                SceneManager.LoadScene("MainMenu");
        }

        /// <summary>
        /// OpponentAI gọi sau mỗi TryHit thành công. Smash KHÔNG còn đếm ở
        /// đây nữa (đếm ở OnShotExecuted — chỉ khi cầu thật sự launch).
        /// </summary>
        public void NotifyShotHit(int hitter, ShotType type)
        {
            // Giữ method để OpponentAI không phải sửa; số liệu rally/smash
            // được đếm ở Update (LastHitter đổi) và OnShotExecuted.
        }

        public void BindInput(MobileInputManager input)
        {
            if (_input == input) return;
            UnbindInput();
            _input = input;
            if (_input == null) return;
            _input.OnShotRequested += OnShotRequested;
            _input.OnServeRequested += OnServeRequested;
            _input.OnPauseRequested += OnPauseRequested;
        }

        public void UnbindInput()
        {
            if (_input == null) return;
            _input.OnShotRequested -= OnShotRequested;
            _input.OnServeRequested -= OnServeRequested;
            _input.OnPauseRequested -= OnPauseRequested;
            _input = null;
        }

        // --- Update ---------------------------------------------------------

        private void Update()
        {
            if (Phase == MatchPhase.Serving || Phase == MatchPhase.Rally
                || Phase == MatchPhase.PointEnd || Phase == MatchPhase.GameEnd)
                Stats.MatchTime += Time.deltaTime;

            // AI tự giao cầu sau 0.8 s khi tới lượt.
            if (Phase == MatchPhase.Serving && Rules != null && Rules.Score.Server == 1)
            {
                _aiServeTimer -= Time.deltaTime;
                if (_aiServeTimer <= 0f)
                {
                    ShotType serve = UnityEngine.Random.value < 0.7f
                        ? ShotType.ServeLow : ShotType.ServeHigh;
                    DoServe(serve);
                }
            }

            // Đếm cú đánh qua lại: LastHitter đổi trong lúc Rally.
            if (Phase == MatchPhase.Rally && Shuttle != null)
            {
                if (Shuttle.State == ShuttlecockController.ShuttleState.Flying)
                {
                    _rallyStuckTimer = 0f;
                    int hitter = Shuttle.LastHitter;
                    if (hitter >= 0 && hitter != _lastSeenHitter)
                    {
                        _lastSeenHitter = hitter;
                        _currentRallyHits++;
                        Stats.Rallies++;
                        Stats.ShotsHit++;
                        // Cú đánh trước đó đã được đối thủ đỡ tiếp -> tốt.
                        ResolvePendingShotAsGood();
                        _pendingShotHitter = hitter;
                        _pendingIsServe = false;
                    }
                }
                else
                {
                    // Cầu chưa bay mà đã ở Rally quá lâu (vd Serve lỗi không
                    // Launch): quay lại giao cầu thay vì kẹt trận.
                    _rallyStuckTimer += Time.deltaTime;
                    if (_rallyStuckTimer > StuckRallyTimeout)
                    {
                        Debug.LogWarning("[MatchManager] Rally kẹt (cầu không bay) — giao lại.");
                        StartPoint();
                    }
                }
            }

            // Hết thời gian chờ sau mỗi điểm.
            if (Phase == MatchPhase.PointEnd || Phase == MatchPhase.GameEnd)
            {
                _pointEndTimer -= Time.deltaTime;
                if (_pointEndTimer <= 0f)
                {
                    if (_pendingMatchWon)
                        FinishMatch(_pendingPointWinner);
                    else
                        StartPoint();
                }
            }
        }

        // --- Landing / điểm -------------------------------------------------

        private void OnShuttleLanded(Vector3 landing, bool inSingles, bool inDoubles)
        {
            if (Phase != MatchPhase.Rally && Phase != MatchPhase.Serving) return;
            if (Rules == null || Shuttle == null) return;

            int lastHitter = Shuttle.LastHitter;
            bool wasServe = Shuttle.LastWasServe;
            int result = Rules.EvaluateLanding(landing, lastHitter, wasServe,
                Shuttle.NetHits, out MatchRules.FaultReason fault, out bool isLet);

            if (result == -1)
            {
                OnMessage?.Invoke("Let — giao lại");
                StartPoint();
                return;
            }

            int winner;
            string detail;
            if (result == -2)
            {
                // Xem ghi chú đầu file: cầu rơi trong sân và đã Dead nên
                // rally không thể tiếp tục — điểm cho người đánh cuối.
                winner = lastHitter;
                detail = wasServe ? "Giao cầu ăn điểm trực tiếp." : "Cầu rơi trong sân.";
            }
            else
            {
                winner = result;
                detail = FaultText(fault);
            }

            EndPoint(winner, detail);
        }

        private void EndPoint(int winner, string detail)
        {
            // Chốt cú đánh cuối: ghi điểm = tốt, lỗi = Errors.
            if (_pendingShotHitter >= 0)
            {
                bool wonByHitter = winner == _pendingShotHitter;
                if (_pendingIsServe)
                {
                    if (wonByHitter) Stats.ServesGood++;
                    else Stats.Errors++;
                }
                else
                {
                    if (wonByHitter) Stats.ShotsGood++;
                    else Stats.Errors++;
                }
                _pendingShotHitter = -1;
                _pendingIsServe = false;
            }

            Stats.LongestRally = Mathf.Max(Stats.LongestRally, _currentRallyHits);

            int gameNo = Rules.Score.CurrentGame;
            bool gameWon, matchWon;
            Rules.AwardPoint(winner, out gameWon, out matchWon);

            _pendingPointWinner = winner;
            _pendingMatchWon = matchWon;

            OnScoreChanged?.Invoke(Rules.Score);

            string who = winner == 0 ? "Bạn" : "Đối thủ";
            OnMessage?.Invoke($"Điểm cho {who}! {detail}".Trim());

            if (gameWon && !matchWon)
            {
                OnMessage?.Invoke($"{who} thắng ván {gameNo}!");
                Phase = MatchPhase.GameEnd;
                _pointEndTimer = GameEndDelay;
            }
            else
            {
                Phase = MatchPhase.PointEnd;
                _pointEndTimer = PointEndDelay;
            }
        }

        private void FinishMatch(int winner)
        {
            Phase = MatchPhase.MatchEnd;
            if (Players != null && Players.Length >= 2
                && Players[0] != null && Players[1] != null)
            {
                Players[winner].PlayCelebrate();
                Players[1 - winner].PlayDisappointed();
            }
            OnMessage?.Invoke(winner == 0 ? "Bạn thắng trận!" : "Đối thủ thắng trận!");
            OnMatchEnd?.Invoke(winner);
        }

        // --- Input callbacks -------------------------------------------------

        private void OnShotRequested(ShotType type, Vector2 dir)
        {
            if (Phase != MatchPhase.Rally) return;
            if (Players == null || Players.Length < 2
                || Players[0] == null || Shuttle == null) return;
            if (ShotLibrary.IsServe(type)) return; // giao cầu chỉ dùng nút Serve

            Vector3 target = ComputeShotTarget(type, dir);
            Players[0].TryHit(type, target);
            // TryHit true = đã vung vợt (kể cả vung hụt); cú đánh qua lại
            // thật sự được đếm khi LastHitter đổi ở Update, smash được đếm
            // ở OnShotExecuted khi cầu launch.
        }

        private void OnServeRequested()
        {
            RequestUserServe();
        }

        private void OnPauseRequested()
        {
            Pause(Phase != MatchPhase.Paused);
        }

        /// <summary>
        /// Hướng vuốt (XZ thế giới, y của Vector2 = chiều sâu z) -> điểm rơi
        /// trên nửa sân đối thủ. Mặc định nhắm khoảng trống đối diện đối thủ,
        /// vuốt đẩy điểm rơi theo hướng vuốt, clamp trong sân đơn.
        /// </summary>
        private Vector3 ComputeShotTarget(ShotType type, Vector2 dir)
        {
            Vector3 oppPos = Players[1] != null
                ? Players[1].transform.position : Vector3.zero;

            float x = -oppPos.x;   // khoảng trống đối diện đối thủ
            float z = 5.2f;        // sâu mặc định
            x += dir.x * 2.2f;
            z += dir.y * 2.2f;

            if (ShotLibrary.IsNetShot(type))
                z = Mathf.Min(z, 2.6f);           // kỹ thuật lưới ở gần lưới
            else if (type == ShotType.Clear || type == ShotType.Lift)
                z = Mathf.Max(z, 4.2f);           // clear/lift đi sâu

            float hw = CourtDimensions.SinglesHalfWidth - 0.35f;
            x = Mathf.Clamp(x, -hw, hw);
            z = Mathf.Clamp(z, 0.6f, CourtDimensions.CourtHalfLength - 0.35f);
            return new Vector3(x, 0f, z);
        }

        // --- Helpers ---------------------------------------------------------

        private void ResolvePendingShotAsGood()
        {
            if (_pendingShotHitter < 0) return;
            if (_pendingIsServe) Stats.ServesGood++;
            else Stats.ShotsGood++;
        }

        private void SubscribeShuttle()
        {
            if (_shuttleSubscribed || Shuttle == null) return;
            Shuttle.OnLanded += OnShuttleLanded;
            _shuttleSubscribed = true;
        }

        private void UnsubscribeShuttle()
        {
            if (!_shuttleSubscribed || Shuttle == null) return;
            Shuttle.OnLanded -= OnShuttleLanded;
            _shuttleSubscribed = false;
        }

        /// <summary>Đếm smash qua ShotController.OnShotExecuted — chỉ đếm khi
        /// cầu thật sự được launch (sau fallback), không đếm vung hụt hay
        /// solver fail như cách đếm ở TryHit/NotifyShotHit trước đây.</summary>
        private void SubscribeShotExecuted()
        {
            if (_shotsSubscribed || Players == null || Players.Length < 2) return;
            foreach (var p in Players)
            {
                if (p != null && p.ShotCtrl != null)
                    p.ShotCtrl.OnShotExecuted += OnShotExecuted;
            }
            _shotsSubscribed = true;
        }

        private void OnShotExecuted(ShotType type, ShotQuality quality)
        {
            if (type == ShotType.Smash || type == ShotType.JumpSmash)
                Stats.Smashes++;
        }

        private static string FaultText(MatchRules.FaultReason fault)
        {
            switch (fault)
            {
                case MatchRules.FaultReason.Out: return "Cầu ra ngoài.";
                case MatchRules.FaultReason.IntoNet: return "Cầu vướng lưới.";
                case MatchRules.FaultReason.ShortServe: return "Giao cầu chưa qua vạch ngắn.";
                case MatchRules.FaultReason.ServeOut: return "Giao cầu ra ngoài ô.";
                case MatchRules.FaultReason.NoCross: return "Cầu không qua lưới.";
                case MatchRules.FaultReason.DoubleHit: return "Đánh cầu hai lần.";
                default: return string.Empty;
            }
        }
    }
}
