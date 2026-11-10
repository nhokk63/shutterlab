// ScenePrebuildSetup.cs — ShuttleLab (PlayMode tests)
//
// Dựng 3 scene bằng BuildScript.PreExport TRƯỚC KHI test chạy, để
// SceneManager.LoadScene("Match") có scene mà load trên checkout sạch
// (repo không có file .unity; PreExport của UBA chỉ chạy ở phase export,
// SAU test — đã chứng minh từ log build #10: cả 2 lần gọi Unity đều là
// -runTests, không có -executeMethod nào).
//
// PreExport gọi qua reflection vì test assembly không được tham chiếu
// Assembly-CSharp-Editor (Unity lặng lẽ bỏ qua tham chiếu đó — bài học
// build #5). Setup chạy tối đa 1 lần mỗi lượt test (cờ tĩnh) dù cả 2
// class PlayMode đều khai IPrebuildSetup.
//
// Scene nhẹ cho test (build #11/#12 treo khi load scene Match nặng trên
// máy build cloud render phần mềm WARP): trước khi gọi PreExport, bật
// SceneBuilder.BuildLightForTests qua reflection — scene Match dựng cho
// test bỏ khán giả + đèn arena, directional làm đèn chính. Phase export
// thật chạy ở tiến trình Unity riêng nên cờ không rò sang bản APK.

using System;
using System.Reflection;

namespace ShuttleLab.Tests
{
    public static class ScenePrebuildSetup
    {
        private static bool s_done;

        public static void EnsureScenes()
        {
            if (s_done) return;
            s_done = true;
            var builderType = Type.GetType("ShuttleLab.Editor.SceneBuilder, Assembly-CSharp-Editor");
            if (builderType == null)
                throw new Exception("[ScenePrebuildSetup] Không tìm thấy ShuttleLab.Editor.SceneBuilder trong Assembly-CSharp-Editor.");
            var lightFlag = builderType.GetField("BuildLightForTests", BindingFlags.Public | BindingFlags.Static);
            if (lightFlag == null)
                throw new Exception("[ScenePrebuildSetup] Không tìm thấy SceneBuilder.BuildLightForTests.");
            lightFlag.SetValue(null, true);
            var type = Type.GetType("ShuttleLab.Editor.BuildScript, Assembly-CSharp-Editor");
            if (type == null)
                throw new Exception("[ScenePrebuildSetup] Không tìm thấy ShuttleLab.Editor.BuildScript trong Assembly-CSharp-Editor.");
            var method = type.GetMethod("PreExport", BindingFlags.Public | BindingFlags.Static);
            if (method == null)
                throw new Exception("[ScenePrebuildSetup] Không tìm thấy BuildScript.PreExport.");
            method.Invoke(null, null);
        }
    }
}
