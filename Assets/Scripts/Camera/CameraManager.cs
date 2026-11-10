// CameraManager.cs — ShuttleLab (Unity 6)
// 6 preset camera, lerp mượt vị trí + góc xoay (tốc độ 5/s), clamp y >= 0.5.
// Preset mặc định đọc từ GameStateManager.CameraPreset.
// DynamicFollow bám theo cầu; BehindPlayer nâng y camera theo độ cao cầu
// một phần khi cầu bay cao.
// LƯU Ý: file nằm trong namespace ShuttleLab.Camera nên kiểu Camera của
// Unity phải ghi đủ UnityEngine.Camera (không viết tắt "Camera").

using UnityEngine;
using ShuttleLab.Core;

namespace ShuttleLab.Camera
{
    public class CameraManager : MonoBehaviour
    {
        public enum CamPreset
        {
            BehindPlayer = 0,
            Elevated = 1,
            Broadcast = 2,
            DynamicFollow = 3,
            Side = 4,
            Tactical = 5
        }

        public CamPreset CurrentPreset { get; private set; } = CamPreset.BehindPlayer;

        [Header("Tốc độ lerp (1/s)")]
        public float FollowSpeed = 5f;

        private Transform _shuttle;

        private UnityEngine.Camera _cam;
        private bool _positioned;

        private void Awake()
        {
            _cam = GetComponent<UnityEngine.Camera>();
            if (_cam == null) _cam = UnityEngine.Camera.main;
            if (_cam == null)
            {
                Debug.LogError("[CameraManager] Không có Camera trên cùng GameObject và không có MainCamera.");
                enabled = false;
            }
        }

        private void Start()
        {
            if (GameStateManager.Instance != null)
            {
                int preset = Mathf.Clamp(GameStateManager.Instance.CameraPreset, 0, 5);
                SetPreset((CamPreset)preset);
            }
        }

        public void SetPreset(CamPreset preset)
        {
            CurrentPreset = preset;
        }

        public void SetTargets(Transform shuttle)
        {
            _shuttle = shuttle;
        }

        private void LateUpdate()
        {
            if (_cam == null) return;

            GetDesired(out Vector3 desiredPos, out Vector3 lookAt);

            // Không xuyên vật thể theo yêu cầu tối thiểu: không xuống dưới 0.5 m.
            desiredPos.y = Mathf.Max(desiredPos.y, 0.5f);

            Vector3 toLook = lookAt - desiredPos;
            Quaternion desiredRot = toLook.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(toLook, Vector3.up)
                : _cam.transform.rotation;

            if (!_positioned)
            {
                _cam.transform.position = desiredPos;
                _cam.transform.rotation = desiredRot;
                _positioned = true;
                return;
            }

            float t = 1f - Mathf.Exp(-FollowSpeed * Time.deltaTime);
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, desiredPos, t);
            _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, desiredRot, t);
        }

        private void GetDesired(out Vector3 pos, out Vector3 lookAt)
        {
            switch (CurrentPreset)
            {
                case CamPreset.BehindPlayer:
                    pos = new Vector3(0f, 6.5f, -10.5f);
                    lookAt = new Vector3(0f, 1f, 2f);
                    // Cầu bay cao thì nâng camera theo một phần.
                    pos.y += ShuttleLift();
                    break;

                case CamPreset.Elevated:
                    pos = new Vector3(0f, 10f, -8f);
                    lookAt = Vector3.zero;
                    break;

                case CamPreset.Broadcast:
                    pos = new Vector3(7f, 5f, -9f);
                    lookAt = new Vector3(0f, 0.5f, 1f);
                    break;

                case CamPreset.DynamicFollow:
                    if (_shuttle != null)
                    {
                        pos = _shuttle.position + new Vector3(0f, 4f, -7f);
                        lookAt = _shuttle.position;
                    }
                    else
                    {
                        pos = new Vector3(0f, 6.5f, -10.5f);
                        lookAt = new Vector3(0f, 1f, 2f);
                    }
                    break;

                case CamPreset.Side:
                    pos = new Vector3(10f, 3f, 0f);
                    lookAt = new Vector3(0f, 1f, 0f);
                    break;

                case CamPreset.Tactical: // gần như nhìn thẳng xuống sân
                    pos = new Vector3(0f, 13f, -2f);
                    lookAt = Vector3.zero;
                    break;

                default:
                    pos = new Vector3(0f, 6.5f, -10.5f);
                    lookAt = new Vector3(0f, 1f, 2f);
                    break;
            }
        }

        /// <summary>
        /// Phần nâng y theo độ cao cầu (BehindPlayer): 30% độ cao vượt 2 m,
        /// tối đa +2.5 m. Cầu thấp thì không nâng.
        /// </summary>
        private float ShuttleLift()
        {
            if (_shuttle == null) return 0f;
            return Mathf.Clamp((_shuttle.position.y - 2f) * 0.3f, 0f, 2.5f);
        }
    }
}
