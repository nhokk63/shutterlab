// ShotButton.cs — ShuttleLab (Unity 6)
// Nút kỹ thuật trên UI Button: chạm xuống ghi điểm bắt đầu, kéo để ngắm
// (mũi tên xoay theo hướng vuốt), thả ra thì đánh.
// - Vuốt > 30px: hướng đánh = vector vuốt chuẩn hoá.
// - Không vuốt (chạm rồi thả): hướng mặc định về góc xa đối diện đối thủ.
// - Chuyển hướng màn hình -> hướng XZ thế giới: vuốt lên = đánh sâu (+z,
//   về cuối sân đối thủ), vuốt xuống = gần lưới, trái/phải = biên -x/+x
//   (góc nhìn camera sau lưng user).
// - Nút giao cầu riêng (IsServeButton): bấm là RequestServe, không vuốt.
// - Mỗi lần chạm chỉ kích hoạt tối đa 1 lần (_fired).
// LƯU Ý: đừng gán thêm Button.onClick cho các nút này ở SceneBuilder —
// handler pointer ở đây đã là cơ chế kích hoạt duy nhất.

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ShuttleLab.Gameplay;

namespace ShuttleLab.Input
{
    public class ShotButton : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public const float MinSwipePx = 30f;

        public ShotType ShotType = ShotType.Clear;

        /// <summary>Nút giao cầu riêng: bấm là RequestServe, không vuốt.</summary>
        public bool IsServeButton;

        [Header("Mũi tên chỉ hướng (Image con, sprite hướng lên trên) — không bắt buộc")]
        public Image directionArrow;

        public MobileInputManager inputManager;

        private Vector2 _downPos;
        private bool _tracking;
        private bool _fired;

        private void Start()
        {
            if (inputManager == null)
                inputManager = FindFirstObjectByType<MobileInputManager>();
            if (directionArrow != null)
                directionArrow.gameObject.SetActive(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _tracking = true;
            _fired = false;
            _downPos = eventData.position;

            if (IsServeButton)
            {
                _fired = true;
                inputManager?.RequestServe();
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_tracking || IsServeButton || directionArrow == null) return;

            Vector2 delta = eventData.position - _downPos;
            if (delta.magnitude < MinSwipePx)
            {
                directionArrow.gameObject.SetActive(false);
                return;
            }

            // Mũi tên sprite hướng lên (+Y): xoay cho trùng hướng vuốt.
            float angle = Mathf.Atan2(-delta.x, delta.y) * Mathf.Rad2Deg;
            directionArrow.rectTransform.rotation = Quaternion.Euler(0f, 0f, angle);
            directionArrow.gameObject.SetActive(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_tracking) return;
            _tracking = false;
            if (directionArrow != null)
                directionArrow.gameObject.SetActive(false);

            if (IsServeButton || _fired) return;
            _fired = true;

            Vector2 delta = eventData.position - _downPos;
            Vector2 dir;
            if (delta.magnitude >= MinSwipePx)
            {
                // Màn hình: x phải = +x thế giới, y lên = +z thế giới (đánh sâu).
                dir = new Vector2(delta.x, delta.y).normalized;
            }
            else
            {
                dir = inputManager != null
                    ? inputManager.GetDefaultShotDirection()
                    : new Vector2(0f, 1f);
            }

            inputManager?.RequestShot(ShotType, dir);
        }
    }
}
