// SceneBuilder.cs — ShuttleLab (Unity 6, Editor only)
//
// Dựng 3 scene bằng code (MenuItem "ShuttleLab/Build All Scenes") — KHÔNG
// sửa file .unity tay (ARCHITECTURE.md). Chạy được cả trong Editor lẫn
// batchmode headless (được BuildScript gọi trước khi build APK).
//
// Scene tạo ra:
//  - MainMenu: Camera + "GameStateManager" + "MainMenu" (tự dựng Canvas).
//  - Match: sân đơn đúng CourtDimensions, 2 Player (PlayerController),
//    Shuttlecock, GameBootstrap, MatchManager, CameraRig, HUDManager,
//    MobileInput (+ JoystickPanel / ShotButtonsPanel), AudioManager.
//    v0.2 phase 1: nhà thi đấu tối (tường + trần tối, khán đài 3 tầng,
//    ~1000 khán giả, dải LED), 6 spotlight 5600K (1 đổ bóng) + 4 nón
//    sáng giả, sân xanh truyền hình + viền xanh lá, biển quảng cáo HƯ CẤU.
//  - Career: Camera + "CareerUI" (tự dựng Canvas).
// Sau đó đăng ký 3 scene vào EditorBuildSettings theo thứ tự
// MainMenu (0), Match (1), Career (2).
//
// GameObject BẮT BUỘC theo tên (các manager tìm theo tên / GameBootstrap
// nối bằng SendMessage):
//  - "HUDManager" (Match) — GameBootstrap SendMessage("BindMatch").
//  - "JoystickPanel" / "ShotButtonsPanel" (Match) — MobileInputManager
//    tự tìm theo tên để đảo tay trái/phải.
//  - "GameStateManager" (MainMenu) — singleton DontDestroyOnLoad; các
//    scene khác tự tạo nếu thiếu nên không cần đặt thêm.
//  - "Player0_User" / "Player1_AI" — GameBootstrap.EnsurePlayers() tìm
//    theo vị trí z nếu MatchManager.Players chưa gán (SceneBuilder gán
//    sẵn nên chỉ là fallback).
//
// LƯU Ý:
//  - Kit màu (user áo đỏ, AI áo trắng-xanh theo refs) KHÔNG đặt ở đây:
//    humanoid được Build trong PlayerController.Init lúc chạy, màu lấy từ
//    PlayerStats do GameBootstrap gán — xem ghi chú kit trong
//    GameBootstrap.cs (đã chỉnh cho khớp refs).
//  - Nhân vật CC0 (Quaternius Universal Base Characters, CC0, bản Superhero
//    — KHÔNG phải bản Regular): model FBX nằm ở Assets/Models/CC0
//    (convert từ GLB bằng Blender, tools/glb_to_fbx.py). SceneBuilder gắn
//    CC0CharacterRig lên 2 Player (user = nam, AI = nữ); thiếu file thì
//    PlayerController.Init fallback humanoid procedural, không dừng build.
//    Xem Assets/Models/CC0/README_CC0.md.
//  - Materials + URP asset được lưu thành asset thật (Assets/Materials,
//    Assets/Settings) để scene serialize đúng; chạy lại menu sẽ tái sử
//    dụng asset đã có thay vì tạo trùng.
//  - Không tạo prefab ở v0.1.0 (chưa cần); scene là nguồn thật duy nhất.

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using ShuttleLab.Camera;
using ShuttleLab.Core;
using ShuttleLab.Gameplay;
using ShuttleLab.Input;
using ShuttleLab.Match;
using ShuttleLab.Players;
using ShuttleLab.Systems;
using ShuttleLab.UI;

namespace ShuttleLab.Editor
{
    public static class SceneBuilder
    {
        public const string SceneDir = "Assets/Scenes";
        public const string SettingsDir = "Assets/Settings";
        public const string MaterialsDir = "Assets/Materials";

        public const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        public const string MatchScenePath = "Assets/Scenes/Match.unity";
        public const string CareerScenePath = "Assets/Scenes/Career.unity";
        public const string UrpAssetPath = "Assets/Settings/ShuttleLabURP.asset";

        // Model nhân vật CC0 (Quaternius, CC0) — FBX convert từ GLB bằng
        // Blender (tools/glb_to_fbx.py). Xem Assets/Models/CC0/README_CC0.md.
        public const string Cc0MaleModelPath = "Assets/Models/CC0/UBC_SuperheroMale.fbx";
        public const string Cc0FemaleModelPath = "Assets/Models/CC0/UBC_SuperheroFemale.fbx";

        // Test PlayMode trên máy build cloud (batchmode, render phần mềm
        // WARP) kẹt ở lúc load scene Match nặng (1008 khán giả + 6
        // spotlight + bóng URP) — build #11/#12 treo đúng chỗ này 2 lần.
        // Khi cờ này bật, BuildMatchScene dựng scene nhẹ cho test: bỏ
        // khán giả + đèn arena, đèn directional làm đèn chính. Chỉ
        // ScenePrebuildSetup bật cờ này; phase export thật chạy ở tiến
        // trình Unity riêng nên cờ không rò sang bản APK.
        // Mặc định false = scene đầy đủ.
        public static bool BuildLightForTests = false;

        // --- Màu ------------------------------------------------------------
        private static readonly Color CourtGreen = new Color(0.12f, 0.45f, 0.25f);
        private static readonly Color FloorDark = new Color(0.05f, 0.08f, 0.14f);
        private static readonly Color LineWhite = new Color(0.95f, 0.95f, 0.92f);
        private static readonly Color PostDark = new Color(0.10f, 0.10f, 0.12f);
        private static readonly Color StandDark = new Color(0.07f, 0.08f, 0.11f);
        private static readonly Color BoardGreen = new Color(0.05f, 0.35f, 0.20f);
        private static readonly Color BoardBlue = new Color(0.08f, 0.20f, 0.45f);
        private static readonly Color ShuttleWhite = new Color(0.96f, 0.96f, 0.92f);

        // v0.2 phase 1 — sân xanh truyền hình + nhà thi đấu tối.
        // Màu viết theo không gian Linear (project dùng Linear, xem C20 audit).
        private static readonly Color CourtBlue = new Color(0.10f, 0.32f, 0.62f);
        private static readonly Color ArenaWall = new Color(0.02f, 0.02f, 0.04f);
        private static readonly Color LedCyan = new Color(0.50f, 0.85f, 1.00f);

        // Biển quảng cáo HƯ CẤU — không dùng tên thương hiệu / giải thật.
        private const string AdBrandText = "SHUTTLELAB";

        [MenuItem("ShuttleLab/Build All Scenes")]
        public static void BuildAllScenes()
        {
            EnsureFolders();
            EnsureUrpAsset();
            BuildMainMenuScene();
            BuildMatchScene();
            BuildCareerScene();
            RegisterBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SceneBuilder] Xong: MainMenu, Match, Career — đã đăng ký vào Build Settings.");
        }

        // --- URP ------------------------------------------------------------

        /// <summary>
        /// Tạo (hoặc tái sử dụng) UniversalRenderPipelineAsset và gán làm
        /// render pipeline toàn project. Chạy lại menu sẽ không tạo trùng.
        /// </summary>
        public const string UrpRendererPath = "Assets/Settings/ShuttleLabRenderer.asset";

        public static void EnsureUrpAsset()
        {
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            // Repair an existing asset created by the old code (null renderer).
            // GetRenderer(0) có thể ném NullReferenceException khi asset cũ
            // thiếu renderer (nó đọc m_RendererDataList[defaultIndex].name
            // trên entry null) — bọc try/catch và coi như thiếu renderer.
            bool rendererMissing = false;
            try { rendererMissing = urp != null && urp.GetRenderer(0) == null; }
            catch (System.Exception e)
            {
                Debug.LogWarning("[SceneBuilder] Không đọc được renderer của URP asset cũ ("
                    + e.GetType().Name + ") — tạo lại.");
                rendererMissing = true;
            }
            if (rendererMissing)
            {
                Debug.LogWarning("[SceneBuilder] URP asset cũ thiếu renderer — tạo lại.");
                AssetDatabase.DeleteAsset(UrpAssetPath);
                AssetDatabase.DeleteAsset(UrpRendererPath);
                urp = null;
            }
            if (urp == null)
            {
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, UrpRendererPath);
                urp = UniversalRenderPipelineAsset.Create(rendererData);
                urp.renderScale = 1f;
                urp.msaaSampleCount = 4;
                AssetDatabase.CreateAsset(urp, UrpAssetPath);
                Debug.Log("[SceneBuilder] Đã tạo URP asset: " + UrpAssetPath);
            }
            // v0.2 phase 1 — ép các giá trị ánh sáng/bóng cho nhà thi đấu tối.
            // Áp dụng cả khi asset đã tồn tại, để chạy lại menu vẫn đúng.
            // shadowDistance 50 -> 28: cả nhà thi đấu chỉ ~35 m, 50 m là lãng phí.
            urp.shadowDistance = 28f;
            // 6 spotlight sân + 1 directional: Forward giới hạn mặc định 4
            // đèn phụ trên mỗi object — nâng lên 8 để đủ 6 spot.
            urp.maxAdditionalLightsCount = 8;
            // Atlas bóng của đèn phụ: 1024 cho spot đổ bóng duy nhất.
            urp.additionalLightsShadowmapResolution = 1024;
            // Bật bóng đèn phụ: setter của supportsAdditionalLightShadows là
            // internal nên phải sửa qua SerializedProperty (mặc định false).
            var urpSO = new SerializedObject(urp);
            var addShadowProp = urpSO.FindProperty("m_AdditionalLightShadowsSupported");
            if (addShadowProp != null)
            {
                addShadowProp.boolValue = true;
                urpSO.ApplyModifiedProperties();
            }
            else
            {
                Debug.LogWarning("[SceneBuilder] Không thấy m_AdditionalLightShadowsSupported — spotlight đổ bóng có thể không hiển thị.");
            }
            GraphicsSettings.renderPipelineAsset = urp;
        }

        // --- MainMenu ---------------------------------------------------------

        public static void BuildMainMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupSceneLighting(new Color(0.03f, 0.10f, 0.06f));

            CreateCamera("MainCamera", new Color(0.03f, 0.10f, 0.06f),
                new Vector3(0f, 3f, -8f), new Vector3(0f, 1f, 0f));

            // Singleton flow (DontDestroyOnLoad). Các scene khác tự tạo
            // nếu thiếu nên chỉ MainMenu — scene vào đầu tiên — mới cần.
            var gsm = new GameObject("GameStateManager");
            gsm.AddComponent<GameStateManager>();

            // MainMenu tự dựng Canvas + toàn bộ UI bằng code trong Awake.
            var menu = new GameObject("MainMenu");
            menu.AddComponent<MainMenu>();

            UIBuilder.EnsureEventSystem();
            SaveScene(scene, MainMenuScenePath);
        }

        // --- Career -----------------------------------------------------------

        public static void BuildCareerScene()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupSceneLighting(new Color(0.03f, 0.10f, 0.06f));

            CreateCamera("MainCamera", new Color(0.03f, 0.10f, 0.06f),
                new Vector3(0f, 3f, -8f), new Vector3(0f, 1f, 0f));

            // CareerUI tự dựng Canvas + toàn bộ UI bằng code trong Awake,
            // tự tạo GameStateManager nếu thiếu.
            var career = new GameObject("CareerUI");
            career.AddComponent<CareerUI>();

            UIBuilder.EnsureEventSystem();
            SaveScene(scene, CareerScenePath);
        }

        // --- Match ------------------------------------------------------------

        public static void BuildMatchScene()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupSceneLighting(new Color(0.02f, 0.03f, 0.05f));
            // v0.2 phase 1 — nhà thi đấu tối: ambient rất mờ + sương nhẹ để
            // khán đài xa chìm vào bóng (tỉ lệ sáng sân:khán đài ~8:1–12:1).
            RenderSettings.ambientLight = new Color(0.05f, 0.06f, 0.09f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.008f;
            RenderSettings.fogColor = new Color(0.008f, 0.009f, 0.015f);

            BuildCourt();
            BuildStandsAndBoards();
            if (!BuildLightForTests)
            {
                BuildArenaLights();
                BuildCrowd();
            }

            // --- Đèn nền mờ (fill) ---
            // v0.2 phase 1: đèn chính là 6 spotlight trong BuildArenaLights.
            // Directional chỉ còn là fill rất mờ, màu xanh lạnh, không bóng.
            // Scene nhẹ cho test (BuildLightForTests): không có spotlight
            // nên directional làm đèn chính để sân vẫn nhìn được.
            var sun = new GameObject("DirectionalLight");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = BuildLightForTests ? 1.2f : 0.1f;
            light.color = new Color(0.55f, 0.65f, 0.85f);
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // --- Cầu (mesh tạo bằng code: nút bần + váy hình nón cụt) ---
            var shuttleGO = new GameObject("Shuttlecock");
            shuttleGO.transform.position = new Vector3(0f, 1.1f, -3f);
            var shuttle = shuttleGO.AddComponent<ShuttlecockController>();
            BuildShuttleMesh(shuttleGO.transform);

            // --- Người chơi (humanoid được Build trong Init lúc chạy) ---
            var userGO = new GameObject("Player0_User");
            userGO.transform.position = new Vector3(0f, 0f, -3.0f);
            userGO.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            var user = userGO.AddComponent<PlayerController>();

            var aiGO = new GameObject("Player1_AI");
            aiGO.transform.position = new Vector3(0f, 0f, 3.0f);
            aiGO.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            var ai = aiGO.AddComponent<PlayerController>();

            // --- Model CC0 (Quaternius, CC0, bản Superhero) ---
            // Gắn CC0CharacterRig lên 2 người chơi (user = nam, AI = nữ).
            // File FBX chưa có thì chỉ log cảnh báo, PlayerController.Init
            // fallback humanoid procedural — build không dừng vì thiếu model.
            AttachCC0Model(userGO, useFemale: false);
            AttachCC0Model(aiGO, useFemale: true);

            // --- MatchManager (nối sẵn Shuttle + Players) ---
            var matchGO = new GameObject("MatchManager");
            var match = matchGO.AddComponent<MatchManager>();
            match.Shuttle = shuttle;
            match.Players = new PlayerController[2];
            match.Players[0] = user;
            match.Players[1] = ai;

            // --- GameBootstrap: wiring duy nhất của scene Match ---
            var bootstrapGO = new GameObject("GameBootstrap");
            bootstrapGO.AddComponent<GameBootstrap>();

            // --- Camera + CameraManager (LateUpdate tự đặt vị trí) ---
            var camGO = new GameObject("CameraRig");
            camGO.tag = "MainCamera"; // để Camera.main fallback trong CameraManager có tác dụng
            var cam = camGO.AddComponent<UnityEngine.Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            camGO.AddComponent<AudioListener>();
            camGO.AddComponent<CameraManager>();
            camGO.transform.position = new Vector3(0f, 6.5f, -10.5f);
            camGO.transform.LookAt(new Vector3(0f, 1f, 2f));

            // --- HUD (tự dựng Canvas + UI bằng code trong Awake) ---
            // BẮT BUỘC tên "HUDManager": GameBootstrap SendMessage("BindMatch").
            var hudGO = new GameObject("HUDManager");
            hudGO.AddComponent<HUDManager>();

            // --- Input cảm ứng (panels theo đúng tên MobileInputManager tìm) ---
            var inputGO = new GameObject("MobileInput");
            var input = inputGO.AddComponent<MobileInputManager>();
            BuildInputPanels(inputGO.transform, input);

            // --- Âm thanh tổng hợp bằng code ---
            var audioGO = new GameObject("AudioManager");
            audioGO.AddComponent<AudioManager>();

            UIBuilder.EnsureEventSystem();
            SaveScene(scene, MatchScenePath);
        }

        /// <summary>
        /// Gắn CC0CharacterRig + model FBX vào một Player GameObject.
        /// Model cần dùng chưa convert / thiếu file thì chỉ log cảnh báo và
        /// bỏ qua — PlayerController.Init sẽ dùng humanoid procedural.
        /// </summary>
        private static void AttachCC0Model(GameObject playerGO, bool useFemale)
        {
            var male = AssetDatabase.LoadAssetAtPath<GameObject>(Cc0MaleModelPath);
            var female = AssetDatabase.LoadAssetAtPath<GameObject>(Cc0FemaleModelPath);
            GameObject wanted = useFemale ? female : male;
            if (wanted == null)
            {
                Debug.LogWarning("[SceneBuilder] Không thấy model CC0: "
                    + (useFemale ? Cc0FemaleModelPath : Cc0MaleModelPath)
                    + " — player " + playerGO.name + " dùng humanoid procedural. "
                    + "Convert GLB -> FBX bằng tools/glb_to_fbx.py rồi chạy lại menu.");
                return;
            }
            var rig = playerGO.AddComponent<CC0CharacterRig>();
            rig.MaleModel = male;
            rig.FemaleModel = female;
            rig.UseFemaleModel = useFemale;
        }

        // --- Sân --------------------------------------------------------------

        private static void BuildCourt()
        {
            var courtRoot = new GameObject("Court");

            // Mặt sân (đỉnh y = 0): v0.2 phase 1 — sân đấu XANH kiểu truyền
            // hình (bóng mờ semi-gloss, smoothness 0.6), viền ngoài XANH LÁ.
            AddBox(courtRoot.transform, "CourtSurface",
                new Vector3(0f, -0.05f, 0f), new Vector3(8f, 0.1f, 15f),
                GetMaterialSmooth("CourtBlue", CourtBlue, 0.6f));
            AddBox(courtRoot.transform, "SurroundFloor",
                new Vector3(0f, -0.06f, 0f), new Vector3(26f, 0.08f, 32f),
                GetMaterial("CourtGreen", CourtGreen));
            // Sàn tối toàn nhà thi đấu (đỉnh thấp hơn sàn xanh lá 2 cm,
            // tránh z-fighting — sàn xanh là tấm thảm nổi trên sàn tối).
            AddBox(courtRoot.transform, "ArenaFloor",
                new Vector3(0f, -0.08f, 0f), new Vector3(44f, 0.08f, 40f),
                GetMaterial("FloorDark", FloorDark));

            // Vạch trắng (hộp mỏng, đỉnh y = 0.026 — tránh z-fighting).
            Material line = GetMaterial("LineWhite", LineWhite);
            float lw = CourtDimensions.LineWidth;
            float hwD = CourtDimensions.DoublesHalfWidth;   // 3.05
            float hwS = CourtDimensions.SinglesHalfWidth;   // 2.59
            float hl = CourtDimensions.CourtHalfLength;     // 6.70
            float ssl = CourtDimensions.ShortServiceDistance; // 1.98
            float dsl = CourtDimensions.DoublesLongServiceDistance; // 5.94

            // Baseline (z = ±6.70), dài theo biên đôi.
            AddLine(courtRoot.transform, "BaselineNear", new Vector3(0f, 0f, -hl),
                new Vector3(hwD * 2f + lw, lw, lw), line);
            AddLine(courtRoot.transform, "BaselineFar", new Vector3(0f, 0f, hl),
                new Vector3(hwD * 2f + lw, lw, lw), line);
            // Biên dọc đôi (x = ±3.05).
            AddLine(courtRoot.transform, "DoublesSidelineL", new Vector3(-hwD, 0f, 0f),
                new Vector3(lw, lw, hl * 2f + lw), line);
            AddLine(courtRoot.transform, "DoublesSidelineR", new Vector3(hwD, 0f, 0f),
                new Vector3(lw, lw, hl * 2f + lw), line);
            // Biên dọc đơn (x = ±2.59).
            AddLine(courtRoot.transform, "SinglesSidelineL", new Vector3(-hwS, 0f, 0f),
                new Vector3(lw, lw, hl * 2f + lw), line);
            AddLine(courtRoot.transform, "SinglesSidelineR", new Vector3(hwS, 0f, 0f),
                new Vector3(lw, lw, hl * 2f + lw), line);
            // Vạch giao cầu ngắn (z = ±1.98), trong biên đơn.
            AddLine(courtRoot.transform, "ShortServiceNear", new Vector3(0f, 0f, -ssl),
                new Vector3(hwS * 2f + lw, lw, lw), line);
            AddLine(courtRoot.transform, "ShortServiceFar", new Vector3(0f, 0f, ssl),
                new Vector3(hwS * 2f + lw, lw, lw), line);
            // Vạch giữa ô giao cầu (x = 0, nối 2 vạch ngắn).
            AddLine(courtRoot.transform, "CentreServiceLine", Vector3.zero,
                new Vector3(lw, lw, ssl * 2f + lw), line);
            // Vạch giao cầu dài đôi (z = ±5.94).
            AddLine(courtRoot.transform, "DoublesLongServiceNear", new Vector3(0f, 0f, -dsl),
                new Vector3(hwD * 2f + lw, lw, lw), line);
            AddLine(courtRoot.transform, "DoublesLongServiceFar", new Vector3(0f, 0f, dsl),
                new Vector3(hwD * 2f + lw, lw, lw), line);

            // --- Lưới: 2 cột kim loại tối + mặt lưới trong suốt + băng trắng trên ---
            Material post = GetMaterialSmooth("PostMetal", PostDark, 0.4f, 0.8f);
            AddCylinder(courtRoot.transform, "NetPostL",
                new Vector3(-hwD, CourtDimensions.PostHeight * 0.5f, 0f),
                0.03f, CourtDimensions.PostHeight, post);
            AddCylinder(courtRoot.transform, "NetPostR",
                new Vector3(hwD, CourtDimensions.PostHeight * 0.5f, 0f),
                0.03f, CourtDimensions.PostHeight, post);

            var netGO = new GameObject("Net");
            netGO.transform.SetParent(courtRoot.transform, false);
            netGO.transform.localPosition = new Vector3(0f, CourtDimensions.NetHeightCenter * 0.5f, 0f);
            var netFilter = netGO.AddComponent<MeshFilter>();
            netFilter.mesh = CreateQuadMesh(hwD * 2f, CourtDimensions.NetHeightCenter);
            var netRenderer = netGO.AddComponent<MeshRenderer>();
            netRenderer.material = GetNetMaterial();

            // Băng trắng trên cùng của lưới (cao 1.524 m ở giữa).
            AddBox(courtRoot.transform, "NetTopTape",
                new Vector3(0f, CourtDimensions.NetHeightCenter - 0.025f, 0f),
                new Vector3(hwD * 2f, 0.05f, 0.04f), line);
        }

        // --- Khán đài + biển quảng cáo (hư cấu) -------------------------------

        // --- Nhà thi đấu tối (v0.2 phase 1) ----------------------------------
        // Khán đài bậc thang 3 tầng mỗi phía (tầng cao 1,5 m, sâu 1,9 m) để
        // đặt khán giả (xem BuildCrowd); tường + trần tối bao quanh; dải LED
        // phát sáng dọc mép trước tầng 1 và tầng 3. Giữ biển quảng cáo hư cấu.
        private const float StandTierHeight = 1.5f;
        private const float StandTierDepth = 1.9f;
        private const float StandLength = 26f;
        private const float StandFrontWest = 7.0f;    // mép trước tầng 0 phía Tây (|x|)
        private const float StandFrontNorth = 10.5f;  // mép trước tầng 0 phía Bắc (z)

        private static void BuildStandsAndBoards()
        {
            var root = new GameObject("Arena");
            Material stand = GetMaterialSmooth("StandTier", StandDark, 0.3f);

            // 3 tầng mỗi phía — bậc thang lùi dần ra ngoài và cao dần.
            for (int t = 0; t < 3; t++)
            {
                float y = StandTierHeight * (t + 0.5f);
                float frontW = StandFrontWest + t * StandTierDepth;
                float frontN = StandFrontNorth + t * StandTierDepth;
                AddBox(root.transform, "StandWest_Tier" + t,
                    new Vector3(-(frontW + StandTierDepth * 0.5f), y, 0f),
                    new Vector3(StandTierDepth, StandTierHeight, StandLength), stand)
                    .isStatic = true;
                AddBox(root.transform, "StandEast_Tier" + t,
                    new Vector3(frontW + StandTierDepth * 0.5f, y, 0f),
                    new Vector3(StandTierDepth, StandTierHeight, StandLength), stand)
                    .isStatic = true;
                AddBox(root.transform, "StandNorth_Tier" + t,
                    new Vector3(0f, y, frontN + StandTierDepth * 0.5f),
                    new Vector3(StandLength, StandTierHeight, StandTierDepth), stand)
                    .isStatic = true;
                AddBox(root.transform, "StandSouth_Tier" + t,
                    new Vector3(0f, y, -(frontN + StandTierDepth * 0.5f)),
                    new Vector3(StandLength, StandTierHeight, StandTierDepth), stand)
                    .isStatic = true;
            }

            // Tường + trần tối bao quanh nhà thi đấu (trần cao 12 m, chuẩn BWF).
            Material wall = GetMaterialSmooth("ArenaWall", ArenaWall, 0.25f);
            AddBox(root.transform, "ArenaWallWest", new Vector3(-14.5f, 6f, 0f),
                new Vector3(1f, 12f, 38f), wall).isStatic = true;
            AddBox(root.transform, "ArenaWallEast", new Vector3(14.5f, 6f, 0f),
                new Vector3(1f, 12f, 38f), wall).isStatic = true;
            AddBox(root.transform, "ArenaWallNorth", new Vector3(0f, 6f, 18.5f),
                new Vector3(30f, 12f, 1f), wall).isStatic = true;
            AddBox(root.transform, "ArenaWallSouth", new Vector3(0f, 6f, -18.5f),
                new Vector3(30f, 12f, 1f), wall).isStatic = true;
            AddBox(root.transform, "ArenaCeiling", new Vector3(0f, 12.25f, 0f),
                new Vector3(31f, 0.5f, 39f), wall).isStatic = true;

            // Dải LED phát sáng dọc mép trước tầng 1 (y=1,57) và tầng 3 (y=4,57).
            // (Nhấc khỏi mặt tầng 1 cm để không z-fight với mặt tầng.)
            Material led = GetEmissiveMaterial("LedStrip",
                new Color(0.05f, 0.08f, 0.10f), LedCyan, 2.0f);
            float stripY0 = StandTierHeight + 0.07f;       // 1,57
            float stripY2 = StandTierHeight * 3f + 0.07f;  // 4,57
            float frontW2 = StandFrontWest + 2f * StandTierDepth;   // 10,8
            float frontN2 = StandFrontNorth + 2f * StandTierDepth;  // 14,3
            AddBox(root.transform, "LedStripWest0", new Vector3(-StandFrontWest, stripY0, 0f),
                new Vector3(0.1f, 0.12f, StandLength), led).isStatic = true;
            AddBox(root.transform, "LedStripWest2", new Vector3(-frontW2, stripY2, 0f),
                new Vector3(0.1f, 0.12f, StandLength), led).isStatic = true;
            AddBox(root.transform, "LedStripEast0", new Vector3(StandFrontWest, stripY0, 0f),
                new Vector3(0.1f, 0.12f, StandLength), led).isStatic = true;
            AddBox(root.transform, "LedStripEast2", new Vector3(frontW2, stripY2, 0f),
                new Vector3(0.1f, 0.12f, StandLength), led).isStatic = true;
            AddBox(root.transform, "LedStripNorth0", new Vector3(0f, stripY0, StandFrontNorth),
                new Vector3(StandLength, 0.12f, 0.1f), led).isStatic = true;
            AddBox(root.transform, "LedStripNorth2", new Vector3(0f, stripY2, frontN2),
                new Vector3(StandLength, 0.12f, 0.1f), led).isStatic = true;
            AddBox(root.transform, "LedStripSouth0", new Vector3(0f, stripY0, -StandFrontNorth),
                new Vector3(StandLength, 0.12f, 0.1f), led).isStatic = true;
            AddBox(root.transform, "LedStripSouth2", new Vector3(0f, stripY2, -frontN2),
                new Vector3(StandLength, 0.12f, 0.1f), led).isStatic = true;

            // Biển quảng cáo dài 2 bên sân (chữ TextMesh, không đụng
            // thương hiệu / tên giải thật).
            AddAdBoard(root.transform, new Vector3(-4.6f, 0.5f, 0f),
                new Vector3(0.15f, 1f, 12f), GetMaterial("BoardGreen", BoardGreen),
                90f, new Vector3(-4.5f, 0.5f, 0f));
            AddAdBoard(root.transform, new Vector3(4.6f, 0.5f, 0f),
                new Vector3(0.15f, 1f, 12f), GetMaterial("BoardBlue", BoardBlue),
                -90f, new Vector3(4.5f, 0.5f, 0f));
        }

        /// <summary>
        /// Dàn đèn nhà thi đấu (v0.2 phase 1): 6 spotlight trên sân (5600K,
        /// 2 hàng × 3, cao 10 m) + hộp đèn nhỏ ở mỗi spot + 4 nón sáng giả
        /// (additive) dưới 4 spot góc. Chỉ 1 spot đổ bóng (spot giữa phía xa).
        /// Cường độ theo thang vật lý của URP (suy hao 1/d² — xem report §3):
        /// spot ở 10 m cần ~300 để sân đủ sáng sau ACES; directional chỉ fill.
        /// </summary>
        private static void BuildArenaLights()
        {
            var root = new GameObject("ArenaLights");
            Color spotColor = new Color(1f, 0.96f, 0.88f); // ~5600K
            Material fixtureMat = GetMaterialSmooth("ArenaWall", ArenaWall, 0.25f);

            float[] xs = { -4.5f, 0f, 4.5f };
            float[] zs = { -3.5f, 3.5f };
            foreach (float x in xs)
            {
                foreach (float z in zs)
                {
                    var spotPos = new Vector3(x, 10f, z);
                    var target = new Vector3(x, 0f, z > 0f ? 2f : -2f);
                    var go = new GameObject("Spot_" + x + "_" + z);
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = spotPos;
                    var light = go.AddComponent<Light>();
                    light.type = LightType.Spot;
                    light.color = spotColor;
                    light.intensity = 300f;
                    light.range = 22f;
                    light.spotAngle = 50f;
                    light.innerSpotAngle = 20f;
                    go.transform.LookAt(target);
                    // Spot giữa phía xa (0, 10, +3.5) là đèn đổ bóng duy nhất.
                    if (x == 0f && z > 0f)
                    {
                        light.shadows = LightShadows.Soft;
                        light.shadowCustomResolution = 1024;
                        light.shadowNearPlane = 0.5f;
                        light.shadowBias = 0.02f;
                        light.shadowNormalBias = 0.4f;
                    }
                    // Hộp đèn nhỏ để nón sáng không mọc ra từ khoảng không.
                    AddBox(root.transform, "SpotFixture_" + x + "_" + z,
                        spotPos, new Vector3(0.6f, 0.25f, 0.6f), fixtureMat)
                        .isStatic = true;
                }
            }

            // Nón sáng giả (fake volumetric): 4 nón additive dưới 4 spot góc.
            Material coneMat = GetConeMaterial();
            float[] coneXs = { -4.5f, 4.5f };
            foreach (float x in coneXs)
            {
                foreach (float z in zs)
                {
                    var spotPos = new Vector3(x, 10f, z);
                    var target = new Vector3(x, 0f, z > 0f ? 2f : -2f);
                    float length = Vector3.Distance(spotPos, target);
                    var coneGO = new GameObject("LightCone_" + x + "_" + z);
                    coneGO.transform.SetParent(root.transform, false);
                    coneGO.transform.position = spotPos;
                    coneGO.transform.rotation = Quaternion.LookRotation(spotPos - target);
                    var filter = coneGO.AddComponent<MeshFilter>();
                    filter.mesh = CreateConeMesh(0.35f, 2.8f, length, 16);
                    var renderer = coneGO.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = coneMat;
                }
            }
        }

        /// <summary>
        /// Khán giả Tier 1 (v0.2 phase 1): ~1000 người ngồi trên 3 tầng khán
        /// đài mỗi phía (2 hàng/tầng). Mỗi người là 1 GameObject dùng chung
        /// 1 trong 3 mesh dáng ngồi + 1 trong 3 material áo — sau đó gộp
        /// theo từng phía bằng StaticBatchingUtility.Combine (tối đa 3 draw
        /// call/phía, frustum culling theo phía). Random có seed cố định để
        /// mỗi lần build ra cùng một khán đài.
        /// </summary>
        private static void BuildCrowd()
        {
            var root = new GameObject("Crowd");
            var variants = new[]
            {
                BuildSpectatorMesh(0), // ngồi thường
                BuildSpectatorMesh(1), // giơ tay cổ vũ
                BuildSpectatorMesh(2), // nghiêng người
            };
            var shirts = new[]
            {
                GetMaterial("CrowdShirtRed", new Color(0.72f, 0.14f, 0.14f)),
                GetMaterial("CrowdShirtBlue", new Color(0.13f, 0.32f, 0.75f)),
                GetMaterial("CrowdShirtYellow", new Color(0.85f, 0.68f, 0.12f)),
            };
            var rng = new System.Random(20261110);
            int total = 0;

            // Mỗi phía là 1 chunk riêng (để Combine + frustum culling theo phía).
            total += BuildCrowdSide(root.transform, "CrowdWest", variants, shirts, rng,
                isWestEast: true, sign: -1f);
            total += BuildCrowdSide(root.transform, "CrowdEast", variants, shirts, rng,
                isWestEast: true, sign: 1f);
            total += BuildCrowdSide(root.transform, "CrowdNorth", variants, shirts, rng,
                isWestEast: false, sign: 1f);
            total += BuildCrowdSide(root.transform, "CrowdSouth", variants, shirts, rng,
                isWestEast: false, sign: -1f);

            Debug.Log("[SceneBuilder] Khán giả: " + total + " người, 4 chunk đã Combine.");
        }

        /// <summary>Một phía khán đài: 3 tầng × 2 hàng, mỗi hàng 42 người.</summary>
        private static int BuildCrowdSide(Transform parent, string chunkName,
            Mesh[] variants, Material[] shirts, System.Random rng,
            bool isWestEast, float sign)
        {
            var chunk = new GameObject(chunkName);
            chunk.transform.SetParent(parent, false);
            int count = 0;
            // Hướng nhìn về sân: Tây +X (yaw 90), Đông -X (yaw -90),
            // Bắc -Z (yaw 180), Nam +Z (yaw 0).
            float baseYaw = isWestEast ? (sign < 0f ? 90f : -90f) : (sign > 0f ? 180f : 0f);
            for (int t = 0; t < 3; t++)
            {
                float tierTopY = StandTierHeight * (t + 1);
                float front = (isWestEast ? StandFrontWest : StandFrontNorth) + t * StandTierDepth;
                for (int row = 0; row < 2; row++)
                {
                    float rowOffset = 0.45f + row * 0.9f; // 0,45 và 1,35 từ mép trước
                    for (int i = 0; i < 42; i++)
                    {
                        float along = -10.5f + i * 0.5f + 0.25f; // -10,25 .. +10,25
                        Vector3 pos;
                        if (isWestEast)
                            pos = new Vector3(sign * (front + rowOffset), tierTopY, along);
                        else
                            pos = new Vector3(along, tierTopY, sign * (front + rowOffset));
                        var go = new GameObject("Spectator");
                        go.transform.SetParent(chunk.transform, false);
                        go.transform.localPosition = pos;
                        go.transform.localRotation = Quaternion.Euler(0f,
                            baseYaw + (float)(rng.NextDouble() * 24.0 - 12.0), 0f);
                        float scale = 0.92f + (float)rng.NextDouble() * 0.16f;
                        go.transform.localScale = Vector3.one * scale;
                        var filter = go.AddComponent<MeshFilter>();
                        int variant = rng.Next(100) < 70 ? 0 : (rng.Next(2) == 0 ? 1 : 2);
                        filter.sharedMesh = variants[variant];
                        var renderer = go.AddComponent<MeshRenderer>();
                        renderer.sharedMaterial = shirts[rng.Next(shirts.Length)];
                        go.isStatic = true;
                        count++;
                    }
                }
            }
            StaticBatchingUtility.Combine(chunk);
            return count;
        }

        // --- Khán giả: mesh dáng ngồi -------------------------------------------

        private static Vector3[] _cubeVerts;
        private static Vector3[] _cubeNormals;
        private static int[] _cubeTris;

        /// <summary>Lấy mảng đỉnh/mặt của khối lập phương đơn vị (dùng chung).</summary>
        private static void EnsureCubeCache()
        {
            if (_cubeVerts != null) return;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            _cubeVerts = mesh.vertices;
            _cubeNormals = mesh.normals;
            _cubeTris = mesh.triangles;
            Object.DestroyImmediate(tmp);
        }

        /// <summary>Thêm một hộp (tâm/kích thước/xoay) vào mesh đang dựng.</summary>
        private static void AppendBox(System.Collections.Generic.List<Vector3> verts,
            System.Collections.Generic.List<Vector3> normals,
            System.Collections.Generic.List<int> tris,
            Vector3 center, Vector3 size, Quaternion rotation)
        {
            EnsureCubeCache();
            var matrix = Matrix4x4.TRS(center, rotation, size);
            var normalMatrix = matrix.inverse.transpose;
            int baseIndex = verts.Count;
            for (int i = 0; i < _cubeVerts.Length; i++)
            {
                verts.Add(matrix.MultiplyPoint3x4(_cubeVerts[i]));
                normals.Add(normalMatrix.MultiplyVector(_cubeNormals[i]).normalized);
            }
            for (int i = 0; i < _cubeTris.Length; i++)
                tris.Add(baseIndex + _cubeTris[i]);
        }

        /// <summary>
        /// Mesh khán giả ngồi (gốc tại mặt ghế, mặt hướng +Z, cao ~0,9 m):
        /// thân + đầu + 2 đùi + 2 tay. 3 dáng: 0 ngồi thường, 1 giơ tay cổ vũ,
        /// 2 nghiêng người. ~72 tris/người (6 hộp × 12 tris).
        /// </summary>
        private static Mesh BuildSpectatorMesh(int variant)
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            Quaternion lean = variant == 2 ? Quaternion.Euler(10f, 0f, 0f) : Quaternion.identity;
            float leanZ = variant == 2 ? 0.06f : 0f;
            float leanHeadZ = variant == 2 ? 0.10f : 0f;

            // Thân + đầu.
            AppendBox(verts, normals, tris, new Vector3(0f, 0.36f, leanZ),
                new Vector3(0.46f, 0.58f, 0.26f), lean);
            AppendBox(verts, normals, tris, new Vector3(0f, 0.78f, 0.01f + leanHeadZ),
                new Vector3(0.23f, 0.23f, 0.23f), lean);
            // Đùi (ngồi, hướng về trước).
            AppendBox(verts, normals, tris, new Vector3(-0.11f, 0.14f, 0.20f),
                new Vector3(0.15f, 0.15f, 0.42f), Quaternion.identity);
            AppendBox(verts, normals, tris, new Vector3(0.11f, 0.14f, 0.20f),
                new Vector3(0.15f, 0.15f, 0.42f), Quaternion.identity);
            // Tay: dáng 1 giơ cao cổ vũ, còn lại buông 2 bên.
            if (variant == 1)
            {
                AppendBox(verts, normals, tris, new Vector3(-0.30f, 0.72f, 0f),
                    new Vector3(0.11f, 0.5f, 0.11f), Quaternion.Euler(0f, 0f, 18f));
                AppendBox(verts, normals, tris, new Vector3(0.30f, 0.72f, 0f),
                    new Vector3(0.11f, 0.5f, 0.11f), Quaternion.Euler(0f, 0f, -18f));
            }
            else
            {
                AppendBox(verts, normals, tris, new Vector3(-0.29f, 0.36f, 0f),
                    new Vector3(0.11f, 0.48f, 0.11f), Quaternion.Euler(0f, 0f, 6f));
                AppendBox(verts, normals, tris, new Vector3(0.29f, 0.36f, 0f),
                    new Vector3(0.11f, 0.48f, 0.11f), Quaternion.Euler(0f, 0f, -6f));
            }

            var mesh = new Mesh { name = "SpectatorMesh_" + variant };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Nón sáng (mặt bên hở, không nắp): miệng trên bán kính nhỏ tại z=0,
        /// miệng dưới bán kính lớn tại z=-length. Dùng với material additive
        /// (Cull Off) — trục chùm sáng là -Z, xoay bằng LookRotation(hướng).
        /// </summary>
        private static Mesh CreateConeMesh(float topRadius, float bottomRadius,
            float length, int segments)
        {
            var mesh = new Mesh { name = "LightCone" };
            var verts = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var tris = new System.Collections.Generic.List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                verts.Add(new Vector3(c * topRadius, s * topRadius, 0f));
                verts.Add(new Vector3(c * bottomRadius, s * bottomRadius, -length));
                uvs.Add(new Vector2((float)i / segments, 1f));
                uvs.Add(new Vector2((float)i / segments, 0f));
            }
            for (int i = 0; i < segments; i++)
            {
                int a0 = i * 2, b0 = i * 2 + 1, a1 = i * 2 + 2, b1 = i * 2 + 3;
                tris.Add(a0); tris.Add(b0); tris.Add(a1);
                tris.Add(b0); tris.Add(b1); tris.Add(a1);
            }
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // --- Input panels -------------------------------------------------------

        /// <summary>
        /// Canvas input + JoystickPanel (trái dưới) + ShotButtonsPanel
        /// (phải dưới). Tên GameObject phải khớp MobileInputManager tìm
        /// ("JoystickPanel" / "ShotButtonsPanel") để đảo tay trái/phải.
        /// ShotButton dùng IPointer handlers — KHÔNG gán Button.onClick.
        /// </summary>
        private static void BuildInputPanels(Transform parent, MobileInputManager input)
        {
            var canvas = UIBuilder.CreateCanvas("InputCanvas", 5); // dưới HUD (10)
            canvas.transform.SetParent(parent, false);
            Transform root = canvas.transform;

            // --- Joystick (trái dưới, 260x260) ---
            var joyPanel = UIBuilder.CreatePanel(root, "JoystickPanel",
                Vector2.zero, Vector2.zero,
                new Vector2(160f, 160f), new Vector2(260f, 260f),
                new Color(0f, 0f, 0f, 0.35f));
            var knobGO = new GameObject("Knob");
            knobGO.transform.SetParent(joyPanel, false);
            var knobImage = knobGO.AddComponent<UnityEngine.UI.Image>();
            knobImage.color = new Color(1f, 1f, 1f, 0.55f);
            var knobRt = knobGO.GetComponent<RectTransform>();
            knobRt.anchorMin = new Vector2(0.5f, 0.5f);
            knobRt.anchorMax = new Vector2(0.5f, 0.5f);
            knobRt.anchoredPosition = Vector2.zero;
            knobRt.sizeDelta = new Vector2(110f, 110f);
            var joystick = joyPanel.gameObject.AddComponent<VirtualJoystick>();
            joystick.knob = knobRt;
            joystick.inputManager = input;
            input.JoystickPanel = joyPanel;

            // --- Nút kỹ thuật (phải dưới, 360x300) ---
            var btnPanel = UIBuilder.CreatePanel(root, "ShotButtonsPanel",
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-200f, 160f), new Vector2(360f, 300f),
                new Color(0f, 0f, 0f, 0.30f));
            input.ShotButtonsPanel = btnPanel;

            // 6 nút kỹ thuật (lưới 3x2) + nút giao cầu riêng bên dưới.
            AddShotButton(btnPanel, "ShotClear", "Phông", ShotType.Clear,
                new Vector2(-116f, 70f), input);
            AddShotButton(btnPanel, "ShotDrop", "Bỏ nhỏ", ShotType.Drop,
                new Vector2(0f, 70f), input);
            AddShotButton(btnPanel, "ShotSmash", "Đập", ShotType.Smash,
                new Vector2(116f, 70f), input);
            AddShotButton(btnPanel, "ShotDrive", "Tạt", ShotType.Drive,
                new Vector2(-116f, -10f), input);
            AddShotButton(btnPanel, "ShotLift", "Bổng", ShotType.Lift,
                new Vector2(0f, -10f), input);
            AddShotButton(btnPanel, "ShotNet", "Lưới", ShotType.NetShot,
                new Vector2(116f, -10f), input);
            AddShotButton(btnPanel, "ServeButton", "GIAO CẦU", ShotType.ServeLow,
                new Vector2(0f, -100f), input, isServe: true, size: new Vector2(220f, 56f));
        }

        private static void AddShotButton(Transform parent, string name, string label,
            ShotType type, Vector2 anchoredPos, MobileInputManager input,
            bool isServe = false, Vector2 size = default)
        {
            if (size == default) size = new Vector2(104f, 64f);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<UnityEngine.UI.Image>();
            image.color = UIBuilder.ButtonNormal;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            UIBuilder.CreateText(go.transform, "Label", label, 22,
                UIBuilder.TextWhite, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var shot = go.AddComponent<ShotButton>();
            shot.ShotType = type;
            shot.IsServeButton = isServe;
            shot.inputManager = input;
        }

        // --- Cầu (mesh) ---------------------------------------------------------

        /// <summary>
        /// Cầu: nút bần (sphere) + váy hình nón cụt (mesh tự tạo).
        /// Hướng +Z là hướng bay (nút bần đi trước — ShuttlecockController
        /// xoay transform theo vận tốc bằng LookRotation).
        /// </summary>
        private static void BuildShuttleMesh(Transform parent)
        {
            Material mat = GetMaterial("ShuttleWhite", ShuttleWhite);

            var cork = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cork.name = "Cork";
            cork.transform.SetParent(parent, false);
            cork.transform.localPosition = new Vector3(0f, 0f, 0.03f);
            cork.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            cork.GetComponent<MeshRenderer>().material = mat;
            Object.DestroyImmediate(cork.GetComponent<Collider>());

            var skirtGO = new GameObject("Skirt");
            skirtGO.transform.SetParent(parent, false);
            var filter = skirtGO.AddComponent<MeshFilter>();
            filter.mesh = CreateFrustumMesh(0.012f, 0.035f, 0.01f, -0.06f, 10);
            var renderer = skirtGO.AddComponent<MeshRenderer>();
            renderer.material = mat;
        }

        // --- Helpers hình học ---------------------------------------------------

        private static GameObject AddBox(Transform parent, string name,
            Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().material = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject AddLine(Transform parent, string name,
            Vector3 pos, Vector3 size, Material mat)
        {
            // Vạch nằm trên mặt sân: hộp mỏng, đỉnh y ≈ 0.012.
            return AddBox(parent, name, new Vector3(pos.x, 0.006f, pos.z), size, mat);
        }

        private static GameObject AddCylinder(Transform parent, string name,
            Vector3 pos, float radius, float height, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            // Cylinder gốc: cao 2, bán kính 0.5.
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<MeshRenderer>().material = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static void AddAdBoard(Transform parent, Vector3 boardPos,
            Vector3 boardSize, Material mat, float textRotY, Vector3 textPos)
        {
            AddBox(parent, "AdBoard", boardPos, boardSize, mat);
            var textGO = new GameObject("AdBoardText");
            textGO.transform.SetParent(parent, false);
            textGO.transform.localPosition = textPos;
            textGO.transform.localRotation = Quaternion.Euler(0f, textRotY, 0f);
            var textMesh = textGO.AddComponent<TextMesh>();
            textMesh.text = AdBrandText;
            textMesh.fontSize = 64;
            textMesh.characterSize = 0.12f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = Color.white;
        }

        private static UnityEngine.Camera CreateCamera(string name, Color bg,
            Vector3 pos, Vector3 lookAt)
        {
            var go = new GameObject(name);
            var cam = go.AddComponent<UnityEngine.Camera>();
            // Menu/Career cần AudioListener để nghe nhạc + tiếng click nút
            // (Match dùng CameraRig riêng nên không trùng listener).
            go.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            go.transform.position = pos;
            go.transform.LookAt(lookAt);
            return cam;
        }

        private static void SetupSceneLighting(Color bgColor)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.35f, 0.38f);
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            // Màu nền đã đặt trên camera (CreateCamera: SolidColor +
            // backgroundColor) — RenderSettings không có backgroundColor.
        }

        private static void SaveScene(Scene scene, string path)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            bool ok = EditorSceneManager.SaveScene(scene, path);
            if (!ok) throw new System.Exception("[SceneBuilder] Không lưu được scene: " + path);
            Debug.Log("[SceneBuilder] Đã lưu scene: " + path);
        }

        private static void RegisterBuildScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(MatchScenePath, true),
                new EditorBuildSettingsScene(CareerScenePath, true),
            };
            Debug.Log("[SceneBuilder] Build Settings: 0=MainMenu, 1=Match, 2=Career.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets", "Settings");
            EnsureFolder("Assets", "Materials");
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }

        // --- Materials ----------------------------------------------------------

        /// <summary>Tái sử dụng material đã có, chưa có thì tạo asset mới.</summary>
        private static Material GetMaterial(string name, Color color)
        {
            string path = MaterialsDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new System.Exception("[SceneBuilder] Không tìm thấy shader URP/Lit lẫn Standard.");
            mat = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>
        /// URP/Lit có smoothness/metallic chỉ định (tái sử dụng asset đã có
        /// thì cập nhật lại smoothness/metallic cho khớp).
        /// </summary>
        private static Material GetMaterialSmooth(string name, Color color,
            float smoothness, float metallic = 0f)
        {
            string path = MaterialsDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null) throw new System.Exception("[SceneBuilder] Không tìm thấy shader URP/Lit lẫn Standard.");
                mat = new Material(shader) { color = color };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            return mat;
        }

        /// <summary>URP/Lit phát sáng (emission HDR &gt; 1 để làm nguồn bloom sau này).</summary>
        private static Material GetEmissiveMaterial(string name, Color baseColor,
            Color emissionColor, float emissionIntensity)
        {
            string path = MaterialsDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null) throw new System.Exception("[SceneBuilder] Không tìm thấy shader URP/Lit lẫn Standard.");
                mat = new Material(shader) { color = baseColor };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_EmissionColor", emissionColor * emissionIntensity);
            mat.EnableKeyword("_EMISSION");
            return mat;
        }

        /// <summary>
        /// Nón sáng giả: URP/Unlit, additive (One, One), không ghi depth,
        /// không cull, render queue Transparent. Màu đã nhân nhỏ sẵn
        /// (additive cộng trực tiếp vào màu nền nên màu phải rất mờ).
        /// </summary>
        private static Material GetConeMaterial()
        {
            string path = MaterialsDir + "/LightCone.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new System.Exception("[SceneBuilder] Không tìm thấy shader URP/Unlit.");
            mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(1f, 0.96f, 0.88f) * 0.055f);
            mat.SetFloat("_Surface", 1f);
            mat.SetInt("_SrcBlend", (int)BlendMode.One);
            mat.SetInt("_DstBlend", (int)BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.SetInt("_Cull", (int)CullMode.Off);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>Lưới trong suốt (URP/Lit, surface = Transparent).</summary>
        private static Material GetNetMaterial()
        {
            string path = MaterialsDir + "/NetWhite.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new System.Exception("[SceneBuilder] Không tìm thấy shader URP/Lit lẫn Standard.");
            mat = new Material(shader)
            {
                color = new Color(0.92f, 0.92f, 0.90f, 0.45f)
            };
            mat.SetFloat("_Surface", 1f);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // --- Mesh tự tạo ---------------------------------------------------------

        /// <summary>Mặt phẳng 2 mặt (width x height) trong mặt XY, tâm ở gốc.</summary>
        private static Mesh CreateQuadMesh(float width, float height)
        {
            var mesh = new Mesh { name = "NetQuad" };
            float hw = width * 0.5f, hh = height * 0.5f;
            mesh.vertices = new[]
            {
                new Vector3(-hw, -hh, 0f), new Vector3(hw, -hh, 0f),
                new Vector3(hw, hh, 0f), new Vector3(-hw, hh, 0f),
                new Vector3(-hw, -hh, 0f), new Vector3(-hw, hh, 0f),
                new Vector3(hw, hh, 0f), new Vector3(hw, -hh, 0f),
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 4, 6, 5, 4, 7, 6 };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 0f),
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Hình nón cụt rỗng (váy cầu): vòng trước bán kính r0 tại z0, vòng
        /// sau bán kính r1 tại z1, cộng nắp đĩa ở vòng sau để không lộ ruột.
        /// </summary>
        private static Mesh CreateFrustumMesh(float r0, float r1, float z0, float z1, int segments)
        {
            var mesh = new Mesh { name = "ShuttleSkirt" };
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();

            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                verts.Add(new Vector3(c * r0, s * r0, z0));
                verts.Add(new Vector3(c * r1, s * r1, z1));
            }
            for (int i = 0; i < segments; i++)
            {
                int a0 = i * 2, b0 = i * 2 + 1, a1 = i * 2 + 2, b1 = i * 2 + 3;
                tris.Add(a0); tris.Add(b0); tris.Add(a1);
                tris.Add(b0); tris.Add(b1); tris.Add(a1);
            }
            // Nắp đĩa sau (tâm + vành).
            int center = verts.Count;
            verts.Add(new Vector3(0f, 0f, z1));
            for (int i = 0; i < segments; i++)
            {
                tris.Add(center);
                tris.Add(i * 2 + 3);
                tris.Add(i * 2 + 1);
            }
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
