// PerformanceSmokeTests.cs — ShuttleLab (PlayMode)
// Smoke test hiệu năng: đo FPS trung bình 5 s trong scene Match đang chạy
// trận thật (AI + vật lý + HUD).
//
// NGƯỠNG 20 FPS LÀ SMOKE TEST TRÊN MÁY BUILD, KHÔNG PHẢI BENCHMARK
// THIẾT BỊ THẬT: máy build chạy qua Xvfb / render phần mềm có thể chậm
// hơn điện thoại thật rất nhiều. Nếu test này đỏ trên máy build, xem log
// FPS trước khi kết luận game chậm — ngưỡng đúng cho thiết bị mục tiêu
// (Android ARM64) sẽ được chốt sau khi đo trên APK thật.

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShuttleLab.Tests
{
    public class PerformanceSmokeTests
    {
        private const float MeasureSeconds = 5f;
        private const float MinAverageFps = 20f;

        [UnityTest]
        public IEnumerator MatchScene_AverageFps_Above20_Over5Seconds()
        {
            SceneManager.LoadScene("Match");
            yield return null;
            yield return new WaitForSeconds(1f); // warmup: bootstrap + frame đầu

            int frames = 0;
            float elapsed = 0f;
            while (elapsed < MeasureSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                frames++;
                yield return null;
            }

            float fps = frames / Mathf.Max(0.01f, elapsed);
            Debug.Log($"[PerformanceSmoke] Scene Match: {fps:0.0} FPS trung bình "
                + $"trong {elapsed:0.0}s ({frames} frames).");

            Assert.GreaterOrEqual(fps, MinAverageFps,
                $"FPS trung bình trong scene Match phải >= {MinAverageFps} "
                + "(smoke test trên máy build; xem ghi chú đầu file).");
        }
    }
}
