// MobileInputManager.cs — ShuttleLab (Unity 6)
// Điều khiển cảm ứng + bàn phím (fallback test trong Editor).
// Nguồn di chuyển: VirtualJoystick (cảm ứng) hoặc WASD/phím mũi tên.
// Nguồn kỹ thuật: ShotButton (vuốt để ngắm) hoặc phím J/K/L/U/I/O.
// Joystick và nút kỹ thuật là UI EventTrigger riêng nên multi-touch hoạt
// động độc lập, không khoá nhau.
// LƯU Ý: file này nằm trong namespace ShuttleLab.Input nên KHÔNG được viết
// tắt "Input." — mọi chỗ dùng input legacy đều ghi đủ UnityEngine.Input.
// Dead zone joystick 0.15 được áp ở SetJoystick (điểm funnel duy nhất của
// mọi nguồn di chuyển); VirtualJoystick chỉ đưa vector thô đã clamp -1..1.

using System;
using UnityEngine;
using ShuttleLab.Core;
using ShuttleLab.Gameplay;
using ShuttleLab.Match;
using ShuttleLab.Players;

namespace ShuttleLab.Input
{
    public class MobileInputManager : MonoBehaviour
    {
        public const float JoystickDeadZone = 0.15f;

        /// <summary>Vector di chuyển hiện tại (x = trái/phải, y = trước/sau ~ z).</summary>
        public Vector2 MoveInput { get; private set; }

        /// <summary>Loại kỹ thuật + hướng vuốt (vector XZ thế giới, đã chuẩn hoá).</summary>
        public event Action<ShotType, Vector2> OnShotRequested;
        public event Action OnServeRequested;
        public event Action OnPauseRequested;

        [Header("Panels tay trái / tay phải (đảo khi LeftHandMode)")]
        public RectTransform JoystickPanel;
        public RectTransform ShotButtonsPanel;

        private PlayerController _user;
        private MatchManager _match;
        private Vector2 _joystickInput;

        private float _joyAnchorLeft;
        private float _joyAnchorRight;
        private float _btnAnchorLeft;
        private float _btnAnchorRight;
        private float _joyPosX;
        private float _btnPosX;
        private bool _anchorsStored;

        public void Bind(PlayerController user, MatchManager match)
        {
            _user = user;
            _match = match;
            ApplyLeftHandMode(GameStateManager.Instance != null
                && GameStateManager.Instance.LeftHandMode);
        }

        private void Start()
        {
            if (GameStateManager.Instance != null)
                ApplyLeftHandMode(GameStateManager.Instance.LeftHandMode);
        }

        private void Update()
        {
            if (_user == null || _match == null) return;

            Vector2 keyboard = ReadKeyboardMove();
            Vector2 move = keyboard.sqrMagnitude > 0.01f ? keyboard : _joystickInput;
            if (move.sqrMagnitude > 1f) move.Normalize();
            MoveInput = move;

            // Chỉ lái người chơi khi đang trong điểm; ngoài điểm thì đứng yên.
            if (_match.Phase == MatchManager.MatchPhase.Rally
                || _match.Phase == MatchManager.MatchPhase.Serving)
                _user.SetMoveInput(move);
            else
                _user.SetMoveInput(Vector2.zero);

            HandleKeyboardShots();
        }

        // --- API cho UI (VirtualJoystick / ShotButton) -----------------------

        /// <summary>Joystick đưa vector thô (-1..1); dead zone + rescale ở đây.</summary>
        public void SetJoystick(Vector2 v)
        {
            float mag = v.magnitude;
            if (mag < JoystickDeadZone)
            {
                _joystickInput = Vector2.zero;
                return;
            }
            if (mag > 1f) { v /= mag; mag = 1f; }
            // Rescale để vùng sau dead zone vẫn đi hết 0..1.
            _joystickInput = v.normalized * ((mag - JoystickDeadZone) / (1f - JoystickDeadZone));
        }

        public void RequestShot(ShotType type, Vector2 swipeDir)
        {
            if (swipeDir.sqrMagnitude < 0.01f)
                swipeDir = GetDefaultShotDirection();
            OnShotRequested?.Invoke(type, swipeDir.normalized);
        }

        public void RequestServe()
        {
            OnServeRequested?.Invoke();
        }

        /// <summary>
        /// Hướng mặc định khi bấm kỹ thuật mà không vuốt: về góc xa đối diện
        /// vị trí đối thủ hiện tại (MatchManager.Players[1]).
        /// </summary>
        public Vector2 GetDefaultShotDirection()
        {
            if (_match == null || _match.Players == null || _match.Players.Length < 2
                || _match.Players[1] == null)
                return new Vector2(0f, 1f); // thẳng sâu

            Vector3 opp = _match.Players[1].transform.position;
            float side = opp.x >= 0f ? -1f : 1f; // nhắm phía đối diện đối thủ
            Vector2 corner = new Vector2(
                side * (CourtDimensions.SinglesHalfWidth - 0.4f),
                CourtDimensions.CourtHalfLength - 0.5f);
            Vector3 me = _user != null ? _user.transform.position : Vector3.zero;
            Vector2 d = corner - new Vector2(me.x, me.z);
            return d.sqrMagnitude > 0.01f ? d.normalized : new Vector2(0f, 1f);
        }

        // --- Bàn phím ---------------------------------------------------------

        private Vector2 ReadKeyboardMove()
        {
            float x = 0f, y = 0f;
            if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow)) y += 1f;
            if (UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            Vector2 v = new Vector2(x, y);
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }

        private void HandleKeyboardShots()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.J)) FireKeyShot(ShotType.Clear);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.K)) FireKeyShot(ShotType.Smash);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.L)) FireKeyShot(ShotType.Drop);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.U)) FireKeyShot(ShotType.Drive);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.I)) FireKeyShot(ShotType.Lift);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.O)) FireKeyShot(ShotType.NetShot);

            if (UnityEngine.Input.GetKeyDown(KeyCode.Space)) RequestServe();
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) || UnityEngine.Input.GetKeyDown(KeyCode.P))
                OnPauseRequested?.Invoke();
        }

        private void FireKeyShot(ShotType type)
        {
            OnShotRequested?.Invoke(type, GetDefaultShotDirection());
        }

        // --- Tay trái ---------------------------------------------------------

        /// <summary>
        /// LeftHandMode: hoán đổi vị trí ngang của panel joystick và panel
        /// nút kỹ thuật (đổi anchor/anchoredPosition cho nhau). Panel nào
        /// chưa gán thì thử tìm theo tên GameObject; không tìm thấy thì thôi.
        /// Đây là chủ duy nhất của bố cục tay trái — đừng gọi thêm
        /// VirtualJoystick.SetLeftHand (đã xóa) kẻo joystick bị dời 2 lần.
        /// </summary>
        public void ApplyLeftHandMode(bool left)
        {
            if (JoystickPanel == null)
            {
                var go = GameObject.Find("JoystickPanel");
                if (go != null) JoystickPanel = go.GetComponent<RectTransform>();
            }
            if (ShotButtonsPanel == null)
            {
                var go = GameObject.Find("ShotButtonsPanel");
                if (go != null) ShotButtonsPanel = go.GetComponent<RectTransform>();
            }
            if (JoystickPanel == null || ShotButtonsPanel == null) return;

            if (!_anchorsStored)
            {
                _anchorsStored = true;
                _joyAnchorLeft = JoystickPanel.anchorMin.x;
                _joyAnchorRight = JoystickPanel.anchorMax.x;
                _btnAnchorLeft = ShotButtonsPanel.anchorMin.x;
                _btnAnchorRight = ShotButtonsPanel.anchorMax.x;
                _joyPosX = JoystickPanel.anchoredPosition.x;
                _btnPosX = ShotButtonsPanel.anchoredPosition.x;
            }

            if (left)
            {
                SetHorizontalAnchors(JoystickPanel, _btnAnchorLeft, _btnAnchorRight, -_joyPosX);
                SetHorizontalAnchors(ShotButtonsPanel, _joyAnchorLeft, _joyAnchorRight, -_btnPosX);
            }
            else
            {
                SetHorizontalAnchors(JoystickPanel, _joyAnchorLeft, _joyAnchorRight, _joyPosX);
                SetHorizontalAnchors(ShotButtonsPanel, _btnAnchorLeft, _btnAnchorRight, _btnPosX);
            }
        }

        private static void SetHorizontalAnchors(RectTransform rt, float minX, float maxX, float posX)
        {
            Vector2 min = rt.anchorMin; min.x = minX; rt.anchorMin = min;
            Vector2 max = rt.anchorMax; max.x = maxX; rt.anchorMax = max;
            Vector2 pos = rt.anchoredPosition; pos.x = posX; rt.anchoredPosition = pos;
        }
    }
}
