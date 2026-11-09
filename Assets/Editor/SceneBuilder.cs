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
//    MobileInput (+ JoystickPanel / ShotButtonsPanel), AudioManager,
//    DirectionalLight + ambient, khán đài + biển quảng cáo HƯ CẤU.
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

        // --- Màu ------------------------------------------------------------
        private static readonly Color CourtGreen = new Color(0.12f, 0.45f, 0.25f);
        private static readonly Color FloorDark = new Color(0.05f, 0.08f, 0.14f);
        private static readonly Color LineWhite = new Color(0.95f, 0.95f, 0.92f);
        private static readonly Color PostDark = new Color(0.10f, 0.10f, 0.12f);
        private static readonly Color StandDark = new Color(0.07f, 0.08f, 0.11f);
        private static readonly Color BoardGreen = new Color(0.05f, 0.35f, 0.20f);
        private static readonly Color BoardBlue = new Color(0.08f, 0.20f, 0.45f);
        private static readonly Color ShuttleWhite = new Color(0.96f, 0.96f, 0.92f);

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
        public static void EnsureUrpAsset()
        {
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (urp == null)
            {
                urp = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
                urp.renderScale = 1f;
                urp.msaaSampleCount = 4;
                urp.shadowDistance = 50f;
                AssetDatabase.CreateAsset(urp, UrpAssetPath);
                Debug.Log("[SceneBuilder] Đã tạo URP asset: " + UrpAssetPath);
            }
            GraphicsSettings.renderPipelineAsset = urp;
            QualitySettings.renderPipelineAsset = urp;
        }

        // --- MainMenu ---------------------------------------------------------

        public static void BuildMainMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyGameObjects, NewSceneMode.Single);
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
                NewSceneSetup.EmptyGameObjects, NewSceneMode.Single);
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
                NewSceneSetup.EmptyGameObjects, NewSceneMode.Single);
            SetupSceneLighting(new Color(0.02f, 0.03f, 0.05f));

            BuildCourt();
            BuildStandsAndBoards();

            // --- Đèn ---
            var sun = new GameObject("DirectionalLight");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.98f, 0.92f);
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

            // Mặt sân (đỉnh y = 0) + sàn xung quanh (thấp hơn 2 cm).
            AddBox(courtRoot.transform, "CourtSurface",
                new Vector3(0f, -0.05f, 0f), new Vector3(8f, 0.1f, 15f),
                GetMaterial("CourtGreen", CourtGreen));
            AddBox(courtRoot.transform, "SurroundFloor",
                new Vector3(0f, -0.06f, 0f), new Vector3(26f, 0.08f, 32f),
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

            // --- Lưới: 2 cột + mặt lưới trong suốt + băng trắng trên ---
            Material post = GetMaterial("PostDark", PostDark);
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

        private static void BuildStandsAndBoards()
        {
            var root = new GameObject("Arena");
            Material stand = GetMaterial("StandDark", StandDark);

            // 4 khối khán đài tối xung quanh sân.
            AddBox(root.transform, "StandWest", new Vector3(-9.5f, 2f, 0f),
                new Vector3(3f, 4f, 22f), stand);
            AddBox(root.transform, "StandEast", new Vector3(9.5f, 2f, 0f),
                new Vector3(3f, 4f, 22f), stand);
            AddBox(root.transform, "StandNorth", new Vector3(0f, 2f, 12f),
                new Vector3(22f, 4f, 3f), stand);
            AddBox(root.transform, "StandSouth", new Vector3(0f, 2f, -12f),
                new Vector3(22f, 4f, 3f), stand);

            // Biển quảng cáo dài 2 bên sân (chữ TextMesh, không đụng
            // thương hiệu / tên giải thật).
            AddAdBoard(root.transform, new Vector3(-4.6f, 0.5f, 0f),
                new Vector3(0.15f, 1f, 12f), GetMaterial("BoardGreen", BoardGreen),
                90f, new Vector3(-4.5f, 0.5f, 0f));
            AddAdBoard(root.transform, new Vector3(4.6f, 0.5f, 0f),
                new Vector3(0.15f, 1f, 12f), GetMaterial("BoardBlue", BoardBlue),
                -90f, new Vector3(4.5f, 0.5f, 0f));
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
            var canvas = UIBuilder.CreateCanvas("InputCanvas");
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
            var go = GameObject.CreatePrimitive(PrimitiveType.Box);
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
            // Màu nền dự phòng nếu camera không che hết khung hình.
            RenderSettings.backgroundColor = bgColor;
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
            mat = new Material(shader) { color = color };
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
