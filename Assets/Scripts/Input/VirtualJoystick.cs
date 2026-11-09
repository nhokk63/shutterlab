// VirtualJoystick.cs — ShuttleLab (Unity 6)
// Joystick cảm ứng trên một RectTransform: tính vector từ tâm tới ngón tay,
// clamp theo bán kính, đưa vector thô (-1..1) cho MobileInputManager.
// Dead zone 0.15 + rescale được áp ở MobileInputManager.SetJoystick (điểm
// funnel duy nhất) để không trừ dead zone 2 lần.
// Knob di chuyển theo ngón tay, thả ra thì về tâm. Multi-touch: joystick
// chỉ nhận đúng pointerId đã chạm vào nó, nút kỹ thuật hoạt động độc lập.

using UnityEngine;
using UnityEngine.EventSystems;

namespace ShuttleLab.Input
{
    public class VirtualJoystick : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [Header("Kéo thả trong Inspector (nếu trống sẽ tự tìm)")]
        public RectTransform knob;
        public MobileInputManager inputManager;

        private RectTransform _bg;
        private int _pointerId = -1;
        private float _origPosX;

        private void Awake()
        {
            _bg = GetComponent<RectTransform>();
            _origPosX = _bg != null ? _bg.anchoredPosition.x : 0f;
            if (knob == null)
            {
                var knobT = transform.Find("Knob");
                if (knobT != null) knob = knobT.GetComponent<RectTransform>();
            }
        }

        private void Start()
        {
            if (inputManager == null)
                inputManager = FindFirstObjectByType<MobileInputManager>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointerId != -1) return;
            _pointerId = eventData.pointerId;
            UpdateKnob(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            UpdateKnob(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            _pointerId = -1;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
            inputManager?.SetJoystick(Vector2.zero);
        }

        private void UpdateKnob(PointerEventData eventData)
        {
            if (_bg == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _bg, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;

            // local tính từ tâm (pivot mặc định 0.5, 0.5); y màn hình lên = +y.
            float maxRadius = Mathf.Min(_bg.rect.width, _bg.rect.height) * 0.5f;
            if (maxRadius <= 1f) return;

            Vector2 clamped = Vector2.ClampMagnitude(local, maxRadius);
            if (knob != null) knob.anchoredPosition = clamped;

            // x -> x thế giới, y (lên màn hình) -> z thế giới (tiến về lưới).
            inputManager?.SetJoystick(clamped / maxRadius);
        }

        /// <summary>
        /// Tay trái: chuyển cả vùng joystick sang phía phải màn hình (và ngược lại).
        /// Chỉ đổi anchor ngang + dấu anchoredPosition.x.
        /// </summary>
        public void SetLeftHand(bool left)
        {
            if (_bg == null) return;
            float anchorX = left ? 1f : 0f;
            Vector2 min = _bg.anchorMin; min.x = anchorX; _bg.anchorMin = min;
            Vector2 max = _bg.anchorMax; max.x = anchorX; _bg.anchorMax = max;
            Vector2 pos = _bg.anchoredPosition;
            pos.x = left ? -Mathf.Abs(_origPosX) : Mathf.Abs(_origPosX);
            _bg.anchoredPosition = pos;
        }
    }
}
