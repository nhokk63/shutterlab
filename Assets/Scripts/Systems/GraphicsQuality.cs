using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShuttleLab.Systems
{
    /// <summary>
    /// Cài đặt đồ hoạ: preset chất lượng + FPS + độ phân giải (renderScale),
    /// lưu PlayerPrefs "shuttlelab_gfx_*", áp dụng ngay và khi khởi động
    /// (GameStateManager.Awake gọi ApplySaved).
    ///
    /// - FPS: 30 / 60 / 90 / 120. 120fps cần màn 120Hz+ (OnePlus 15: 165Hz).
    ///   Đặt vSyncCount = 0 để targetFrameRate khóa đúng số (vSync = 1 sẽ
    ///   khóa theo tần số quét màn hình, không ra đúng 120 trên màn 165Hz).
    /// - Độ phân giải: URP renderScale 0.6–1.0 (trên Android KHÔNG dùng
    ///   Screen.SetResolution như PC). renderScale 1.0 ở 2772×1272 ≈ 3.5Mpx.
    /// - MSAA: tắt = 1, bật = 2/4. Bóng: tắt = shadowDistance 0.
    /// - Bloom/hậu kỳ: chưa có Volume hậu kỳ ở v0.1.0 — cờ chỉ lưu sẵn,
    ///   có tác dụng từ v0.2 khi Volume được dựng.
    /// - Lần đầu mở game: chưa có key nào → tự detect theo máy
    ///   (RAM + nhân CPU + dòng GPU) rồi chọn preset phù hợp.
    /// </summary>
    public static class GraphicsQuality
    {
        public enum Tier { Low = 0, Balanced = 1, High = 2, Ultra = 3 }

        public const string KeyTier = "shuttlelab_gfx_tier";
        public const string KeyFps = "shuttlelab_gfx_fps";
        public const string KeyScale = "shuttlelab_gfx_scale";
        public const string KeyMsaa = "shuttlelab_gfx_msaa";
        public const string KeyShadows = "shuttlelab_gfx_shadows";
        public const string KeyShadowDistance = "shuttlelab_gfx_shadowdist";
        public const string KeyBloom = "shuttlelab_gfx_bloom";

        public static readonly string[] TierNames = { "Tiết kiệm", "Cân bằng", "Cao", "Tối đa" };
        public static readonly int[] FpsOptions = { 30, 60, 90, 120 };

        public static Tier CurrentTier =>
            (Tier)Mathf.Clamp(PlayerPrefs.GetInt(KeyTier, (int)AutoDetect()), 0, 3);

        public static int CurrentFps => PlayerPrefs.GetInt(KeyFps, FpsForTier(CurrentTier));
        public static float CurrentScale => PlayerPrefs.GetFloat(KeyScale, ScaleForTier(CurrentTier));
        public static int CurrentMsaa => PlayerPrefs.GetInt(KeyMsaa, MsaaForTier(CurrentTier));
        public static bool ShadowsOn => PlayerPrefs.GetInt(KeyShadows, CurrentTier == Tier.Low ? 0 : 1) != 0;
        public static bool BloomOn => PlayerPrefs.GetInt(KeyBloom, CurrentTier == Tier.Ultra ? 1 : 0) != 0;

        public static int FpsForTier(Tier t)
        {
            switch (t)
            {
                case Tier.Low: return 30;
                case Tier.Ultra: return 120;
                default: return 60;
            }
        }

        public static float ScaleForTier(Tier t)
        {
            switch (t)
            {
                case Tier.Low: return 0.6f;
                case Tier.Balanced: return 0.75f;
                case Tier.High: return 0.85f;
                default: return 1.0f;
            }
        }

        public static int MsaaForTier(Tier t)
        {
            switch (t)
            {
                case Tier.Low: return 1;
                case Tier.Ultra: return 4;
                default: return 2;
            }
        }

        public static float ShadowDistanceForTier(Tier t)
        {
            switch (t)
            {
                case Tier.Low: return 0f;
                case Tier.Balanced: return 25f;
                default: return 30f;
            }
        }

        /// <summary>Đoán preset theo phần cứng (RAM + nhân CPU + dòng GPU).</summary>
        public static Tier AutoDetect()
        {
            int ramMb = SystemInfo.systemMemorySize;
            int cores = SystemInfo.processorCount;
            string gpu = (SystemInfo.graphicsDeviceName ?? "").ToLowerInvariant();
            bool flagshipGpu = gpu.Contains("adreno 8") || gpu.Contains("adreno 7")
                || gpu.Contains("mali-g7") || gpu.Contains("mali-g8")
                || gpu.Contains("apple a1") || gpu.Contains("apple m");
            if (ramMb >= 8000 && cores >= 8 && flagshipGpu) return Tier.Ultra;
            if (ramMb >= 6000) return Tier.High;
            if (ramMb >= 4000) return Tier.Balanced;
            return Tier.Low;
        }

        /// <summary>Áp preset: đổi FPS + độ phân giải + MSAA + bóng một lượt.</summary>
        public static void Apply(Tier tier)
        {
            PlayerPrefs.SetInt(KeyTier, (int)tier);
            PlayerPrefs.SetInt(KeyFps, FpsForTier(tier));
            PlayerPrefs.SetFloat(KeyScale, ScaleForTier(tier));
            PlayerPrefs.SetInt(KeyMsaa, MsaaForTier(tier));
            PlayerPrefs.SetInt(KeyShadows, tier == Tier.Low ? 0 : 1);
            PlayerPrefs.SetInt(KeyBloom, tier == Tier.Ultra ? 1 : 0);
            PlayerPrefs.SetFloat(KeyShadowDistance, ShadowDistanceForTier(tier));
            PlayerPrefs.Save();
            ApplyFps(PlayerPrefs.GetInt(KeyFps));
            ApplyUrp();
        }

        public static void ApplyFps(int fps)
        {
            fps = Mathf.Clamp(fps, 30, 120);
            PlayerPrefs.SetInt(KeyFps, fps);
            PlayerPrefs.Save();
            Application.targetFrameRate = fps;
            QualitySettings.vSyncCount = 0;
        }

        public static void ApplyScale(float scale)
        {
            scale = Mathf.Clamp(scale, 0.5f, 1f);
            PlayerPrefs.SetFloat(KeyScale, scale);
            PlayerPrefs.Save();
            ApplyUrp();
        }

        /// <summary>Áp setting đã lưu (gọi khi khởi động). Lần đầu mở game
        /// thì tự detect theo máy.</summary>
        public static void ApplySaved()
        {
            if (!PlayerPrefs.HasKey(KeyTier))
            {
                Apply(AutoDetect());
                return;
            }
            ApplyFps(PlayerPrefs.GetInt(KeyFps, 60));
            ApplyUrp();
        }

        private static void ApplyUrp()
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) return;
            urp.renderScale = Mathf.Clamp(PlayerPrefs.GetFloat(KeyScale, 1f), 0.5f, 1f);
            urp.msaaSampleCount = PlayerPrefs.GetInt(KeyMsaa, 4);
            urp.shadowDistance = PlayerPrefs.GetFloat(KeyShadowDistance, 30f);
        }
    }
}
