// PlayerAnimationController.cs — ShuttleLab (Unity 6, URP)
// Procedural pose animation: no animation assets. A Pose is a dictionary of
// joint-name -> local rotation; a Clip is keyframes (time, pose, root Y
// offset). Shot clips are built as windup -> contact -> follow-through ->
// recover, each with a ContactTime so the swing's onContact callback fires
// exactly when the racket meets the shuttle. Clips blend over 0.12 s.
// The Run loop is generated procedurally each frame (legs pedal on a sine,
// arms swing opposite) and follows the run speed.
// Joint Euler conventions (character faces +Z, limbs hang down -Y):
//   shoulder/hip X negative = limb swings forward, X positive = back;
//   elbow/knee X positive = joint bends (forearm/shin goes back).
// Angles are design approximations, tuned by eye in the Calibration Scene.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShuttleLab.Players
{
    /// <summary>One keyframe: a full-body pose at a time, plus root lift.</summary>
    public class AnimKeyframe
    {
        public float Time;
        public Dictionary<string, Quaternion> Pose;
        public float RootOffsetY;

        public AnimKeyframe(float time, Dictionary<string, Quaternion> pose, float rootOffsetY = 0f)
        {
            Time = time;
            Pose = pose;
            RootOffsetY = rootOffsetY;
        }
    }

    /// <summary>
    /// A procedural clip. ContactTime &gt;= 0 only for shot clips: seconds from
    /// clip start until the racket contacts the shuttle.
    /// </summary>
    public class AnimClip
    {
        public string Name;
        public AnimKeyframe[] Keyframes;
        public float Duration;
        public float ContactTime = -1f;
        public bool Loop;

        public AnimClip(string name, AnimKeyframe[] keyframes, float contactTime = -1f, bool loop = false)
        {
            Name = name;
            Keyframes = keyframes;
            Duration = keyframes.Length > 0 ? keyframes[keyframes.Length - 1].Time : 0f;
            ContactTime = contactTime;
            Loop = loop;
        }

        public bool IsShotClip => ContactTime >= 0f;
    }

    /// <summary>
    /// Retarget một pose procedural (local rotation tuyệt đối theo rig
    /// HumanoidBuilder) sang một rig khác (vd model CC0 import với bind
    /// pose T-pose). Implementer tự map xương; xem CCOPoseRetargeter.
    /// </summary>
    public interface IJointPoseRetargeter
    {
        void ApplyPose(Dictionary<string, Quaternion> pose, HumanoidRig rig);
    }

    public class PlayerAnimationController : MonoBehaviour
    {
        public const float BlendDuration = 0.12f;

        /// <summary>
        /// Retargeter tùy chọn (vd CCOPoseRetargeter cho model CC0 import):
        /// khi có, ApplyPose không gán localRotation trực tiếp nữa mà đi qua
        /// retargeter. Null = rig procedural (mặc định).
        /// </summary>
        public IJointPoseRetargeter Retargeter { get; set; }

        /// <summary>All joint names the pose system knows about.</summary>
        public static readonly string[] JointNames =
        {
            "Hips", "Spine", "Chest", "Neck", "Head",
            "ShoulderL", "ShoulderR", "ElbowL", "ElbowR", "WristL", "WristR",
            "HipL", "HipR", "KneeL", "KneeR", "AnkleL", "AnkleR"
        };

        /// <summary>True while a non-looping clip (e.g. a shot swing) is playing.</summary>
        public bool IsBusy => _current != null && !_current.Loop;

        /// <summary>True while a shot clip is playing (contact may still come).</summary>
        public bool IsPlayingShot => _current != null && _current.IsShotClip && _clipTime < _current.Duration;

        public string CurrentClipName => _current != null ? _current.Name : null;

        private HumanoidRig _rig;
        private Dictionary<string, Transform> _joints;
        private Dictionary<string, AnimClip> _clips;

        private AnimClip _current;
        private float _clipTime;
        private float _speed = 1f;
        private Action _onContact;
        private Action _onEnd;
        private bool _contactFired;

        private Dictionary<string, Quaternion> _blendFrom;
        private float _blendTimer;

        /// <summary>
        /// Pose procedural đã áp lần cuối (không gian procedural). Dùng làm
        /// nguồn blend khi có Retargeter — KHÔNG được đọc localRotation của
        /// xương đã retarget vì đó là không gian khác (xem SnapshotBlendFrom).
        /// </summary>
        private Dictionary<string, Quaternion> _lastProceduralPose;

        // Procedural Run state.
        private bool _proceduralRun;
        private float _runPhase;
        private float _runIntensity = 1f;

        private void Awake()
        {
            _clips = AnimClipLibrary.BuildAll();
        }

        /// <summary>Bind the joint transforms built by HumanoidBuilder.</summary>
        public void Bind(HumanoidRig rig)
        {
            _rig = rig;
            _joints = new Dictionary<string, Transform>
            {
                { "Hips", rig.Hips }, { "Spine", rig.Spine }, { "Chest", rig.Chest },
                { "Neck", rig.Neck }, { "Head", rig.Head },
                { "ShoulderL", rig.ShoulderL }, { "ShoulderR", rig.ShoulderR },
                { "ElbowL", rig.ElbowL }, { "ElbowR", rig.ElbowR },
                { "WristL", rig.WristL }, { "WristR", rig.WristR },
                { "HipL", rig.HipL }, { "HipR", rig.HipR },
                { "KneeL", rig.KneeL }, { "KneeR", rig.KneeR },
                { "AnkleL", rig.AnkleL }, { "AnkleR", rig.AnkleR }
            };
            _lastProceduralPose = null; // rig mới: chưa có pose để blend từ
            PlayLoop("Ready");
        }

        public AnimClip GetClip(string name)
        {
            if (_clips == null) _clips = AnimClipLibrary.BuildAll();
            _clips.TryGetValue(name, out AnimClip clip);
            return clip;
        }

        public float GetClipDuration(string name)
        {
            AnimClip clip = GetClip(name);
            return clip != null ? clip.Duration : 0.5f;
        }

        /// <summary>
        /// Play a one-shot clip. onContact fires once at ContactTime (shot
        /// clips); onEnd fires when the clip finishes, after which the
        /// controller returns to the Ready loop.
        /// </summary>
        public void PlayClip(AnimClip clip, float speed, Action onContact, Action onEnd)
        {
            if (clip == null) return;
            SnapshotBlendFrom();
            _current = clip;
            _clipTime = 0f;
            _speed = Mathf.Max(0.05f, speed);
            _onContact = onContact;
            _onEnd = onEnd;
            _contactFired = false;
            _proceduralRun = false;
        }

        /// <summary>Play a looping clip: "Idle", "Ready" or "Run".</summary>
        public void PlayLoop(string name)
        {
            AnimClip clip = GetClip(name);
            if (clip == null) return;
            if (_current == clip && _current.Loop) return;
            SnapshotBlendFrom();
            _current = clip;
            _clipTime = 0f;
            _speed = 1f;
            _onContact = null;
            _onEnd = null;
            _contactFired = false;
            _proceduralRun = name == "Run";
            if (_proceduralRun) _runPhase = 0f;
        }

        /// <summary>Run-loop intensity 0..1.5: scales the procedural leg cadence.</summary>
        public void SetRunSpeed(float speedFactor)
        {
            _runIntensity = Mathf.Clamp(speedFactor, 0f, 1.5f);
        }

        /// <summary>Stop the current clip and return to Ready.</summary>
        public void StopClip()
        {
            _onEnd = null;
            _onContact = null;
            PlayLoop("Ready");
        }

        private void Update()
        {
            if (_current == null || _joints == null || _rig == null) return;

            float dt = Time.deltaTime;
            _clipTime += dt * _speed;
            if (_blendTimer < BlendDuration) _blendTimer += dt;

            if (_proceduralRun)
            {
                _runPhase += dt * _speed * (6f + 9f * _runIntensity);
                ApplyPose(BuildRunPose(), SampleRootY(_clipTime));
                return;
            }

            if (_current.Loop && _current.Duration > 0f)
                _clipTime %= _current.Duration;

            // Contact callback, exactly at ContactTime.
            if (_current.IsShotClip && !_contactFired && _clipTime >= _current.ContactTime)
            {
                _contactFired = true;
                _onContact?.Invoke();
            }

            Dictionary<string, Quaternion> pose = SamplePose(_clipTime);
            float rootY = SampleRootY(_clipTime);
            ApplyPose(pose, rootY);

            if (!_current.Loop && _clipTime >= _current.Duration)
            {
                Action end = _onEnd;
                _onEnd = null;
                _onContact = null;
                _current = null;
                end?.Invoke();
                PlayLoop("Ready");
            }
        }

        // --- Pose sampling / application --------------------------------------

        private Dictionary<string, Quaternion> SamplePose(float t)
        {
            AnimKeyframe[] keys = _current.Keyframes;
            if (keys.Length == 0) return IdentityPose();
            if (keys.Length == 1 || t <= keys[0].Time) return keys[0].Pose;
            if (t >= keys[keys.Length - 1].Time) return keys[keys.Length - 1].Pose;

            for (int i = 0; i < keys.Length - 1; i++)
            {
                if (t >= keys[i].Time && t <= keys[i + 1].Time)
                {
                    float f = (t - keys[i].Time) / Mathf.Max(1e-5f, keys[i + 1].Time - keys[i].Time);
                    return LerpPose(keys[i].Pose, keys[i + 1].Pose, f);
                }
            }
            return keys[keys.Length - 1].Pose;
        }

        private float SampleRootY(float t)
        {
            AnimKeyframe[] keys = _current.Keyframes;
            if (keys.Length == 0) return 0f;
            if (keys.Length == 1 || t <= keys[0].Time) return keys[0].RootOffsetY;
            if (t >= keys[keys.Length - 1].Time) return keys[keys.Length - 1].RootOffsetY;
            for (int i = 0; i < keys.Length - 1; i++)
            {
                if (t >= keys[i].Time && t <= keys[i + 1].Time)
                {
                    float f = (t - keys[i].Time) / Mathf.Max(1e-5f, keys[i + 1].Time - keys[i].Time);
                    return Mathf.Lerp(keys[i].RootOffsetY, keys[i + 1].RootOffsetY, f);
                }
            }
            return 0f;
        }

        private void ApplyPose(Dictionary<string, Quaternion> pose, float rootOffsetY)
        {
            if (_blendTimer < BlendDuration && _blendFrom != null)
            {
                float f = Mathf.Clamp01(_blendTimer / BlendDuration);
                pose = LerpPose(_blendFrom, pose, f);
            }

            if (Retargeter != null)
            {
                // Rig import (vd CC0): pose procedural được retarget sang
                // xương của rig đó; _lastProceduralPose giữ pose gốc để
                // blend lần sau (không gian procedural, không phải local
                // rotation đã retarget của xương).
                Retargeter.ApplyPose(pose, _rig);
                _lastProceduralPose = pose;
            }
            else
            {
                foreach (var kv in pose)
                {
                    if (_joints.TryGetValue(kv.Key, out Transform joint) && joint != null)
                        joint.localRotation = kv.Value;
                }
            }

            if (_rig.Root != null)
            {
                Vector3 p = _rig.Root.localPosition;
                p.y = rootOffsetY;
                _rig.Root.localPosition = p;
            }
        }

        private void SnapshotBlendFrom()
        {
            _blendFrom = new Dictionary<string, Quaternion>();
            if (Retargeter != null)
            {
                // Blend trong không gian pose procedural: localRotation hiện
                // tại của xương CC0 đã qua retarget (bind * delta) KHÔNG
                // cùng không gian với pose procedural nên không slerp được
                // với pose mới — lấy pose procedural lần cuối thay thế.
                if (_lastProceduralPose != null)
                    foreach (var kv in _lastProceduralPose)
                        _blendFrom[kv.Key] = kv.Value;
                _blendTimer = 0f;
                return;
            }
            if (_joints == null) return;
            foreach (var kv in _joints)
                if (kv.Value != null) _blendFrom[kv.Key] = kv.Value.localRotation;
            _blendTimer = 0f;
        }

        private static Dictionary<string, Quaternion> LerpPose(
            Dictionary<string, Quaternion> a, Dictionary<string, Quaternion> b, float f)
        {
            var result = new Dictionary<string, Quaternion>();
            foreach (string joint in JointNames)
            {
                Quaternion qa = a != null && a.TryGetValue(joint, out Quaternion ra) ? ra : Quaternion.identity;
                Quaternion qb = b != null && b.TryGetValue(joint, out Quaternion rb) ? rb : Quaternion.identity;
                result[joint] = Quaternion.Slerp(qa, qb, f);
            }
            return result;
        }

        private static Dictionary<string, Quaternion> IdentityPose()
        {
            var pose = new Dictionary<string, Quaternion>();
            foreach (string joint in JointNames) pose[joint] = Quaternion.identity;
            return pose;
        }

        /// <summary>
        /// Procedural Run pose: legs pedal on a sine (opposite phase), arms
        /// swing opposite to the legs, torso leans forward, small bounce.
        /// </summary>
        private Dictionary<string, Quaternion> BuildRunPose()
        {
            float sL = Mathf.Sin(_runPhase);
            float sR = Mathf.Sin(_runPhase + Mathf.PI);
            float amp = 0.6f + 0.4f * _runIntensity;
            var pose = IdentityPose();
            pose["HipL"] = Quaternion.Euler(-32f * sL * amp, 0f, 0f);
            pose["HipR"] = Quaternion.Euler(-32f * sR * amp, 0f, 0f);
            pose["KneeL"] = Quaternion.Euler(55f * Mathf.Max(0f, sR) * amp, 0f, 0f);
            pose["KneeR"] = Quaternion.Euler(55f * Mathf.Max(0f, sL) * amp, 0f, 0f);
            pose["AnkleL"] = Quaternion.Euler(15f * sL, 0f, 0f);
            pose["AnkleR"] = Quaternion.Euler(15f * sR, 0f, 0f);
            pose["ShoulderL"] = Quaternion.Euler(-25f * sR * amp - 15f, 0f, 12f);
            pose["ShoulderR"] = Quaternion.Euler(-25f * sL * amp - 15f, 0f, -12f);
            pose["ElbowL"] = Quaternion.Euler(-70f, 0f, 0f);
            pose["ElbowR"] = Quaternion.Euler(-70f, 0f, 0f);
            pose["WristR"] = Quaternion.Euler(-20f, 0f, 0f);
            pose["Chest"] = Quaternion.Euler(12f, 0f, 0f);
            pose["Spine"] = Quaternion.Euler(5f, 0f, 0f);
            pose["Head"] = Quaternion.Euler(-6f, 0f, 0f);
            return pose;
        }
    }

    /// <summary>
    /// Builds every procedural clip. Each shot clip is a distinct set of
    /// keyframes (windup -&gt; contact -&gt; follow-through -&gt; recover) — never
    /// the same clip played at a different speed.
    /// </summary>
    public static class AnimClipLibrary
    {
        /// <summary>
        /// Build a full pose from per-joint Euler overrides (degrees).
        /// Joints not listed stay at identity.
        /// </summary>
        private static Dictionary<string, Quaternion> P(params (string joint, Vector3 euler)[] entries)
        {
            var pose = new Dictionary<string, Quaternion>();
            foreach (string joint in PlayerAnimationController.JointNames)
                pose[joint] = Quaternion.identity;
            foreach (var (joint, euler) in entries)
                pose[joint] = Quaternion.Euler(euler);
            return pose;
        }

        private static Vector3 E(float x, float y, float z) => new Vector3(x, y, z);

        public static Dictionary<string, AnimClip> BuildAll()
        {
            var clips = new Dictionary<string, AnimClip>
            {
                { "Idle", BuildIdle() },
                { "Ready", BuildReady() },
                { "Run", BuildRun() },
                { "LungeForward", BuildLungeForward() },
                { "LungeLeft", BuildLungeLeft() },
                { "LungeRight", BuildLungeRight() },
                { "Jump", BuildJump() },
                { "Smash", BuildSmash() },
                { "Clear", BuildClear() },
                { "Drop", BuildDrop() },
                { "NetShot", BuildNetShot() },
                { "Serve", BuildServe() },
                { "Block", BuildBlock() },
                { "Celebrate", BuildCelebrate() },
                { "Disappointed", BuildDisappointed() }
            };
            return clips;
        }

        // --- Loops ------------------------------------------------------------

        private static AnimClip BuildIdle()
        {
            var breathe = P(
                ("Chest", E(2f, 0f, 0f)),
                ("ShoulderL", E(0f, 0f, 8f)), ("ShoulderR", E(0f, 0f, -8f)),
                ("ElbowL", E(-15f, 0f, 0f)), ("ElbowR", E(-15f, 0f, 0f)));
            var breatheDeep = P(
                ("Chest", E(4f, 0f, 0f)), ("Spine", E(1f, 0f, 0f)),
                ("Head", E(-2f, 0f, 0f)),
                ("ShoulderL", E(0f, 0f, 9f)), ("ShoulderR", E(0f, 0f, -9f)),
                ("ElbowL", E(-16f, 0f, 0f)), ("ElbowR", E(-16f, 0f, 0f)));
            return new AnimClip("Idle", new[]
            {
                new AnimKeyframe(0f, breathe),
                new AnimKeyframe(1f, breatheDeep),
                new AnimKeyframe(2f, breathe)
            }, loop: true);
        }

        private static AnimClip BuildReady()
        {
            var ready = P(
                ("Chest", E(12f, 0f, 0f)),
                ("HipL", E(-25f, 0f, 0f)), ("HipR", E(-25f, 0f, 0f)),
                ("KneeL", E(35f, 0f, 0f)), ("KneeR", E(35f, 0f, 0f)),
                ("AnkleL", E(-10f, 0f, 0f)), ("AnkleR", E(-10f, 0f, 0f)),
                ("ShoulderR", E(-45f, 0f, -25f)), ("ElbowR", E(-55f, 0f, 0f)),
                ("ShoulderL", E(-20f, 0f, 20f)), ("ElbowL", E(-40f, 0f, 0f)));
            var readyBounce = P(
                ("Chest", E(13f, 0f, 0f)),
                ("HipL", E(-27f, 0f, 0f)), ("HipR", E(-27f, 0f, 0f)),
                ("KneeL", E(38f, 0f, 0f)), ("KneeR", E(38f, 0f, 0f)),
                ("AnkleL", E(-11f, 0f, 0f)), ("AnkleR", E(-11f, 0f, 0f)),
                ("ShoulderR", E(-47f, 0f, -25f)), ("ElbowR", E(-56f, 0f, 0f)),
                ("ShoulderL", E(-22f, 0f, 20f)), ("ElbowL", E(-42f, 0f, 0f)));
            return new AnimClip("Ready", new[]
            {
                new AnimKeyframe(0f, ready, 0f),
                new AnimKeyframe(0.6f, readyBounce, 0.02f),
                new AnimKeyframe(1.2f, ready, 0f)
            }, loop: true);
        }

        /// <summary>Run is a placeholder keyframe clip; the controller replaces it with the procedural sine pose.</summary>
        private static AnimClip BuildRun()
        {
            return new AnimClip("Run", new[]
            {
                new AnimKeyframe(0f, P()),
                new AnimKeyframe(0.5f, P())
            }, loop: true);
        }

        // --- Lunges (footwork, no contact time) -------------------------------

        private static AnimClip BuildLungeForward()
        {
            var start = P(
                ("Chest", E(12f, 0f, 0f)),
                ("HipL", E(-25f, 0f, 0f)), ("HipR", E(-25f, 0f, 0f)),
                ("KneeL", E(35f, 0f, 0f)), ("KneeR", E(35f, 0f, 0f)),
                ("ShoulderR", E(-45f, 0f, -25f)), ("ElbowR", E(-55f, 0f, 0f)));
            var lunge = P(
                ("Chest", E(22f, 0f, 0f)),
                ("HipR", E(-70f, 0f, 0f)), ("KneeR", E(85f, 0f, 0f)),
                ("HipL", E(25f, 0f, 0f)), ("KneeL", E(15f, 0f, 0f)),
                ("AnkleR", E(-15f, 0f, 0f)),
                ("ShoulderR", E(-60f, 0f, -15f)), ("ElbowR", E(-30f, 0f, 0f)),
                ("ShoulderL", E(-10f, 0f, 25f)), ("ElbowL", E(-25f, 0f, 0f)));
            return new AnimClip("LungeForward", new[]
            {
                new AnimKeyframe(0f, start, 0f),
                new AnimKeyframe(0.15f, lunge, -0.16f),
                new AnimKeyframe(0.5f, start, 0f)
            });
        }

        private static AnimClip BuildLungeLeft()
        {
            var start = P(
                ("Chest", E(12f, 0f, 0f)),
                ("HipL", E(-25f, 0f, 0f)), ("HipR", E(-25f, 0f, 0f)),
                ("KneeL", E(35f, 0f, 0f)), ("KneeR", E(35f, 0f, 0f)));
            var lunge = P(
                ("Chest", E(10f, 0f, 12f)),
                ("HipL", E(-20f, 0f, 35f)), ("KneeL", E(45f, 0f, 0f)),
                ("HipR", E(-10f, 0f, -30f)), ("KneeR", E(20f, 0f, 0f)),
                ("ShoulderL", E(-30f, 0f, 30f)), ("ElbowL", E(-40f, 0f, 0f)));
            return new AnimClip("LungeLeft", new[]
            {
                new AnimKeyframe(0f, start, 0f),
                new AnimKeyframe(0.15f, lunge, -0.12f),
                new AnimKeyframe(0.5f, start, 0f)
            });
        }

        private static AnimClip BuildLungeRight()
        {
            var start = P(
                ("Chest", E(12f, 0f, 0f)),
                ("HipL", E(-25f, 0f, 0f)), ("HipR", E(-25f, 0f, 0f)),
                ("KneeL", E(35f, 0f, 0f)), ("KneeR", E(35f, 0f, 0f)));
            var lunge = P(
                ("Chest", E(10f, 0f, -12f)),
                ("HipR", E(-20f, 0f, -35f)), ("KneeR", E(45f, 0f, 0f)),
                ("HipL", E(-10f, 0f, 30f)), ("KneeL", E(20f, 0f, 0f)),
                ("ShoulderR", E(-50f, 0f, -30f)), ("ElbowR", E(-45f, 0f, 0f)));
            return new AnimClip("LungeRight", new[]
            {
                new AnimKeyframe(0f, start, 0f),
                new AnimKeyframe(0.15f, lunge, -0.12f),
                new AnimKeyframe(0.5f, start, 0f)
            });
        }

        private static AnimClip BuildJump()
        {
            var crouch = P(
                ("HipL", E(-40f, 0f, 0f)), ("HipR", E(-40f, 0f, 0f)),
                ("KneeL", E(60f, 0f, 0f)), ("KneeR", E(60f, 0f, 0f)),
                ("Chest", E(15f, 0f, 0f)),
                ("ShoulderL", E(-30f, 0f, 15f)), ("ShoulderR", E(-30f, 0f, -15f)));
            var air = P(
                ("HipL", E(-40f, 0f, 0f)), ("HipR", E(-30f, 0f, 0f)),
                ("KneeL", E(70f, 0f, 0f)), ("KneeR", E(50f, 0f, 0f)),
                ("Chest", E(8f, 0f, 0f)),
                ("ShoulderL", E(-150f, 0f, 20f)), ("ShoulderR", E(-150f, 0f, -20f)),
                ("ElbowL", E(-20f, 0f, 0f)), ("ElbowR", E(-20f, 0f, 0f)));
            return new AnimClip("Jump", new[]
            {
                new AnimKeyframe(0f, crouch, -0.08f),
                new AnimKeyframe(0.25f, air, 0.45f),
                new AnimKeyframe(0.6f, crouch, 0f)
            });
        }

        // --- Shot clips (windup -> contact -> follow-through -> recover) -----

        private static AnimClip BuildSmash()
        {
            var ready = P(
                ("Chest", E(12f, 0f, 0f)),
                ("HipL", E(-25f, 0f, 0f)), ("HipR", E(-25f, 0f, 0f)),
                ("KneeL", E(35f, 0f, 0f)), ("KneeR", E(35f, 0f, 0f)),
                ("ShoulderR", E(-45f, 0f, -25f)), ("ElbowR", E(-55f, 0f, 0f)));
            var windup = P(
                ("Chest", E(8f, -35f, 0f)), ("Spine", E(0f, -15f, 0f)),
                ("ShoulderR", E(-20f, 0f, -70f)), ("ElbowR", E(-100f, 0f, 0f)),
                ("HipR", E(-15f, 0f, 0f)), ("KneeR", E(25f, 0f, 0f)),
                ("ShoulderL", E(-20f, 0f, 30f)));
            var contact = P(
                ("Chest", E(15f, 25f, 0f)), ("Spine", E(5f, 10f, 0f)),
                ("ShoulderR", E(-170f, 0f, -10f)), ("ElbowR", E(-10f, 0f, 0f)),
                ("WristR", E(-25f, 0f, 0f)),
                ("HipL", E(-20f, 0f, 0f)), ("KneeL", E(30f, 0f, 0f)),
                ("HipR", E(-35f, 0f, 0f)), ("KneeR", E(45f, 0f, 0f)));
            var follow = P(
                ("Chest", E(20f, 30f, 0f)),
                ("ShoulderR", E(-40f, 0f, 35f)), ("ElbowR", E(-30f, 0f, 0f)),
                ("HipL", E(-30f, 0f, 0f)), ("KneeL", E(40f, 0f, 0f)));
            return new AnimClip("Smash", new[]
            {
                new AnimKeyframe(0f, ready, 0f),
                new AnimKeyframe(0.18f, windup, 0.05f),
                new AnimKeyframe(0.30f, contact, 0.22f),
                new AnimKeyframe(0.50f, follow, 0.05f),
                new AnimKeyframe(0.70f, ready, 0f)
            }, contactTime: 0.30f);
        }

        private static AnimClip BuildClear()
        {
            var ready = P(
                ("Chest", E(12f, 0f, 0f)),
                ("HipL", E(-25f, 0f, 0f)), ("HipR", E(-25f, 0f, 0f)),
                ("KneeL", E(35f, 0f, 0f)), ("KneeR", E(35f, 0f, 0f)),
                ("ShoulderR", E(-45f, 0f, -25f)), ("ElbowR", E(-55f, 0f, 0f)));
            var windup = P(
                ("Chest", E(6f, -25f, 0f)),
                ("ShoulderR", E(-30f, 0f, -55f)), ("ElbowR", E(-90f, 0f, 0f)),
                ("HipR", E(-20f, 0f, 0f)), ("KneeR", E(30f, 0f, 0f)));
            var contact = P(
                ("Chest", E(10f, 15f, 0f)),
                ("ShoulderR", E(-165f, 0f, -5f)), ("ElbowR", E(-15f, 0f, 0f)),
                ("WristR", E(-15f, 0f, 0f)),
                ("HipL", E(-25f, 0f, 0f)), ("KneeL", E(35f, 0f, 0f)));
            var follow = P(
                ("Chest", E(14f, 20f, 0f)),
                ("ShoulderR", E(-90f, 0f, 0f)), ("ElbowR", E(-20f, 0f, 0f)));
            return new AnimClip("Clear", new[]
            {
                new AnimKeyframe(0f, ready, 0f),
                new AnimKeyframe(0.20f, windup, 0.04f),
                new AnimKeyframe(0.32f, contact, 0.15f),
                new AnimKeyframe(0.55f, follow, 0.02f),
                new AnimKeyframe(0.75f, ready, 0f)
            }, contactTime: 0.32f);
        }

        private static AnimClip BuildDrop()
        {
            var ready = P(
                ("Chest", E(12f, 0f, 0f)),
                ("HipL", E(-25f, 0f, 0f)), ("HipR", E(-25f, 0f, 0f)),
                ("KneeL", E(35f, 0f, 0f)), ("KneeR", E(35f, 0f, 0f)),
                ("ShoulderR", E(-45f, 0f, -25f)), ("ElbowR", E(-55f, 0f, 0f)));
            var windup = P(
                ("Chest", E(8f, -20f, 0f)),
                ("ShoulderR", E(-35f, 0f, -45f)), ("ElbowR", E(-85f, 0f, 0f)));
            var contact = P(
                ("Chest", E(12f, 10f, 0f)),
                ("ShoulderR", E(-150f, 0f, -10f)), ("ElbowR", E(-35f, 0f, 0f)),
                ("WristR", E(-30f, 0f, 0f)),
                ("HipL", E(-25f, 0f, 0f)), ("KneeL", E(35f, 0f, 0f)));
            var follow = P(
                ("Chest", E(14f, 12f, 0f)),
                ("ShoulderR", E(-100f, 0f, -5f)), ("ElbowR", E(-40f, 0f, 0f)));
            return new AnimClip("Drop", new[]
            {
                new AnimKeyframe(0f, ready, 0f),
                new AnimKeyframe(0.16f, windup, 0.03f),
                new AnimKeyframe(0.26f, contact, 0.12f),
                new AnimKeyframe(0.45f, follow, 0f),
                new AnimKeyframe(0.65f, ready, 0f)
            }, contactTime: 0.26f);
        }

        private static AnimClip BuildNetShot()
        {
            var ready = P(
                ("Chest", E(12f, 0f, 0f)),
                ("HipL", E(-25f, 0f, 0f)), ("HipR", E(-25f, 0f, 0f)),
                ("KneeL", E(35f, 0f, 0f)), ("KneeR", E(35f, 0f, 0f)),
                ("ShoulderR", E(-45f, 0f, -25f)), ("ElbowR", E(-55f, 0f, 0f)));
            var low = P(
                ("Chest", E(20f, 0f, 0f)),
                ("HipL", E(-55f, 0f, 0f)), ("HipR", E(-55f, 0f, 0f)),
                ("KneeL", E(70f, 0f, 0f)), ("KneeR", E(70f, 0f, 0f)),
                ("AnkleL", E(-20f, 0f, 0f)), ("AnkleR", E(-20f, 0f, 0f)),
                ("ShoulderR", E(-70f, 0f, -10f)), ("ElbowR", E(-25f, 0f, 0f)));
            var contact = P(
                ("Chest", E(18f, 5f, 0f)),
                ("HipL", E(-60f, 0f, 0f)), ("HipR", E(-50f, 0f, 0f)),
                ("KneeL", E(75f, 0f, 0f)), ("KneeR", E(65f, 0f, 0f)),
                ("ShoulderR", E(-85f, 0f, -5f)), ("ElbowR", E(-15f, 0f, 0f)),
                ("WristR", E(-20f, 0f, 0f)));
            return new AnimClip("NetShot", new[]
            {
                new AnimKeyframe(0f, ready, 0f),
                new AnimKeyframe(0.10f, low, -0.14f),
                new AnimKeyframe(0.18f, contact, -0.12f),
                new AnimKeyframe(0.35f, low, -0.14f),
                new AnimKeyframe(0.50f, ready, 0f)
            }, contactTime: 0.18f);
        }

        private static AnimClip BuildServe()
        {
            var ready = P(
                ("Chest", E(8f, 0f, 0f)),
                ("HipL", E(-15f, 0f, 0f)), ("HipR", E(-15f, 0f, 0f)),
                ("KneeL", E(20f, 0f, 0f)), ("KneeR", E(20f, 0f, 0f)),
                ("ShoulderR", E(0f, 0f, -10f)), ("ElbowR", E(-20f, 0f, 0f)),
                ("ShoulderL", E(-10f, 0f, 10f)));
            var backswing = P(
                ("Chest", E(6f, -10f, 0f)),
                ("ShoulderR", E(25f, 0f, -10f)), ("ElbowR", E(-20f, 0f, 0f)),
                ("HipL", E(-20f, 0f, 0f)), ("KneeL", E(25f, 0f, 0f)));
            var contact = P(
                ("Chest", E(8f, 5f, 0f)),
                ("ShoulderR", E(-55f, 0f, -5f)), ("ElbowR", E(-15f, 0f, 0f)),
                ("HipL", E(-20f, 0f, 0f)), ("KneeL", E(25f, 0f, 0f)));
            var follow = P(
                ("Chest", E(10f, 8f, 0f)),
                ("ShoulderR", E(-75f, 0f, 0f)), ("ElbowR", E(-20f, 0f, 0f)));
            return new AnimClip("Serve", new[]
            {
                new AnimKeyframe(0f, ready, 0f),
                new AnimKeyframe(0.22f, backswing, 0f),
                new AnimKeyframe(0.38f, contact, 0f),
                new AnimKeyframe(0.55f, follow, 0f),
                new AnimKeyframe(0.80f, ready, 0f)
            }, contactTime: 0.38f);
        }

        private static AnimClip BuildBlock()
        {
            var ready = P(
                ("Chest", E(12f, 0f, 0f)),
                ("HipL", E(-25f, 0f, 0f)), ("HipR", E(-25f, 0f, 0f)),
                ("KneeL", E(35f, 0f, 0f)), ("KneeR", E(35f, 0f, 0f)),
                ("ShoulderR", E(-45f, 0f, -25f)), ("ElbowR", E(-55f, 0f, 0f)));
            var block = P(
                ("Chest", E(14f, 0f, 0f)),
                ("HipL", E(-30f, 0f, 0f)), ("HipR", E(-30f, 0f, 0f)),
                ("KneeL", E(40f, 0f, 0f)), ("KneeR", E(40f, 0f, 0f)),
                ("ShoulderR", E(-80f, 0f, -20f)), ("ElbowR", E(-30f, 0f, 0f)));
            return new AnimClip("Block", new[]
            {
                new AnimKeyframe(0f, ready, 0f),
                new AnimKeyframe(0.15f, block, 0f),
                new AnimKeyframe(0.40f, ready, 0f)
            }, contactTime: 0.15f);
        }

        // --- Reactions ----------------------------------------------------------

        private static AnimClip BuildCelebrate()
        {
            var down = P(
                ("ShoulderL", E(-20f, 0f, 15f)), ("ShoulderR", E(-20f, 0f, -15f)),
                ("ElbowL", E(-30f, 0f, 0f)), ("ElbowR", E(-30f, 0f, 0f)));
            var up = P(
                ("Chest", E(-6f, 0f, 0f)), ("Head", E(-12f, 0f, 0f)),
                ("ShoulderL", E(-160f, 0f, 25f)), ("ShoulderR", E(-160f, 0f, -25f)),
                ("ElbowL", E(-15f, 0f, 0f)), ("ElbowR", E(-15f, 0f, 0f)),
                ("HipL", E(-15f, 0f, 0f)), ("HipR", E(-15f, 0f, 0f)),
                ("KneeL", E(20f, 0f, 0f)), ("KneeR", E(20f, 0f, 0f)));
            return new AnimClip("Celebrate", new[]
            {
                new AnimKeyframe(0f, down, 0f),
                new AnimKeyframe(0.35f, up, 0.12f),
                new AnimKeyframe(0.70f, up, 0f),
                new AnimKeyframe(1.00f, down, 0f),
                new AnimKeyframe(1.40f, down, 0f)
            });
        }

        private static AnimClip BuildDisappointed()
        {
            var slump = P(
                ("Chest", E(15f, 0f, 0f)), ("Spine", E(8f, 0f, 0f)),
                ("Head", E(25f, 0f, 0f)),
                ("ShoulderL", E(10f, 0f, 5f)), ("ShoulderR", E(10f, 0f, -5f)),
                ("ElbowL", E(-10f, 0f, 0f)), ("ElbowR", E(-10f, 0f, 0f)),
                ("HipL", E(-10f, 0f, 0f)), ("HipR", E(-10f, 0f, 0f)),
                ("KneeL", E(15f, 0f, 0f)), ("KneeR", E(15f, 0f, 0f)));
            var deeper = P(
                ("Chest", E(20f, 0f, 0f)), ("Spine", E(10f, 0f, 0f)),
                ("Head", E(30f, 0f, 0f)),
                ("ShoulderL", E(12f, 0f, 5f)), ("ShoulderR", E(12f, 0f, -5f)),
                ("HipL", E(-12f, 0f, 0f)), ("HipR", E(-12f, 0f, 0f)),
                ("KneeL", E(18f, 0f, 0f)), ("KneeR", E(18f, 0f, 0f)));
            return new AnimClip("Disappointed", new[]
            {
                new AnimKeyframe(0f, slump, -0.03f),
                new AnimKeyframe(0.70f, deeper, -0.05f),
                new AnimKeyframe(1.40f, slump, -0.03f)
            });
        }
    }
}
