using UnityEngine;

namespace ShuttleLab.Gameplay
{
    /// <summary>
    /// Solves the launch velocity for a shot: find v0 so that the drag-affected
    /// simulated trajectory lands on the target. Shooting method: ballistic
    /// initial guess, then simulate with ShuttleAerodynamics and correct v0.
    /// The flight itself is always simulated live by ShuttlecockController —
    /// the solver only picks the initial velocity, never the path.
    /// </summary>
    public static class ShotSolver
    {
        private const int MaxIterations = 12;
        private const float ConvergeDistance = 0.15f;
        private const float SimDt = 1f / 240f;

        /// <summary>
        /// Solve for a launch velocity from start to target for the given shot
        /// type and quality. Returns false when the shot is physically
        /// unreachable (e.g. a drop from the back court) — the caller must
        /// fall back to another shot (usually Lift/Clear).
        /// </summary>
        public static bool Solve(Vector3 start, Vector3 target, ShotType type,
            ShotQuality quality, Vector3 wind, out Vector3 launchVelocity)
        {
            launchVelocity = Vector3.zero;
            var profile = ShotLibrary.Get(type);

            // Apply quality: dispersion + power loss. Poor quality may push the
            // effective target out of the court — that is the risk, by design.
            Vector3 aim = target;
            float dispersion = ShotTiming.Dispersion(quality);
            if (dispersion > 0.01f)
            {
                // Deterministic dispersion from a hash of the inputs (no RNG in
                // the solver path, so replays/tests are reproducible).
                float h = Mathf.Sin(start.x * 12.9898f + start.z * 78.233f + target.x * 37.719f) * 43758.5453f;
                float frac = h - Mathf.Floor(h);
                float angle = frac * Mathf.PI * 2f;
                aim += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dispersion;
            }

            float flightTime = profile.FlightTime;
            Vector3 toTarget = aim - start;

            // Ballistic initial guess (no drag).
            Vector3 v0 = new Vector3(
                toTarget.x / flightTime,
                (toTarget.y + 0.5f * ShuttleAerodynamics.Gravity * flightTime * flightTime) / flightTime,
                toTarget.z / flightTime);

            // Enforce a minimum arc for high shots: raise the apex by lifting vy.
            float wantApex = Mathf.Max(profile.ApexHeight, start.y + 0.5f);
            float apexVy = Mathf.Sqrt(2f * ShuttleAerodynamics.Gravity * Mathf.Max(0.1f, wantApex - start.y));
            if (type == ShotType.Clear || type == ShotType.Lift || type == ShotType.ServeHigh)
                v0.y = Mathf.Max(v0.y, apexVy * 0.85f);

            // Shooting iterations with drag. Power factor is applied INSIDE the
            // loop so convergence is checked on the velocity actually returned
            // (the old code converged pre-power, then scaled down and landed short).
            for (int i = 0; i < MaxIterations; i++)
            {
                Vector3 vEff = v0 * ShotTiming.PowerFactor(quality);
                bool landed = ShuttleAerodynamics.Simulate(start, vEff, wind, 5f, SimDt,
                    out Vector3 simLanding, out float simTime, out _);
                if (!landed) return false;

                Vector3 error = aim - simLanding;
                error.y = 0f;
                float errDist = error.magnitude;
                if (errDist < ConvergeDistance) break;

                // Drag-aware correction: under quadratic drag, range is
                // logarithmic in launch speed (range ~ ln(1+k*vh*t)/k), so the
                // linear correction must be scaled by (1+k*vh*t) to converge
                // on long shots (a Clear needs ~45 m/s from a 12.7 m/s guess).
                float k = 0.5f * ShuttleAerodynamics.Rho * ShuttleAerodynamics.Cd
                    * ShuttleAerodynamics.Area / ShuttleAerodynamics.Mass;
                float vh = new Vector2(v0.x, v0.z).magnitude;
                float dragScale = 1f + k * vh * Mathf.Max(0.2f, simTime);
                v0.x += error.x / Mathf.Max(0.2f, simTime) * dragScale;
                v0.z += error.z / Mathf.Max(0.2f, simTime) * dragScale;
                float timeErr = flightTime - simTime;
                v0.y += timeErr * 2.0f;

                if (v0.magnitude > 75f) return false; // unreachable at sane speeds
                if (i == MaxIterations - 1) return false; // never accept an unverified velocity
            }

            // Final verification: re-simulate the velocity we are about to return.
            Vector3 vFinal = v0 * ShotTiming.PowerFactor(quality);
            bool okFinal = ShuttleAerodynamics.Simulate(start, vFinal, wind, 5f, SimDt,
                out Vector3 finalLanding, out _, out _);
            if (!okFinal) return false;
            Vector3 finalError = aim - finalLanding; finalError.y = 0f;
            if (finalError.magnitude > ConvergeDistance) return false;
            v0 = vFinal;

            // Sanity: must go roughly towards the target and over the net.
            Vector3 dir = (aim - start);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                Vector3 hv = v0; hv.y = 0f;
                if (Vector3.Dot(hv.normalized, dir.normalized) < 0.5f) return false;
            }

            launchVelocity = v0;
            return true;
        }

        /// <summary>Solve a serve into the diagonal service box.</summary>
        public static bool SolveServe(Vector3 start, Vector3 boxCentre, ShotType serveType,
            ShotQuality quality, Vector3 wind, out Vector3 launchVelocity)
        {
            return Solve(start, boxCentre, serveType, quality, wind, out launchVelocity);
        }

        /// <summary>
        /// Pick a fallback shot when the requested one is unreachable.
        /// High shuttle -&gt; Clear; low shuttle -&gt; Lift; mid -&gt; Drive.
        /// </summary>
        public static ShotType FallbackShot(float shuttleHeight)
        {
            if (shuttleHeight > 1.9f) return ShotType.Clear;
            if (shuttleHeight < 1.0f) return ShotType.Lift;
            return ShotType.Drive;
        }
    }
}
