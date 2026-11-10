// RallyPlayModeTests.cs — ShuttleLab (PlayMode)
// Kiểm tra trận đấu thật trong scene Match: giao cầu -> rally -> ghi điểm,
// AI đánh trả, cầu không xuyên lưới.
//
// YÊU CẦU TRƯỚC KHI CHẠY: scene phải được đăng ký trong Build Settings
// (chạy menu ShuttleLab/Build All Scenes) — SceneManager.LoadScene theo
// tên chỉ chạy với scene đã đăng ký.
// KHÔNG dùng input thật: giao cầu qua MatchManager.RequestUserServe /
// StartPoint, AI tự lái bằng OpponentAI. Người chơi (user) không đánh trả
// nên mọi rally đều kết thúc bằng điểm cho một bên — đó là điều test
// kiểm tra (trận không kẹt), không phải "user thắng".

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ShuttleLab.Core;
using ShuttleLab.Gameplay;
using ShuttleLab.Match;

namespace ShuttleLab.Tests
{
    public class RallyPlayModeTests
    {
        private MatchManager _match;
        private ShuttlecockController _shuttle;

        private IEnumerator LoadMatchScene()
        {
            SceneManager.LoadScene("Match");
            yield return null; // load scene, Awake/Start chạy
            yield return new WaitForSeconds(0.5f);
            _match = Object.FindFirstObjectByType<MatchManager>();
            _shuttle = Object.FindFirstObjectByType<ShuttlecockController>();
            Assert.IsNotNull(_match, "Không tìm thấy MatchManager trong scene Match.");
            Assert.IsNotNull(_shuttle, "Không tìm thấy ShuttlecockController trong scene Match.");
            Assert.IsNotNull(_match.Rules, "MatchManager chưa khởi tạo Rules.");
        }

        [UnityTest]
        public IEnumerator ServeStartsRally_AndPointScores()
        {
            yield return LoadMatchScene();
            Assert.AreEqual(MatchManager.MatchPhase.Serving, _match.Phase,
                "Trận phải bắt đầu ở phase Serving.");
            Assert.AreEqual(0, _match.Rules.Score.Server,
                "Điểm đầu tiên user (0) phải là người giao.");

            _match.RequestUserServe();

            // Đợi cầu bay (Serve phát ở callback onContact của animation,
            // không phải ngay lúc gọi Serve).
            float wait = 0f;
            while (_shuttle.State != ShuttlecockController.ShuttleState.Flying && wait < 5f)
            {
                wait += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(ShuttlecockController.ShuttleState.Flying, _shuttle.State,
                "Cầu phải bay sau khi giao cầu.");

            // Đợi một điểm được ghi (AI đỡ rồi user không trả, hoặc giao
            // ăn điểm trực tiếp). Trận kẹt = fail. Nếu giao lỗi (cầu không
            // bay, watchdog đưa về Serving), giao lại tối đa 3 lần.
            wait = 0f;
            int totalPoints = 0;
            int serveRetries = 0;
            while (wait < 25f)
            {
                totalPoints = _match.Rules.Score.Points[0] + _match.Rules.Score.Points[1];
                if (totalPoints > 0) break;
                if (_match.Phase == MatchManager.MatchPhase.Serving && wait > 5f && serveRetries < 3)
                {
                    serveRetries++;
                    _match.RequestUserServe();
                    wait = 0f;
                }
                wait += Time.deltaTime;
                yield return null;
            }
            Assert.Greater(totalPoints, 0,
                "Sau khi giao cầu, một điểm phải được ghi trong 25 s (không được kẹt trận).");
            Assert.GreaterOrEqual(_match.Stats.ShotsHit, 1,
                "Ít nhất cú giao cầu phải được đếm vào thống kê.");
        }

        [UnityTest]
        public IEnumerator AiReturnsAtLeastOneShot()
        {
            yield return LoadMatchScene();

            // AI có thể đỡ trượt 1 quả giao (ace) nên cho tối đa 5 lần
            // giao: ép user làm người giao lại (white-box trong test) để
            // AI có nhiều cơ hội đỡ. Rallies đếm số lần LastHitter đổi
            // trong phase Rally — tức AI đã đánh trả thành công.
            int maxRallies = 0;
            for (int attempt = 0; attempt < 5 && maxRallies < 1; attempt++)
            {
                _match.Rules.Score.Server = 0;
                _match.Rules.Score.Receiver = 1;
                _match.StartPoint();
                Assert.AreEqual(MatchManager.MatchPhase.Serving, _match.Phase);
                _match.RequestUserServe();

                float wait = 0f;
                while (wait < 10f)
                {
                    maxRallies = Mathf.Max(maxRallies, _match.Stats.Rallies);
                    if (maxRallies >= 1) break;
                    if (_match.Phase == MatchManager.MatchPhase.PointEnd
                        || _match.Phase == MatchManager.MatchPhase.GameEnd)
                        break; // hết điểm mà AI chưa đỡ -> giao lại
                    wait += Time.deltaTime;
                    yield return null;
                }

                // Đợi phase rời PointEnd/GameEnd trước lần giao sau để
                // không đè lên timer chờ của MatchManager.
                float settle = 0f;
                while ((_match.Phase == MatchManager.MatchPhase.PointEnd
                        || _match.Phase == MatchManager.MatchPhase.GameEnd) && settle < 5f)
                {
                    settle += Time.deltaTime;
                    yield return null;
                }
            }

            Assert.GreaterOrEqual(maxRallies, 1,
                "AI phải đánh trả được ít nhất 1 cú trong 5 lần giao (OpponentAI hoạt động).");
        }

        [UnityTest]
        public IEnumerator ShuttleNeverPassesThroughNet()
        {
            yield return LoadMatchScene();
            _match.RequestUserServe();

            // Lấy mẫu vị trí cầu mỗi frame; nếu dấu z đổi phía thì nội suy
            // điểm cắt mặt phẳng lưới (z = 0) và yêu cầu y tại đó phải cao
            // hơn mặt lưới (trừ dung sai 0.10 m). Cầu đi vòng qua cột
            // (|x| > biên đôi) được miễn vì đó là đường hợp lệ.
            Vector3 prev = _shuttle.transform.position;
            float t = 0f;
            while (t < 20f)
            {
                Vector3 cur = _shuttle.transform.position;
                if (_shuttle.State == ShuttlecockController.ShuttleState.Flying
                    && prev.z != 0f && cur.z != 0f
                    && Mathf.Sign(prev.z) != Mathf.Sign(cur.z))
                {
                    float f = prev.z / (prev.z - cur.z);
                    Vector3 cross = Vector3.Lerp(prev, cur, f);
                    if (Mathf.Abs(cross.x) <= CourtDimensions.DoublesHalfWidth - 0.15f)
                    {
                        float netH = CourtDimensions.NetHeightAt(cross.x);
                        Assert.GreaterOrEqual(cross.y, netH - 0.10f,
                            $"Cầu xuyên lưới tại x={cross.x:0.00}, y={cross.y:0.00} < lưới {netH:0.00}.");
                    }
                }
                prev = cur;
                t += Time.deltaTime;
                yield return null;
            }
            Assert.Greater(_match.Stats.ShotsHit, 0,
                "Test vô nghĩa: không có cú đánh nào được thực hiện trong 20 s.");
        }
    }
}
