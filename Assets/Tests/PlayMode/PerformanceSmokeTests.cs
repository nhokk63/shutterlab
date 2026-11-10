// PerformanceSmokeTests.cs — ShuttleLab (PlayMode)
// Smoke test hiệu năng: đo FPS trung bình trong scene Match đang chạy
// trận thật (AI + vật lý + HUD).
//
// NGƯỠNG 20 FPS CHỈ ÁP DỤNG KHI CHẠY TRÊN THIẾT BỊ THẬT. Trên máy build
// cloud (batchmode, render phần mềm WARP, không GPU) URP 3D có bóng có thể
// chỉ đạt 5–15 FPS trong khi trên điện thoại mục tiêu vẫn 60 FPS — nên ở
// batchmode test chỉ LOG fps, không assert ngưỡng. Warmup 3 s realtime +
// bỏ 1 s đầu để loại nhiễu biên dịch shader / khởi tạo scene.

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShuttleLab.Tests
{
    public class PerformanceSmokeTests : IPrebuildSetup
    {
        // Dựng scene trước khi test chạy (xem ScenePrebuildSetup).
        public void Setup() => ScenePrebuildSetup.EnsureScenes();

        private const float MeasureSeconds = 5f;
        private const float MinAverageFps = 20f;

        [UnityTest]
        public IEnumerator MatchScene_AverageFps_Above20_Over5Seconds()
        {
            SceneManager.LoadScene("Match");
            yield return null;
            Assert.AreEqual("Match", SceneManager.GetActiveScene().name,
                "Scene 'Match' không load được — chưa đăng ký trong Build Settings (cần PreExport/SceneBuilder chạy trước test).");
            var match = Object.FindFirstObjectByType<ShuttleLab.Match.MatchManager>();
            Assert.IsNotNull(match, "Không tìm thấy MatchManager trong scene Match.");
            yield return new WaitForSecondsRealtime(3f);
            Assert.AreNotEqual(ShuttleLab.Match.MatchManager.MatchPhase.Idle, match.Phase,
                "Trận chưa bắt đầu sau warmup — FPS đo được không phải của trận thật.");
            int frames = 0; float elapsed = 0f; bool discarded = false;
            while (elapsed < MeasureSeconds + 1f)
            {
                elapsed += Time.unscaledDeltaTime; frames++;
                if (!discarded && elapsed >= 1f) { discarded = true; frames = 0; elapsed = 0f; }
                yield return null;
            }
            float fps = frames / Mathf.Max(0.01f, elapsed);
            Debug.Log($"[PerformanceSmoke] Scene Match: {fps:0.0} FPS trung bình trong {elapsed:0.0}s ({frames} frames).");
            if (Application.isBatchMode)
            {
                Debug.LogWarning("[PerformanceSmoke] Chạy batchmode/cloud — chỉ log FPS, không assert ngưỡng 20.");
                yield break;
            }
            Assert.GreaterOrEqual(fps, MinAverageFps,
                $"FPS trung bình trong scene Match phải >= {MinAverageFps}.");
        }
    }
}
