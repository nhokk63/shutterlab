// ShotSolverTests.cs — ShuttleLab (EditMode)
// Kiểm tra ShotSolver: bắn trúng đích (mô phỏng lại bằng
// ShuttleAerodynamics.Simulate với v0 solver trả về), target ngoài tầm
// trả false, FallbackShot theo độ cao.
//
// NGÂN SÁCH SAI SỐ (đọc ShotSolver.cs trước khi siết ngưỡng):
// solver hội tụ khi điểm rơi mô phỏng cách "đích ngắm" < 0.15 m, nhưng
// đích ngắm = target + dispersion tất định theo chất lượng (Perfect:
// 0.08 m). Vì vậy điểm rơi so với target gốc có thể lệch tới 0.23 m.
// Test dùng ngưỡng 0.25 m và ghi rõ lý do — không giả vờ solver chính
// xác 0.15 m so với target gốc.

using NUnit.Framework;
using UnityEngine;
using ShuttleLab.Core;
using ShuttleLab.Gameplay;

namespace ShuttleLab.Tests
{
    public class ShotSolverTests
    {
        // 0.15 m hội tụ + 0.08 m dispersion Perfect (xem ghi chú đầu file).
        private const float LandingTolerance = 0.25f;

        private static Vector3 SimulateLanding(Vector3 start, Vector3 v0)
        {
            bool landed = ShuttleAerodynamics.Simulate(start, v0, Vector3.zero,
                5f, 1f / 240f, out Vector3 landing, out float _, out float _);
            Assert.IsTrue(landed, "Mô phỏng phải tìm được điểm rơi.");
            return landing;
        }

        [Test]
        public void Solve_ClearAcrossCourt_Converges()
        {
            Vector3 start = new Vector3(0f, 2.0f, -5.0f);
            Vector3 target = new Vector3(0f, 0f, 5.5f);
            bool ok = ShotSolver.Solve(start, target, ShotType.Clear,
                ShotQuality.Perfect, Vector3.zero, out Vector3 v0);
            Assert.IsTrue(ok, "Clear từ giữa sân sang cuối sân đối thủ phải giải được.");
            Vector3 landing = SimulateLanding(start, v0);
            Assert.AreEqual(target.x, landing.x, LandingTolerance);
            Assert.AreEqual(target.z, landing.z, LandingTolerance);
            Assert.IsTrue(CourtDimensions.IsInSingles(landing));
        }

        [Test]
        public void Solve_DropNearNet_Converges()
        {
            Vector3 start = new Vector3(0f, 2.5f, -2.0f);
            Vector3 target = new Vector3(0.5f, 0f, 1.2f);
            bool ok = ShotSolver.Solve(start, target, ShotType.Drop,
                ShotQuality.Perfect, Vector3.zero, out Vector3 v0);
            Assert.IsTrue(ok, "Drop từ gần lưới phải giải được.");
            Vector3 landing = SimulateLanding(start, v0);
            Assert.AreEqual(target.x, landing.x, LandingTolerance);
            Assert.AreEqual(target.z, landing.z, LandingTolerance);
        }

        [Test]
        public void Solve_ServeLow_IntoBox()
        {
            Vector3 start = new Vector3(-1.295f, 1.1f, -3.58f);
            Vector3 target = CourtDimensions.ServiceBoxCentre(1, 1, false);
            bool ok = ShotSolver.Solve(start, target, ShotType.ServeLow,
                ShotQuality.Perfect, Vector3.zero, out Vector3 v0);
            Assert.IsTrue(ok, "Giao thấp vào ô chéo phải giải được.");
            Vector3 landing = SimulateLanding(start, v0);
            Assert.IsTrue(CourtDimensions.IsInServiceBox(landing, 1, 1, false),
                $"Điểm rơi giao cầu {landing} phải nằm trong ô giao cầu chéo.");
        }

        [Test]
        public void Solve_SmashFromMidCourt_Converges()
        {
            Vector3 start = new Vector3(1.0f, 2.8f, -1.5f);
            Vector3 target = new Vector3(-1.5f, 0f, 4.5f);
            bool ok = ShotSolver.Solve(start, target, ShotType.Smash,
                ShotQuality.Perfect, Vector3.zero, out Vector3 v0);
            Assert.IsTrue(ok, "Smash từ giữa sân phải giải được.");
            Vector3 landing = SimulateLanding(start, v0);
            Assert.AreEqual(target.x, landing.x, LandingTolerance);
            Assert.AreEqual(target.z, landing.z, LandingTolerance);
        }

        [Test]
        public void Solve_UnreachableTarget_ReturnsFalse()
        {
            // Đích cách 30 m ngang sân: cần v0 vượt ngưỡng tốc độ hợp lý
            // (solver trả false khi v0 > 75 m/s hoặc không hội tụ).
            Vector3 start = new Vector3(0f, 1.0f, -6.7f);
            Vector3 target = new Vector3(30f, 0f, 6.7f);
            bool ok = ShotSolver.Solve(start, target, ShotType.Clear,
                ShotQuality.Perfect, Vector3.zero, out Vector3 v0);
            Assert.IsFalse(ok, "Target ngoài tầm vật lý phải trả false để caller fallback.");
        }

        [Test]
        public void FallbackShot_ByHeight()
        {
            Assert.AreEqual(ShotType.Clear, ShotSolver.FallbackShot(2.5f), "Cầu cao -> Clear.");
            Assert.AreEqual(ShotType.Lift, ShotSolver.FallbackShot(0.5f), "Cầu thấp -> Lift.");
            Assert.AreEqual(ShotType.Drive, ShotSolver.FallbackShot(1.5f), "Cầu giữa -> Drive.");
            // Biên: đúng ngưỡng thì rơi về Drive (điều kiện là > 1.9 / < 1.0).
            Assert.AreEqual(ShotType.Drive, ShotSolver.FallbackShot(1.9f));
            Assert.AreEqual(ShotType.Drive, ShotSolver.FallbackShot(1.0f));
        }
    }
}
