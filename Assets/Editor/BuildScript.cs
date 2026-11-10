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
//  - Scripting backend Mono (không phải IL2CPP): máy build yếu (RAM 7.7 GB),
//    IL2CPP biên dịch C++ lâu và tốn RAM, dễ fail giữa chừng; Mono build
//    nhanh và đủ cho bản đầu. Đổi sang IL2CPP ở bản sau khi có máy mạnh hơn
//    hoặc cần tối ưu hiệu năng / chống dịch ngược.
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
using UnityEditor.Build.Reporting;
using UnityEngine;

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
            // Dựng lại scene trước để APK không dùng scene cũ.
            SceneBuilder.BuildAllScenes();
            ApplyPlayerSettings();

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
                + $"  Package: {PackageName} v{Version} (code {BundleVersionCode}), ARM64, Mono.");

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

            // Mono cho bản đầu (xem ghi chú đầu file). IL2CPP để sau.
            PlayerSettings.SetScriptingBackend(
                BuildTargetGroup.Android, ScriptingImplementation.Mono2x);

            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedOrientations =
                UIOrientation.LandscapeLeft | UIOrientation.LandscapeRight;

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
