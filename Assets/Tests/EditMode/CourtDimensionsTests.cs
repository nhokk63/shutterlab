// CourtDimensionsTests.cs — ShuttleLab (EditMode)
// Kiểm tra hình học sân (BWF) — nguồn thật duy nhất là CourtDimensions.

using NUnit.Framework;
using UnityEngine;
using ShuttleLab.Core;

namespace ShuttleLab.Tests
{
    public class CourtDimensionsTests
    {
        [Test]
        public void IsInSingles_Centre_IsTrue()
        {
            Assert.IsTrue(CourtDimensions.IsInSingles(new Vector3(0f, 0f, 0f)));
            Assert.IsTrue(CourtDimensions.IsInSingles(new Vector3(1f, 0f, -4f)));
            Assert.IsTrue(CourtDimensions.IsInSingles(new Vector3(-2f, 0f, 5f)));
        }

        [Test]
        public void IsInSingles_OnBoundaryLine_IsTrue()
        {
            // Trên vạch (kể cả nửa bề rộng vạch 0.02 m) vẫn tính là trong.
            Assert.IsTrue(CourtDimensions.IsInSingles(new Vector3(2.59f, 0f, 6.70f)));
            Assert.IsTrue(CourtDimensions.IsInSingles(new Vector3(-2.59f, 0f, -6.70f)));
        }

        [Test]
        public void IsInSingles_OutsideSideline_IsFalse()
        {
            Assert.IsFalse(CourtDimensions.IsInSingles(new Vector3(2.62f, 0f, 0f)));
            Assert.IsFalse(CourtDimensions.IsInSingles(new Vector3(-2.62f, 0f, 0f)));
        }

        [Test]
        public void IsInSingles_OutsideBaseline_IsFalse()
        {
            Assert.IsFalse(CourtDimensions.IsInSingles(new Vector3(0f, 0f, 6.73f)));
            Assert.IsFalse(CourtDimensions.IsInSingles(new Vector3(0f, 0f, -6.73f)));
        }

        [Test]
        public void IsInDoubles_WiderThanSingles()
        {
            // x = 2.9 m: ngoài sân đơn nhưng trong sân đôi.
            Assert.IsFalse(CourtDimensions.IsInSingles(new Vector3(2.9f, 0f, 0f)));
            Assert.IsTrue(CourtDimensions.IsInDoubles(new Vector3(2.9f, 0f, 0f)));
            Assert.IsFalse(CourtDimensions.IsInDoubles(new Vector3(3.08f, 0f, 0f)));
        }

        [Test]
        public void IsInServiceBox_AllFourBoxes()
        {
            // Tâm 4 ô giao cầu (x = ±1.295, |z| = 4.34).
            Assert.IsTrue(CourtDimensions.IsInServiceBox(new Vector3(-1.295f, 0f, -4.34f), -1, -1, false));
            Assert.IsTrue(CourtDimensions.IsInServiceBox(new Vector3(1.295f, 0f, -4.34f), -1, 1, false));
            Assert.IsTrue(CourtDimensions.IsInServiceBox(new Vector3(1.295f, 0f, 4.34f), 1, 1, false));
            Assert.IsTrue(CourtDimensions.IsInServiceBox(new Vector3(-1.295f, 0f, 4.34f), 1, -1, false));
        }

        [Test]
        public void IsInServiceBox_WrongSideOrShort_IsFalse()
        {
            // Sai phía (z âm nhưng hỏi ô phía xa).
            Assert.IsFalse(CourtDimensions.IsInServiceBox(new Vector3(1.295f, 0f, -4.34f), 1, 1, false));
            // Sai ô x (chéo sai).
            Assert.IsFalse(CourtDimensions.IsInServiceBox(new Vector3(1.295f, 0f, -4.34f), -1, -1, false));
            // Chưa qua vạch ngắn (|z| = 1.5 < 1.98).
            Assert.IsFalse(CourtDimensions.IsInServiceBox(new Vector3(1.295f, 0f, -1.5f), -1, 1, false));
            // Quá vạch cuối sân đơn (|z| = 6.9 > 6.70).
            Assert.IsFalse(CourtDimensions.IsInServiceBox(new Vector3(1.295f, 0f, 6.9f), 1, 1, false));
        }

        [Test]
        public void ServiceCourtXSign_AllCases()
        {
            // Điểm chẵn -> sân PHẢI của người giao (nhìn từ phía mình).
            // Người giao gần (z<0) quay mặt +z nên phải của họ là x<0 (-1);
            // người giao xa quay mặt -z nên phải của họ là x>0 (+1).
            Assert.AreEqual(-1, CourtDimensions.ServiceCourtXSign(-1, true));
            Assert.AreEqual(1, CourtDimensions.ServiceCourtXSign(-1, false));
            Assert.AreEqual(1, CourtDimensions.ServiceCourtXSign(1, true));
            Assert.AreEqual(-1, CourtDimensions.ServiceCourtXSign(1, false));
        }

        [Test]
        public void ServerPosition_NearRightCourt()
        {
            // Gần, sân phải: x = -1.295, z = -(1.98 + 1.6) = -3.58.
            Vector3 p = CourtDimensions.ServerPosition(-1, -1);
            Assert.AreEqual(-1.295f, p.x, 0.001f);
            Assert.AreEqual(-3.58f, p.z, 0.001f);
            Assert.AreEqual(0f, p.y);
        }

        [Test]
        public void ReceiverPosition_FarLeftBox()
        {
            // Xa, ô trái: x = +1.295 (ô đối chéo), z = +(1.98 + 1.2) = 3.18.
            Vector3 p = CourtDimensions.ReceiverPosition(1, 1);
            Assert.AreEqual(1.295f, p.x, 0.001f);
            Assert.AreEqual(3.18f, p.z, 0.001f);
        }
    }
}
