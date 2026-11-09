using System;
using UnityEngine;
using ShuttleLab.Core;

namespace ShuttleLab.Gameplay
{
    /// <summary>
    /// The shuttlecock. Simulated with real velocity / acceleration (RK2, 240 Hz
    /// substeps) — never a fixed curve or a tween to a pre-chosen landing spot.
    /// Collisions: court plane (rally ends), net plane (reflect with damping),
    /// net posts (approximated as spheres). Cork leads the velocity vector.
    /// </summary>
    public class ShuttlecockController : MonoBehaviour
    {
        public enum ShuttleState { Held, Flying, Dead }

        public ShuttleState State { get; private set; } = ShuttleState.Held;
        public Vector3 Velocity { get; private set; } = Vector3.zero;
        public Vector3 Wind { get; set; } = Vector3.zero;

        /// <summary>Player index (0 = near/user, 1 = far/AI) who hit the shuttle last. -1 = none.</summary>
        public int LastHitter { get; set; } = -1;

        /// <summary>Was the last launch a serve?</summary>
        public bool LastWasServe { get; set; } = false;

        public int NetHits { get; private set; } = 0;
        public float FlightTime { get; private set; } = 0f;
        public float ApexHeight { get; private set; } = 0f;

        /// <summary>Fired when the shuttle touches the court. Args: landing pos, in singles, in doubles.</summary>
        public event Action<Vector3, bool, bool> OnLanded;

        /// <summary>Fired when the shuttle hits the net.</summary>
        public event Action OnNetHit;

        private const float SubstepHz = 240f;
        private const float PostRadius = 0.05f;
        private const float KillY = -2f;

        private Vector3 _launchPos;
        private Vector3 _prevPos;

        private void Awake()
        {
            _prevPos = transform.position;
        }

        public void HoldAt(Vector3 position)
        {
            State = ShuttleState.Held;
            Velocity = Vector3.zero;
            transform.position = position;
            _prevPos = position;
            FlightTime = 0f;
            ApexHeight = position.y;
        }

        public void Launch(Vector3 initialVelocity, int hitter, bool isServe)
        {
            State = ShuttleState.Flying;
            Velocity = initialVelocity;
            LastHitter = hitter;
            LastWasServe = isServe;
            NetHits = 0;
            FlightTime = 0f;
            ApexHeight = transform.position.y;
            _launchPos = transform.position;
            _prevPos = transform.position;
        }

        public void Kill()
        {
            State = ShuttleState.Dead;
            Velocity = Vector3.zero;
        }

        /// <summary>Where the shuttle will land from its current state (for AI + UI).</summary>
        public bool PredictLanding(out Vector3 landing, out float flightTime)
        {
            if (State != ShuttleState.Flying)
            {
                landing = transform.position;
                flightTime = 0f;
                return false;
            }
            return ShuttleAerodynamics.PredictLanding(transform.position, Velocity, Wind, out landing, out flightTime);
        }

        private void FixedUpdate()
        {
            if (State != ShuttleState.Flying) return;

            float dt = Time.fixedDeltaTime;
            int steps = Mathf.Max(1, Mathf.RoundToInt(dt * SubstepHz));
            float h = dt / steps;

            for (int i = 0; i < steps; i++)
            {
                _prevPos = transform.position;
                Vector3 pos = transform.position;
                Vector3 vel = Velocity;
                ShuttleAerodynamics.StepRK2(ref pos, ref vel, h, Wind);
                transform.position = pos;
                Velocity = vel;
                FlightTime += h;
                if (pos.y > ApexHeight) ApexHeight = pos.y;

                if (CheckNetCollision()) break;
                if (CheckPostCollision()) break;
                if (CheckCourtLanding()) return;
                if (pos.y < KillY || pos.magnitude > 60f)
                {
                    Kill();
                    return;
                }
            }

            // Cork leads the flight direction.
            if (Velocity.sqrMagnitude > 0.25f)
            {
                Quaternion target = Quaternion.LookRotation(Velocity.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, 0.35f);
            }
        }

        private bool CheckCourtLanding()
        {
            Vector3 pos = transform.position;
            if (pos.y > 0f) return false;

            float f = _prevPos.y / Mathf.Max(1e-5f, _prevPos.y - pos.y);
            f = Mathf.Clamp01(f);
            Vector3 landing = Vector3.Lerp(_prevPos, pos, f);
            landing.y = 0f;
            transform.position = landing;
            State = ShuttleState.Dead;
            Velocity = Vector3.zero;
            OnLanded?.Invoke(landing, CourtDimensions.IsInSingles(landing), CourtDimensions.IsInDoubles(landing));
            return true;
        }

        private bool CheckNetCollision()
        {
            Vector3 pos = transform.position;
            // Crossing the net plane z = 0?
            if (Mathf.Sign(_prevPos.z) == Mathf.Sign(pos.z) && pos.z != 0f) return false;
            if (Mathf.Abs(pos.x) > CourtDimensions.DoublesHalfWidth + 0.1f) return false;

            // Interpolate crossing point.
            float denom = _prevPos.z - pos.z;
            if (Mathf.Abs(denom) < 1e-6f) return false;
            float f = Mathf.Clamp01(_prevPos.z / denom);
            Vector3 cross = Vector3.Lerp(_prevPos, pos, f);

            float netH = CourtDimensions.NetHeightAt(cross.x);
            if (cross.y >= netH) return false; // cleared the net

            // Hit the net: reflect z with heavy damping, bleed the rest.
            NetHits++;
            Vector3 v = Velocity;
            v.z = -v.z * 0.22f;
            v.x *= 0.55f;
            v.y *= 0.45f;
            Velocity = v;
            // Push back to the hitter's side so we don't tunnel through.
            Vector3 p = transform.position;
            p.z = _prevPos.z + Mathf.Sign(_prevPos.z) * 0.02f;
            transform.position = p;
            OnNetHit?.Invoke();
            return true;
        }

        private bool CheckPostCollision()
        {
            Vector3 pos = transform.position;
            foreach (float px in new[] { -CourtDimensions.DoublesHalfWidth, CourtDimensions.DoublesHalfWidth })
            {
                Vector3 postTop = new Vector3(px, CourtDimensions.PostHeight, 0f);
                Vector3 postBottom = new Vector3(px, 0f, 0f);
                Vector3 closest = ClosestPointOnSegment(pos, postBottom, postTop);
                if ((pos - closest).sqrMagnitude < PostRadius * PostRadius)
                {
                    Vector3 n = (pos - closest).normalized;
                    if (n.sqrMagnitude < 1e-6f) n = Vector3.forward;
                    Velocity = Vector3.Reflect(Velocity, n) * 0.3f;
                    transform.position = closest + n * (PostRadius + 0.01f);
                    NetHits++;
                    OnNetHit?.Invoke();
                    return true;
                }
            }
            return false;
        }

        private static Vector3 ClosestPointOnSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            return a + ab * t;
        }
    }
}
