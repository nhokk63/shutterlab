// RacketController.cs — ShuttleLab (Unity 6, URP)
// Owns the racket: its contact point (RacketHead from HumanoidRig) and the
// swing. A swing plays the matching procedural clip on the animation
// controller; the actual shot is executed by ShotController from the clip's
// onContact callback (wired by PlayerController), so the racket never
// launches the shuttle itself.

using System;
using UnityEngine;
using ShuttleLab.Players;

namespace ShuttleLab.Gameplay
{
    public class RacketController : MonoBehaviour
    {
        /// <summary>Centre of the string bed (contact point), from HumanoidRig.</summary>
        public Transform RacketHead { get; private set; }

        /// <summary>World position of the string-bed centre.</summary>
        public Vector3 ContactPoint => RacketHead != null
            ? RacketHead.position
            : transform.position + Vector3.up * 1.2f;

        private PlayerAnimationController _anim;
        private HumanoidRig _rig;

        /// <summary>Wire the rig (racket) and the animation controller. Called by PlayerController.Init.</summary>
        public void Bind(HumanoidRig rig, PlayerAnimationController anim)
        {
            _rig = rig;
            _anim = anim;
            RacketHead = rig != null ? rig.RacketHead : null;
        }

        /// <summary>Play the swing for a shot type over the given duration (seconds).</summary>
        public void PlaySwing(ShotType type, float duration)
        {
            PlaySwing(type, duration, null, null);
        }

        /// <summary>
        /// Play the swing; onContact fires at the clip's ContactTime,
        /// onEnd when the swing finishes.
        /// </summary>
        public void PlaySwing(ShotType type, float duration, Action onContact, Action onEnd)
        {
            if (_anim == null)
            {
                onContact?.Invoke();
                onEnd?.Invoke();
                return;
            }

            string clipName = ClipNameForShot(type);
            AnimClip clip = _anim.GetClip(clipName);
            if (clip == null)
            {
                onContact?.Invoke();
                onEnd?.Invoke();
                return;
            }

            // Scale playback so the swing lasts `duration` seconds. Each shot
            // type still has its own distinct clip — speed only stretches it.
            float speed = duration > 0.01f ? clip.Duration / duration : 1f;
            _anim.PlayClip(clip, speed, onContact, onEnd);
        }

        /// <summary>
        /// Is the shuttle inside the contact window? Within 0.45 m of the
        /// string bed AND in front of the racket face (positive dot with the
        /// racket-head forward, i.e. the string-bed normal).
        /// </summary>
        public bool IsInContactWindow(Vector3 shuttlePos)
        {
            if (RacketHead == null) return false;
            Vector3 toShuttle = shuttlePos - RacketHead.position;
            if (toShuttle.magnitude > 0.45f) return false;
            if (toShuttle.sqrMagnitude < 1e-6f) return true;
            return Vector3.Dot(toShuttle.normalized, RacketHead.forward) > 0f;
        }

        /// <summary>Map a shot type to its animation clip (nearest neighbour).</summary>
        public static string ClipNameForShot(ShotType type)
        {
            switch (type)
            {
                case ShotType.Smash:
                case ShotType.JumpSmash:
                case ShotType.HalfSmash:
                    return "Smash";
                case ShotType.Clear:
                case ShotType.Lift:
                case ShotType.Drive:
                    return "Clear";
                case ShotType.Drop:
                    return "Drop";
                case ShotType.NetShot:
                case ShotType.NetKill:
                case ShotType.Push:
                    return "NetShot";
                case ShotType.Block:
                case ShotType.DefensiveDrive:
                    return "Block";
                case ShotType.ServeLow:
                case ShotType.ServeHigh:
                case ShotType.ServeFlick:
                    return "Serve";
                default:
                    return "Clear";
            }
        }
    }
}
