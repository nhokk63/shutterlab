using UnityEngine;
using ShuttleLab.Core;
using ShuttleLab.Gameplay;

namespace ShuttleLab.Match
{
    /// <summary>
    /// Pure BWF rules logic — no Unity scene dependencies, fully unit-testable.
    /// Rally scoring, 21 points, win by 2, cap at 30, best of 3 games.
    /// </summary>
    public class MatchRules
    {
        public const int PointsToWinGame = 21;
        public const int MaxPoints = 30;
        public const int GamesToWinMatch = 2;
        public const int WinBy = 2;

        public enum FaultReason
        {
            None,
            Out,            // landed outside the singles court
            IntoNet,        // hit the net and fell back on the hitter's side
            ShortServe,     // serve did not reach the service box
            ServeOut,       // serve landed outside the service box
            NoCross,        // shuttle never crossed the net
            DoubleHit,      // (reserved) hit twice
        }

        public class ScoreState
        {
            public int[] Points = new int[2];   // points in the current game
            public int[] Games = new int[2];    // games won
            public int CurrentGame = 1;
            public int Server = 0;              // player index serving
            public int Receiver = 1;
            public bool[] WonGame = new bool[2];

            public ScoreState Clone()
            {
                var c = new ScoreState();
                c.Points[0] = Points[0]; c.Points[1] = Points[1];
                c.Games[0] = Games[0]; c.Games[1] = Games[1];
                c.CurrentGame = CurrentGame;
                c.Server = Server; c.Receiver = Receiver;
                return c;
            }
        }

        public ScoreState Score { get; private set; } = new ScoreState();

        public void Reset()
        {
            Score = new ScoreState();
        }

        /// <summary>Server's score even -&gt; right court, odd -&gt; left court.</summary>
        public bool ServerOnRightCourt() => Score.Points[Score.Server] % 2 == 0;

        /// <summary>X sign of the service court the server stands on.</summary>
        public int ServerCourtXSign()
        {
            int serverSide = Score.Server == 0 ? -1 : 1;
            return CourtDimensions.ServiceCourtXSign(serverSide, ServerOnRightCourt());
        }

        /// <summary>X sign of the service box the serve must land in.</summary>
        public int TargetBoxXSign()
        {
            // Diagonal box: opposite X sign from the server's court.
            return -ServerCourtXSign();
        }

        /// <summary>Side sign (z) of the receiver.</summary>
        public int ReceiverSideSign() => Score.Receiver == 0 ? -1 : 1;

        /// <summary>
        /// Evaluate a landing. Returns the point winner (0/1), or -1 for a let
        /// (replay the rally). fault explains the decision.
        /// NOTE: a landing ALWAYS ends the rally — ShuttlecockController sets
        /// State = Dead the moment the shuttle touches the ground, so nobody
        /// can play it afterwards. A good serve or a shot landing in-bounds
        /// therefore scores for the last hitter (serve ace / winner by
        /// placement); there is no "rally continues" outcome here.
        /// (History: an earlier draft returned -2 for "rally continues";
        /// MatchManager worked around it by treating -2 as point-to-last-
        /// hitter. That workaround is now folded into this method, and the
        /// -2 branch in MatchManager stays only as a safety net.)
        /// </summary>
        public int EvaluateLanding(Vector3 landing, int lastHitter, bool wasServe,
            int netHits, out FaultReason fault, out bool isLet)
        {
            fault = FaultReason.None;
            isLet = false;
            int opponent = 1 - lastHitter;

            bool inSingles = CourtDimensions.IsInSingles(landing);
            int hitterSide = lastHitter == 0 ? -1 : 1;

            // Did the shuttle cross to the opponent's side?
            bool crossed = Mathf.Sign(landing.z) != hitterSide;

            if (wasServe)
            {
                // Serve must land in the diagonal service box.
                int boxX = TargetBoxXSign();
                int recvSide = ReceiverSideSign();
                bool inBox = CourtDimensions.IsInServiceBox(landing, recvSide, boxX, doubles: false);

                if (netHits > 0 && inBox)
                {
                    // Net-cord serve that lands in: let, replay.
                    isLet = true;
                    return -1;
                }
                if (!crossed)
                {
                    fault = FaultReason.NoCross;
                    return opponent;
                }
                if (!inBox)
                {
                    // Short (did not reach the short service line) vs out.
                    float az = Mathf.Abs(landing.z);
                    fault = az < CourtDimensions.ShortServiceDistance
                        ? FaultReason.ShortServe : FaultReason.ServeOut;
                    return opponent;
                }
                // Serve good: receiver failed to play it before it landed.
                // Serve ace — point to the server.
                return lastHitter;
            }

            // Rally landing.
            if (!crossed)
            {
                // Fell back on the hitter's own side (usually into the net).
                fault = netHits > 0 ? FaultReason.IntoNet : FaultReason.NoCross;
                return opponent;
            }
            if (!inSingles)
            {
                fault = FaultReason.Out;
                return opponent;
            }
            // In-bounds: winner by placement — point to the last hitter.
            return lastHitter;
        }

        /// <summary>Award a point to the winner. Returns true if the match is over.</summary>
        public bool AwardPoint(int winner, out bool gameWon, out bool matchWon)
        {
            gameWon = false;
            matchWon = false;

            Score.Points[winner]++;
            int loser = 1 - winner;

            // Service goes to the point winner (rally scoring).
            Score.Server = winner;
            Score.Receiver = loser;

            int p = Score.Points[winner];
            int q = Score.Points[loser];
            if ((p >= PointsToWinGame && p - q >= WinBy) || p == MaxPoints)
            {
                Score.Games[winner]++;
                Score.WonGame[winner] = Score.Games[winner] >= GamesToWinMatch;
                gameWon = true;
                if (Score.Games[winner] >= GamesToWinMatch)
                {
                    matchWon = true;
                    return true;
                }
                // Next game: reset points, server = winner of the previous game.
                Score.Points[0] = 0;
                Score.Points[1] = 0;
                Score.CurrentGame++;
                Score.Server = winner;
                Score.Receiver = loser;
            }
            return false;
        }

        /// <summary>Score line for the HUD, e.g. "21-15".</summary>
        public string ScoreLine() => $"{Score.Points[0]}-{Score.Points[1]}";

        public string GamesLine() => $"{Score.Games[0]}-{Score.Games[1]}";
    }
}
