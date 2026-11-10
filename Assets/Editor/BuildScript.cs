// BuildScript.cs — ShuttleLab (Unity 6, Editor only)
//
// Build APK Android: MenuItem "ShuttleLab/Build Android APK" hoặc gọi từ
// batchmode:
//   Unity -batchmode -quit -projectPath <project>
//         -executeMethod ShuttleLab.Editor.BuildScript.BuildAndroid
//
// Trước khi build sẽ dựng lại 3 scene qua SceneBuilder (idempotent) để
// APK luôn dùng scene mới nhất, rồi đặt PlayerSettings và gọi
// BuildPipeline.BuildPlayer. Lỗi build được in ra log và throw (để
// batchmode trả exit code != 0 thay vì im lặng "thành công giả").
//
// QUYẾT ĐỊNH KỸ THUẬT v0.1.0:
//  - Scripting backend IL2CPP — BẮT BUỘC, không phải Mono. Đào ra
//    2026-11-10 (ikdasm + kiểm tra đĩa): Unity 6000.0.64f1 chỉ ship Mono
//    cho ARMv7 (Variations/mono/*/Libs chỉ có armeabi-v7a, KHÔNG có
//    arm64-v8a; il2cpp thì có đủ arm64-v8a). Build pipeline lọc kiến trúc
//    theo backend (FilteredTargetArchitectures) nên Mono + ARM64 bị lọc về
//    0 → chết "Target architecture not specified" ở Checking prerequisites
//    (4 lần build local chết y hệt dù setting ARM64 đã dính). ARMv7-only
//    cũng vô dụng vì OnePlus 15 (Snapdragon 8 Elite Gen 5) là máy 64-bit
//    only, không chạy code 32-bit. IL2CPP + ARM64 là tổ hợp duy nhất được.
//    IL2CPP biên dịch C++ lâu và tốn RAM hơn — chấp nhận vì không còn lựa chọn.
//  - Strip engine code KHÔNG bật (giữ an toàn bản đầu — tránh strip nhầm
//    class dùng qua reflection / SendMessage như HUDManager.BindMatch).
//  - Chỉ ARM64 (arm64-v8a), theo yêu cầu; target API 35, min API 24,
//    Graphics API OpenGLES3, màn hình ngang 2 chiều.
//  - APK ra ~/workspace/shuttlelab/build/ShuttleLab-Android-v0.1.0.apk
//    (đường dẫn tuyệt đối qua HOME trên Linux / USERPROFILE trên Windows).

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShuttleLab.Editor
{
    public static class BuildScript
    {
        public const string CompanyName = "ShuttleLab";
        public const string ProductName = "ShuttleLab";
        public const string PackageName = "com.shuttlelab.badminton";
        public const string Version = "0.1.0";
        public const int BundleVersionCode = 1;

        /// <summary>Đường dẫn APK đầu ra (tuyệt đối).</summary>
        public static string ApkPath
        {
            get
            {
                string home = Environment.GetEnvironmentVariable("HOME")
                    ?? Environment.GetEnvironmentVariable("USERPROFILE");
                string dir = string.IsNullOrEmpty(home)
                    ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "build"))
                    : Path.Combine(home, "workspace", "shuttlelab", "build");
                return Path.Combine(dir, "ShuttleLab-Android-v0.1.0.apk");
            }
        }

        [MenuItem("ShuttleLab/Build Android APK")]
        public static void BuildAndroidFromMenu()
        {
            BuildAndroid();
        }

        /// <summary>
        /// Entry cho batchmode (-executeMethod). Không tham số, static,
        /// throw khi thất bại để batchmode trả exit code lỗi.
        /// </summary>
        public static void BuildAndroid()
        {
            // Chuyển active build target sang Android TRƯỚC mọi thứ:
            // PlayerSettings.Android.* (đặc biệt targetArchitectures) chỉ
            // có tác dụng khi Android đang là active target — build local
            // 12:10 chết với "Target architecture not specified" dù
            // ApplyPlayerSettings đã chạy và gán ARM64, vì lúc đó active
            // target vẫn là Standalone (repo để PlayerSettings mặc định,
            // cờ -buildTarget Android không tự chuyển target). Switch ở
            // đây sẽ reimport asset cho Android (tốn vài phút) nhưng là
            // bước bắt buộc để setting dính.
            bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android, BuildTarget.Android);
            Debug.Log($"[BuildScript] SwitchActiveBuildTarget(Android) = {switched}, "
                + $"active target giờ là {EditorUserBuildSettings.activeBuildTarget}.");

            // Dựng lại scene trước để APK không dùng scene cũ.
            SceneBuilder.BuildAllScenes();
            ApplyPlayerSettings();

            // Kiểm chứng setting dính trước khi build (fail nhanh với
            // thông báo rõ thay vì chết khó hiểu ở BuildPlayer).
            if (PlayerSettings.Android.targetArchitectures == AndroidArchitecture.None)
                throw new Exception("[BuildScript] targetArchitectures vẫn là None sau khi "
                    + "gán ARM64 — không build được. Kiểm tra active build target.");
            Debug.Log($"[BuildScript] targetArchitectures = {PlayerSettings.Android.targetArchitectures}.");
            AssetDatabase.SaveAssets();

            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new Exception("[BuildScript] Không có scene nào trong Build Settings — "
                    + "chạy ShuttleLab/Build All Scenes trước.");

            string apkDir = Path.GetDirectoryName(ApkPath);
            if (!string.IsNullOrEmpty(apkDir))
                Directory.CreateDirectory(apkDir);

            Debug.Log($"[BuildScript] Bắt đầu build Android APK: {ApkPath}\n"
                + $"  Scenes: {string.Join(", ", scenes)}\n"
                + $"  Package: {PackageName} v{Version} (code {BundleVersionCode}), ARM64, IL2CPP.");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"[BuildScript] Kết quả: {summary.result} — "
                + $"{summary.totalErrors} lỗi, {summary.totalWarnings} cảnh báo, "
                + $"dung lượng {summary.totalSize / 1048576.0:0.0} MB, "
                + $"thời gian {summary.totalTime.TotalSeconds:0.0}s.");

            if (summary.result != BuildResult.Succeeded)
                throw new Exception("[BuildScript] Build Android thất bại: "
                    + $"{summary.totalErrors} lỗi. Xem log ở trên để biết file/lỗi cụ thể. "
                    + $"APK mong đợi tại: {ApkPath}");

            if (!File.Exists(ApkPath))
                throw new Exception("[BuildScript] Build báo thành công nhưng không thấy APK tại: "
                    + ApkPath);

            Debug.Log("[BuildScript] APK xong: " + ApkPath);
        }

        /// <summary>
        /// Hook cho Unity Build Automation (cloud build): chạy sau khi biên
        /// dịch script, trước khi export. Dựng lại 3 scene qua SceneBuilder
        /// + đặt PlayerSettings để bản build cloud dùng đúng scene và đúng
        /// package (repo không commit scene, EditorBuildSettings trong repo
        /// đang trống nên không có hook này thì cloud build ra APK rỗng).
        /// Khai báo trong Dashboard: build target → Advanced settings →
        /// Pre-export method name = ShuttleLab.Editor.BuildScript.PreExport
        /// Không tham số nên biên dịch được cả local lẫn cloud (không cần
        /// #if UNITY_CLOUD_BUILD).
        /// </summary>
        public static void PreExport()
        {
            SceneBuilder.BuildAllScenes();
            ApplyPlayerSettings();
            Debug.Log("[BuildScript] PreExport xong: scene + PlayerSettings "
                + "đã sẵn sàng cho cloud build.");
        }

        private static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.colorSpace = ColorSpace.Linear; // URP thiết kế cho Linear, Gamma làm sáng/tối sai
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.Android.bundleVersionCode = BundleVersionCode;

            // IL2CPP bắt buộc (xem ghi chú đầu file): Mono trên Android của
            // Unity 6 chỉ có ARMv7, không có ARM64.
            PlayerSettings.SetScriptingBackend(
                BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);

            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            // PlayerSettings không có allowedOrientations — khóa ngang bằng
            // defaultInterfaceOrientation ở trên là đủ.

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });

            // Không strip engine code ở bản đầu (tránh strip nhầm class dùng
            // qua reflection / SendMessage).
            PlayerSettings.stripEngineCode = false;

            // SDK/NDK/JDK: Unity tự dùng bộ đi kèm Android Build Support.
            // Chỉ log để chẩn đoán nếu build lỗi vì thiếu SDK.
            string sdkRoot = EditorPrefs.GetString("AndroidSdkRoot");
            Debug.Log(string.IsNullOrEmpty(sdkRoot)
                ? "[BuildScript] AndroidSdkRoot chưa đặt — Unity sẽ dùng SDK đi kèm Android Build Support."
                : "[BuildScript] AndroidSdkRoot = " + sdkRoot);
        }
    }
}
