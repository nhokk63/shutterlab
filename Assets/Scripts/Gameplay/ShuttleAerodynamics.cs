using UnityEngine;

namespace ShuttleLab.Gameplay
{
    /// <summary>
    /// Shuttlecock aerodynamics. Feather shuttle reference values:
    /// mass 4.74-5.50 g, 16 feathers. Drag: F = -0.5 * rho * Cd * A * |v| * v.
    /// Cd is a tunable effective coefficient, not a universal constant — see the
    /// Physics Calibration Scene / ShotSolver convergence tests.
    /// </summary>
    public static class ShuttleAerodynamics
    {
        public const float Mass = 0.005f;      // kg
        public const float Rho = 1.225f;       // kg/m^3, sea level
        public const float Cd = 0.55f;         // effective drag coefficient
        public const float Area = 0.0038f;     // m^2 effective frontal area
        public const float Gravity = 9.81f;    // m/s^2

        public static Vector3 DragAcceleration(Vector3 velocity, Vector3 wind)
        {
            Vector3 rel = velocity - wind;
            float speed = rel.magnitude;
            if (speed < 1e-4f) return Vector3.zero;
            float k = 0.5f * Rho * Cd * Area / Mass;
            return -k * speed * rel;
        }

        public static Vector3 Acceleration(Vector3 velocity, Vector3 wind)
        {
            return DragAcceleration(velocity, wind) + new Vector3(0f, -Gravity, 0f);
        }

        /// <summary>One RK2 step. Modifies pos/vel in place.</summary>
        public static void StepRK2(ref Vector3 pos, ref Vector3 vel, float dt, Vector3 wind)
        {
            Vector3 a1 = Acceleration(vel, wind);
            Vector3 vMid = vel + a1 * (dt * 0.5f);
            Vector3 a2 = Acceleration(vMid, wind);
            vel = vel + a2 * dt;
            pos = pos + vMid * dt;
        }

        /// <summary>
        /// Simulate a flight until the shuttle reaches y &lt;= 0 (court plane) or
        /// maxTime elapses. Returns true when a landing was found; landing is the
        /// interpolated court-plane crossing. apex is the max height reached.
        /// </summary>
        public static bool Simulate(Vector3 start, Vector3 v0, Vector3 wind,
            float maxTime, float dt, out Vector3 landing, out float flightTime, out float apex)
        {
            Vector3 pos = start;
            Vector3 vel = v0;
            apex = start.y;
            float t = 0f;
            landing = start;
            flightTime = 0f;

            if (start.y <= 0f && v0.y <= 0f)
            {
                landing = start;
                landing.y = 0f;
                return true;
            }

            while (t < maxTime)
            {
                Vector3 prevPos = pos;
                float prevT = t;
                StepRK2(ref pos, ref vel, dt, wind);
                t += dt;
                if (pos.y > apex) apex = pos.y;

                if (pos.y <= 0f)
                {
                    // Interpolate the exact crossing between prevPos and pos.
                    float f = prevPos.y / (prevPos.y - pos.y);
                    f = Mathf.Clamp01(f);
                    landing = Vector3.Lerp(prevPos, pos, f);
                    landing.y = 0f;
                    flightTime = prevT + dt * f;
                    return true;
                }
            }

            landing = pos;
            flightTime = maxTime;
            return false;
        }

        /// <summary>Predict where the shuttle will land from its current state.</summary>
        public static bool PredictLanding(Vector3 pos, Vector3 vel, Vector3 wind,
            out Vector3 landing, out float flightTime)
        {
            return Simulate(pos, vel, wind, 5f, 1f / 240f, out landing, out flightTime, out _);
        }
    }
}
