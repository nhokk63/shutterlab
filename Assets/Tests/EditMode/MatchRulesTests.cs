// MatchRulesTests.cs — ShuttleLab (EditMode)
// Kiểm tra luật BWF thuần trong MatchRules (không phụ thuộc scene).
// HợP ĐỒNG đã chốt 2026-11-09: cầu chạm sân là rally kết thúc — giao cầu
// tốt rơi trong ô = điểm cho server (KHÔNG còn mã -2 "rally tiếp tục").

using NUnit.Framework;
using UnityEngine;
using ShuttleLab.Gameplay;
using ShuttleLab.Match;
using ShuttleLab.Core;

namespace ShuttleLab.Tests
{
    public class MatchRulesTests
    {
        private MatchRules NewRules()
        {
            var rules = new MatchRules();
            rules.Reset();
            return rules;
        }

        /// <summary>Đánh n điểm liên tiếp cho người thắng, trả về (gameWon, matchWon) của điểm cuối.</summary>
        private static void AwardPoints(MatchRules rules, int winner, int count,
            out bool lastGameWon, out bool lastMatchWon)
        {
            lastGameWon = false;
            lastMatchWon = false;
            for (int i = 0; i < count; i++)
                rules.AwardPoint(winner, out lastGameWon, out lastMatchWon);
        }

        [Test]
        public void WinGame_At21Points()
        {
            var rules = NewRules();
            AwardPoints(rules, 0, 20, out bool gameWon, out bool matchWon);
            Assert.IsFalse(gameWon);
            Assert.AreEqual(20, rules.Score.Points[0]);

            rules.AwardPoint(0, out gameWon, out matchWon);
            Assert.IsTrue(gameWon);
            Assert.IsFalse(matchWon);
            Assert.AreEqual(1, rules.Score.Games[0]);
            Assert.AreEqual(2, rules.Score.CurrentGame);
            // Sang ván mới: điểm reset, người thắng ván trước giao cầu.
            Assert.AreEqual(0, rules.Score.Points[0]);
            Assert.AreEqual(0, rules.Score.Points[1]);
            Assert.AreEqual(0, rules.Score.Server);
        }

        [Test]
        public void WinByTwo_At20All()
        {
            var rules = NewRules();
            rules.Score.Points[0] = 20;
            rules.Score.Points[1] = 20;

            rules.AwardPoint(0, out bool gameWon, out bool matchWon);
            Assert.IsFalse(gameWon, "21-20 chưa hơn 2 điểm nên chưa thắng ván.");

            rules.AwardPoint(0, out gameWon, out matchWon);
            Assert.IsTrue(gameWon, "22-20 hơn 2 điểm nên thắng ván.");
            Assert.IsFalse(matchWon);
        }

        [Test]
        public void Cap30_At29All()
        {
            var rules = NewRules();
            rules.Score.Points[0] = 29;
            rules.Score.Points[1] = 29;

            rules.AwardPoint(0, out bool gameWon, out bool matchWon);
            Assert.IsTrue(gameWon, "30-29: chạm cap 30 thì thắng ván ngay, không cần hơn 2.");
        }

        [Test]
        public void WinMatch_BestOfThree()
        {
            var rules = NewRules();
            AwardPoints(rules, 0, 21, out bool gameWon, out bool matchWon);
            Assert.IsTrue(gameWon);
            Assert.IsFalse(matchWon, "Thắng 1 ván chưa thắng trận.");

            AwardPoints(rules, 0, 21, out gameWon, out matchWon);
            Assert.IsTrue(gameWon);
            Assert.IsTrue(matchWon, "Thắng 2 ván thì thắng trận.");
            Assert.AreEqual(2, rules.Score.Games[0]);
        }

        [Test]
        public void GoodServe_ScoresForServer()
        {
            var rules = NewRules(); // server 0, điểm 0-0 (chẵn -> sân phải).
            Vector3 boxCentre = CourtDimensions.ServiceBoxCentre(1, 1, false);
            int winner = rules.EvaluateLanding(boxCentre, 0, true, 0,
                out MatchRules.FaultReason fault, out bool isLet);
            Assert.AreEqual(0, winner, "Giao cầu tốt rơi trong ô: điểm cho server (giao ăn điểm trực tiếp).");
            Assert.IsFalse(isLet);
            Assert.AreEqual(MatchRules.FaultReason.None, fault);
        }

        [Test]
        public void ServeShort_Fault()
        {
            var rules = NewRules();
            // |z| = 1.0 < vạch ngắn 1.98.
            int winner = rules.EvaluateLanding(new Vector3(1.295f, 0f, 1.0f), 0, true, 0,
                out MatchRules.FaultReason fault, out bool isLet);
            Assert.AreEqual(1, winner);
            Assert.AreEqual(MatchRules.FaultReason.ShortServe, fault);
            Assert.IsFalse(isLet);
        }

        [Test]
        public void ServeOut_Fault()
        {
            var rules = NewRules();
            // |z| = 6.9 > cuối sân đơn 6.70.
            int winner = rules.EvaluateLanding(new Vector3(1.295f, 0f, 6.9f), 0, true, 0,
                out MatchRules.FaultReason fault, out bool isLet);
            Assert.AreEqual(1, winner);
            Assert.AreEqual(MatchRules.FaultReason.ServeOut, fault);
        }

        [Test]
        public void ServeIntoNet_Let()
        {
            var rules = NewRules();
            Vector3 boxCentre = CourtDimensions.ServiceBoxCentre(1, 1, false);
            int result = rules.EvaluateLanding(boxCentre, 0, true, 1,
                out MatchRules.FaultReason fault, out bool isLet);
            Assert.AreEqual(-1, result, "Giao chạm lưới mà vào ô: let, giao lại.");
            Assert.IsTrue(isLet);
        }

        [Test]
        public void ServeNoCross_Fault()
        {
            var rules = NewRules();
            // Rơi lại phía người giao (z < 0): không qua lưới.
            int winner = rules.EvaluateLanding(new Vector3(0f, 0f, -3.0f), 0, true, 0,
                out MatchRules.FaultReason fault, out bool isLet);
            Assert.AreEqual(1, winner);
            Assert.AreEqual(MatchRules.FaultReason.NoCross, fault);
        }

        [Test]
        public void RallyOut_LosesPoint()
        {
            var rules = NewRules();
            // x = 3.0 > biên đơn 2.59: ra ngoài.
            int winner = rules.EvaluateLanding(new Vector3(3.0f, 0f, 5.0f), 0, false, 0,
                out MatchRules.FaultReason fault, out bool isLet);
            Assert.AreEqual(1, winner);
            Assert.AreEqual(MatchRules.FaultReason.Out, fault);
        }

        [Test]
        public void RallyIntoNet_LosesPoint()
        {
            var rules = NewRules();
            int winner = rules.EvaluateLanding(new Vector3(0f, 0f, -3.0f), 0, false, 1,
                out MatchRules.FaultReason fault, out bool isLet);
            Assert.AreEqual(1, winner);
            Assert.AreEqual(MatchRules.FaultReason.IntoNet, fault);
        }

        [Test]
        public void RallyInBounds_Winner()
        {
            var rules = NewRules();
            int winner = rules.EvaluateLanding(new Vector3(1.0f, 0f, 5.0f), 0, false, 0,
                out MatchRules.FaultReason fault, out bool isLet);
            Assert.AreEqual(0, winner, "Đánh trong sân: winner, điểm cho người đánh.");
            Assert.AreEqual(MatchRules.FaultReason.None, fault);
            Assert.IsFalse(isLet);
        }

        [Test]
        public void ServerSwitches_AfterPointWonByReceiver()
        {
            var rules = NewRules();
            Assert.AreEqual(0, rules.Score.Server);
            rules.AwardPoint(1, out _, out _);
            Assert.AreEqual(1, rules.Score.Server, "Rally scoring: người thắng điểm được giao cầu.");
            Assert.AreEqual(0, rules.Score.Receiver);
        }

        [Test]
        public void ServerOnRightCourt_EvenScore()
        {
            var rules = NewRules();
            Assert.IsTrue(rules.ServerOnRightCourt(), "0 điểm (chẵn) -> sân phải.");
            rules.AwardPoint(0, out _, out _);
            Assert.IsFalse(rules.ServerOnRightCourt(), "1 điểm (lẻ) -> sân trái.");
        }
    }
}
