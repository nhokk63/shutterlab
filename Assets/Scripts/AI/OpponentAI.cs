// OpponentAI.cs — ShuttleLab (Unity 6)
// Drives the AI player (index 1) through PlayerController only.
// MonoBehaviour on the AI player's GameObject (added by GameBootstrap).
//
// WHAT THIS DOES:
// - Rally: predicts the landing with AIPerception, waits out its
//   ReactionDelay after the opponent's hit, runs to the predicted spot
//   with PlayerController.MoveTowards, and hits with
//   AIShotSelection + PlayerController.TryHit. After every successful
//   TryHit it calls MatchManager.NotifyShotHit(1, type) so the AI's
//   smashes are counted (PlayerController has no event carrying ShotType).
// - Practice mode (MatchManager.IsPractice): shot selection uses the
//   PracticeVersion of the difficulty (half the error, less aggression)
//   so the AI returns everything it can reach and makes no intentional
//   errors; the rest of the flow is unchanged.
//
// WHAT THIS DELIBERATELY DOES NOT DO:
// - SERVE: MatchManager already serves for the AI (Update: 0.8 s timer,
//   ServeLow 70% / ServeHigh 30% via DoServe, with the correct service
//   box from MatchRules). If OpponentAI also called Serve, the serve
//   would be attempted twice and the stats/phase bookkeeping in DoServe
//   would run on top of a serve it did not launch. So in the Serving
//   phase this component only waits — the serve belongs to MatchManager.
//   (If the serve is ever moved to the AI, delete the AI-serve block in
//   MatchManager.Update and let this component call Serve instead.)
// - Score/rules/phase: never touches Rules, Score, AwardPoint or Phase.
// - The user: never moves player 0, never reads input.
// - Reaching over the net: a hit is only attempted when the shuttle is
//   on the AI's side (z > -0.2 m); a predicted landing on the user's
//   side means the AI walks back home instead of chasing it.
//
// Reaction delay is real: the AI stands still from the moment the
// opponent's hit is observed until ReactionDelay has elapsed. A slow
// Beginner (0.55 s) therefore genuinely cannot reach a fast smash.

using UnityEngine;
using ShuttleLab.Gameplay;
using ShuttleLab.Match;
using ShuttleLab.Players;

namespace ShuttleLab.AI
{
    public class OpponentAI : MonoBehaviour
    {
        private const int AiIndex = 1;

        private PlayerController _ai;
        private MatchManager _match;
        private DifficultyConfig _diff;

        private AIPerception _perception;
        private AIShotSelection _selection;
        private System.Random _rng;

        // Reaction / rally-exchange tracking.
        private int _lastObservedHitter = -1;
        private float _reactReadyAt;
        private bool _incoming;

        private MatchManager.MatchPhase _lastPhase = MatchManager.MatchPhase.Idle;

        public int DifficultyLevel => _diff.Level;
        public bool IsReady => _ai != null && _match != null;

        public void Init(PlayerController aiPlayer, MatchManager match, int difficulty)
        {
            _ai = aiPlayer;
            _match = match;
            _diff = AIDifficulty.ForLevel(difficulty);

            _perception = new AIPerception();
            _perception.Init(match != null ? match.Shuttle : null);
            _perception.PredictionError = _diff.PredictionError;

            _selection = new AIShotSelection();
            _selection.Init(match != null ? match.Shuttle : null);

            // Seeded per difficulty so AI behaviour is reproducible.
            _rng = new System.Random(1000 + _diff.Level * 77);

            _lastObservedHitter = -1;
            _reactReadyAt = 0f;
            _incoming = false;
        }

        private void Update()
        {
            if (_ai == null || _match == null) return;

            MatchManager.MatchPhase phase = _match.Phase;

            // New point: forget the previous exchange.
            if (phase != _lastPhase)
            {
                _lastPhase = phase;
                if (phase == MatchManager.MatchPhase.Serving)
                {
                    _lastObservedHitter = -1;
                    _incoming = false;
                    _reactReadyAt = 0f;
                }
            }

            // Serve: owned by MatchManager (see header). Rally: our job.
            if (phase != MatchManager.MatchPhase.Rally) return;

            ShuttlecockController shuttle = _match.Shuttle;
            if (shuttle == null) return;
            if (shuttle.State != ShuttlecockController.ShuttleState.Flying) return;

            // Our own shot is flying away — nothing to do until the
            // opponent answers it. Record the change so the opponent's
            // next hit starts a FRESH reaction delay (otherwise the
            // reaction timer from the previous exchange would be reused).
            if (shuttle.LastHitter == AiIndex)
            {
                _lastObservedHitter = AiIndex;
                _incoming = false;
                return;
            }

            // Opponent just hit: start the reaction clock, split step.
            if (shuttle.LastHitter == 0 && _lastObservedHitter != 0)
            {
                _lastObservedHitter = 0;
                _incoming = true;
                _reactReadyAt = Time.time + _diff.ReactionDelay;
                _ai.NotifyOpponentHit();
            }

            if (!_incoming) return;
            if (Time.time < _reactReadyAt) return; // still reacting: stand still

            // Predict, then move.
            Vector3 shuttlePos = shuttle.transform.position;
            Vector3 target;
            if (_perception.TryPredict(out Vector3 landing, out float timeToLand))
            {
                _perception.ObserverPosition = _ai.transform.position;
                if (landing.z < 0.3f)
                {
                    // Landing on the opponent's side (fault / net cord):
                    // do not chase across the net, recover home instead.
                    _ai.MoveTowards(_ai.HomePosition, _diff.MoveSpeedFactor);
                    return;
                }
                target = landing;
            }
            else
            {
                // No prediction (rare): chase the shuttle's current spot,
                // clamped to our own half.
                target = shuttlePos;
                target.z = Mathf.Max(target.z, 0.3f);
                target.y = 0f;
            }

            _ai.MoveTowards(target, _diff.MoveSpeedFactor);

            // Hit when the shuttle is within reach, on our side of the net,
            // and we are not already mid-swing.
            if (_ai.IsSwinging) return;
            if (shuttlePos.z <= -0.2f) return; // never reach over the net
            if (!_ai.CanHitShuttle(shuttlePos, out _)) return;

            DifficultyConfig selDiff = _match.IsPractice
                ? _diff.PracticeVersion()
                : _diff;

            (ShotType type, Vector3 shotTarget) = _selection.Choose(
                shuttle.State,
                _ai.transform.position,
                OpponentPosition(),
                _ai.Stats, _ai.Stamina, selDiff, _rng);

            // TryHit true = a swing was executed (including a whiff).
            // The shuttle itself is launched later, from the animation's
            // onContact callback inside PlayerController.
            if (_ai.TryHit(type, shotTarget))
                _match.NotifyShotHit(AiIndex, type);
        }

        private Vector3 OpponentPosition()
        {
            if (_match.Players != null && _match.Players.Length > 0
                && _match.Players[0] != null)
                return _match.Players[0].transform.position;
            return Vector3.zero;
        }
    }
}
