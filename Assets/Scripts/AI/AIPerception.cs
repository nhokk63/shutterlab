// AIPerception.cs — ShuttleLab (Unity 6)
// Predicts where the shuttle will land and whether the AI can get there.
// Plain class, no MonoBehaviour. Reads the live shuttle only — it never
// touches the score, the rules, or any player other than through the
// position the caller supplies (ObserverPosition).
//
// Prediction uses the real physics: ShuttlecockController.PredictLanding
// -> ShuttleAerodynamics.PredictLanding -> Simulate (RK2 with drag).
// Difficulty is modelled as noise on the predicted landing point, so a
// low-level AI runs to the wrong spot and a high-level AI barely errs.
//
// LIMIT ĐÃ BIẾT:
// - The prediction ignores the net and the posts (Simulate only integrates
//   flight to the court plane). A net-cord shot is therefore predicted as
//   if it passed through the net. OpponentAI re-predicts every frame, so
//   the error corrects itself within a few frames of the real net hit.
// - The prediction also ignores the opponent: it is a pure ballistics
//   forecast of the current flight, not a guess about the next shot.
// - CanReach is part of this class's public API for difficulty tuning and
//   PlayMode tests. OpponentAI does NOT gate its chase on it: the chase
//   is best-effort (the prediction carries noise, so a "can't reach"
//   verdict can be wrong), and the hit itself is gated on the live
//   PlayerController.CanHitShuttle check instead. A future improvement is
//   to let a high-level AI deliberately let a clearly-out ball drop
//   (predicted landing outside the singles court by more than
//   PredictionError) instead of chasing it — not implemented in v0.1.0,
//   to keep the AI's rally behaviour obviously correct first.

using UnityEngine;
using ShuttleLab.Gameplay;

namespace ShuttleLab.AI
{
    public class AIPerception
    {
        private ShuttlecockController _shuttle;

        /// <summary>
        /// Prediction noise (m) applied to the landing point. Set from
        /// DifficultyConfig.PredictionError (0 = perfect prediction).
        /// </summary>
        public float PredictionError { get; set; }

        /// <summary>
        /// The AI player's current position. OpponentAI sets this every
        /// frame before calling CanReach — perception itself never finds
        /// players in the scene.
        /// </summary>
        public Vector3 ObserverPosition { get; set; }

        /// <summary>Last prediction, if TryPredict succeeded this frame.</summary>
        public Vector3 LastLanding { get; private set; }

        /// <summary>Time (s) until the last predicted landing.</summary>
        public float LastTimeToLand { get; private set; }

        public bool HasPrediction { get; private set; }

        public void Init(ShuttlecockController shuttle)
        {
            _shuttle = shuttle;
            HasPrediction = false;
        }

        /// <summary>
        /// Predict the landing point and time-to-land of the current flight.
        /// Returns false when there is no flight to predict (shuttle held,
        /// dead, or not the opponent's shot). The returned landing carries
        /// the difficulty noise; the raw physics landing is never exposed.
        /// </summary>
        public bool TryPredict(out Vector3 landing, out float timeToLand)
        {
            landing = Vector3.zero;
            timeToLand = 0f;
            HasPrediction = false;

            if (_shuttle == null) return false;
            if (_shuttle.State != ShuttlecockController.ShuttleState.Flying) return false;
            // Only predict the opponent's shots — our own shot flying away
            // needs no prediction.
            if (_shuttle.LastHitter == 1) return false;

            if (!_shuttle.PredictLanding(out Vector3 rawLanding, out float rawTime))
                return false;

            // Difficulty noise: uniform disc of radius PredictionError.
            if (PredictionError > 0.001f)
            {
                float angle = UnityEngine.Random.value * Mathf.PI * 2f;
                float radius = PredictionError * Mathf.Sqrt(UnityEngine.Random.value);
                rawLanding += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            }

            landing = rawLanding;
            timeToLand = rawTime;
            LastLanding = landing;
            LastTimeToLand = timeToLand;
            HasPrediction = true;
            return true;
        }

        /// <summary>
        /// Can the observer reach the landing point before the shuttle gets
        /// there? moveSpeed is the observer's effective court speed (m/s,
        /// already including difficulty and stamina factors). A small margin
        /// is kept: the AI must arrive a fraction before the shuttle, and
        /// its racket reach counts towards the distance.
        /// </summary>
        public bool CanReach(Vector3 landing, float timeToLand, float moveSpeed)
        {
            if (timeToLand <= 0f || moveSpeed <= 0f) return false;

            Vector3 flat = landing - ObserverPosition;
            flat.y = 0f;
            float distance = flat.magnitude;

            const float arriveEarly = 0.05f;   // must be there slightly early
            const float reachBonus = 1.15f;    // racket reach (PlayerController.ReachRadius)
            float timeAvailable = Mathf.Max(0f, timeToLand - arriveEarly);
            return distance - reachBonus <= moveSpeed * timeAvailable;
        }
    }
}
