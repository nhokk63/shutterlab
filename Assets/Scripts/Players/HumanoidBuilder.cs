// HumanoidBuilder.cs — ShuttleLab (Unity 6, URP)
// Builds a ~1.75 m badminton player from Unity primitives (capsule /
// cylinder / sphere / box) with anatomically plausible proportions — NOT a
// single capsule. Hierarchy is a real joint rig (hips -> spine -> chest ->
// neck -> head, shoulders -> elbows -> wrists, hips -> knees -> ankles) so
// PlayerAnimationController can pose it procedurally.
// The racket (handle cylinder + torus head frame + string disc) is held in
// the dominant hand (LeftHanded => left wrist; right hand by default).
// RacketHead is an empty Transform at the centre of the string bed, used by
// RacketController as the contact point.
// Materials try "Universal Render Pipeline/Lit" first, falling back to
// "Standard" (headless / non-URP contexts).
//
// PROPORTION / KIT REFERENCE (read-only, no assets copied):
//   ~/workspace/shuttlelab/refs/*.png (Pure Badminton press kit) were used
//   only to calibrate proportions and kit: ~1.75 m tall, broad shoulders
//   with an athletic V-taper to narrower hips, long legs (hip height ~55%
//   of total height), polo shirt with collar + short sleeves, shorts ending
//   above the knee, white crew socks, white shoes with a dark sole, racket
//   in the right hand. From the behind-player camera the silhouette must
//   read as a badminton player — the light-shirt / dark-shorts contrast is
//   what carries that read, so the default kit keeps it.

using System.Collections.Generic;
using UnityEngine;

namespace ShuttleLab.Players
{
    /// <summary>Joint transforms of a built humanoid, for animation + racket use.</summary>
    public class HumanoidRig
    {
        public Transform Root;
        public Transform Hips;
        public Transform Spine;
        public Transform Chest;
        public Transform Neck;
        public Transform Head;
        public Transform ShoulderL;
        public Transform ShoulderR;
        public Transform ElbowL;
        public Transform ElbowR;
        public Transform WristL;
        public Transform WristR;
        public Transform HipL;
        public Transform HipR;
        public Transform KneeL;
        public Transform KneeR;
        public Transform AnkleL;
        public Transform AnkleR;

        /// <summary>Centre of the racket string bed (contact point).</summary>
        public Transform RacketHead;

        /// <summary>Racket handle / grip transform (child of the dominant wrist).</summary>
        public Transform RacketHandle;

        public GameObject RacketObject;
    }

    public static class HumanoidBuilder
    {
        // --- Proportions (metres, total height ~1.75 m) ---------------------
        private const float HipHeight = 0.95f;
        private const float ThighLength = 0.42f;
        private const float ShinLength = 0.42f;
        private const float ShoulderWidth = 0.46f;   // shoulder-to-shoulder (broad, athletic V-taper)
        private const float UpperArmLength = 0.28f;
        private const float ForearmLength = 0.26f;
        private const float HeadRadius = 0.11f;
        private const float RacketHandleLength = 0.38f;
        private const float RacketHeadRadius = 0.115f;

        private static readonly Dictionary<Color, Material> MaterialCache =
            new Dictionary<Color, Material>();

        private static Material _stringMaterial;
        private static Material _racketFrameMaterial;

        /// <summary>
        /// Build a humanoid under <paramref name="parent"/> (usually the
        /// PlayerController GameObject, whose transform is at the feet).
        /// The character faces +Z in local space.
        /// </summary>
        public static HumanoidRig Build(GameObject parent, PlayerStats stats, bool leftHanded)
        {
            if (stats == null) stats = PlayerStats.DefaultPlayer();

            var rig = new HumanoidRig();
            GameObject root = new GameObject(string.IsNullOrEmpty(stats.Name)
                ? "Humanoid" : "Humanoid_" + stats.Name);
            root.transform.SetParent(parent.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            rig.Root = root.transform;

            Material skin = GetMaterial(stats.SkinColor);
            Material shirt = GetMaterial(stats.ShirtColor);
            Material shorts = GetMaterial(stats.ShortsColor);
            Material shoe = GetMaterial(stats.ShoeColor);
            Material hair = GetMaterial(stats.HairColor);

            // --- Lower body --------------------------------------------------
            rig.Hips = Joint(root.transform, "Hips", new Vector3(0f, HipHeight, 0f));
            AddMesh(rig.Hips, PrimitiveType.Capsule, "Pelvis",
                new Vector3(0f, 0.02f, 0f), new Vector3(0.30f, 0.22f, 0.19f), shorts);

            rig.HipL = Joint(rig.Hips, "HipL", new Vector3(-0.10f, -0.02f, 0f));
            rig.HipR = Joint(rig.Hips, "HipR", new Vector3(0.10f, -0.02f, 0f));
            BuildLeg(rig.HipL, rig, "L", shorts, skin, shoe, out rig.KneeL, out rig.AnkleL);
            BuildLeg(rig.HipR, rig, "R", shorts, skin, shoe, out rig.KneeR, out rig.AnkleR);

            // --- Torso -------------------------------------------------------
            rig.Spine = Joint(rig.Hips, "Spine", new Vector3(0f, 0.13f, 0f));
            AddMesh(rig.Spine, PrimitiveType.Capsule, "Abdomen",
                new Vector3(0f, 0.08f, 0f), new Vector3(0.30f, 0.26f, 0.19f), shirt);

            rig.Chest = Joint(rig.Spine, "Chest", new Vector3(0f, 0.20f, 0f));
            AddMesh(rig.Chest, PrimitiveType.Capsule, "Chest",
                new Vector3(0f, 0.08f, 0f), new Vector3(0.40f, 0.28f, 0.22f), shirt);

            rig.Neck = Joint(rig.Chest, "Neck", new Vector3(0f, 0.18f, 0f));
            AddMesh(rig.Neck, PrimitiveType.Cylinder, "Neck",
                new Vector3(0f, 0.03f, 0f), new Vector3(0.09f, 0.08f, 0.09f), skin);
            // Polo collar ring at the base of the neck (shirt colour).
            AddMesh(rig.Neck, PrimitiveType.Cylinder, "Collar",
                new Vector3(0f, 0.065f, 0f), new Vector3(0.14f, 0.025f, 0.14f), shirt);

            rig.Head = Joint(rig.Neck, "Head", new Vector3(0f, 0.10f, 0f));
            AddMesh(rig.Head, PrimitiveType.Sphere, "Head",
                Vector3.zero, new Vector3(HeadRadius * 2f, HeadRadius * 2f, HeadRadius * 2f), skin);
            // Hair cap: a slightly larger sphere, flattened and shifted up/back.
            AddMesh(rig.Head, PrimitiveType.Sphere, "Hair",
                new Vector3(0f, 0.035f, -0.015f),
                new Vector3(HeadRadius * 2.1f, HeadRadius * 1.5f, HeadRadius * 2.1f), hair);

            // --- Arms --------------------------------------------------------
            rig.ShoulderL = Joint(rig.Chest, "ShoulderL",
                new Vector3(-ShoulderWidth * 0.5f, 0.10f, 0f));
            rig.ShoulderR = Joint(rig.Chest, "ShoulderR",
                new Vector3(ShoulderWidth * 0.5f, 0.10f, 0f));
            BuildArm(rig.ShoulderL, "L", shirt, skin,
                out rig.ElbowL, out rig.WristL);
            BuildArm(rig.ShoulderR, "R", shirt, skin,
                out rig.ElbowR, out rig.WristR);

            // --- Racket in the dominant hand ---------------------------------
            Transform dominantWrist = leftHanded ? rig.WristL : rig.WristR;
            BuildRacket(dominantWrist, rig);

            return rig;
        }

        private static void BuildLeg(Transform hip, HumanoidRig rig, string side,
            Material shortsMat, Material skinMat, Material shoeMat,
            out Transform knee, out Transform ankle)
        {
            // Thigh: shorts end above the knee (top 0.30 m), then bare skin
            // down to the knee — the reference kit's shorts-above-knee look.
            AddMesh(hip, PrimitiveType.Capsule, "ThighShorts" + side,
                new Vector3(0f, -0.15f, 0f),
                new Vector3(0.14f, 0.30f, 0.14f), shortsMat);
            AddMesh(hip, PrimitiveType.Capsule, "ThighSkin" + side,
                new Vector3(0f, -0.35f, 0f),
                new Vector3(0.12f, 0.16f, 0.12f), skinMat);

            knee = Joint(hip, "Knee" + side, new Vector3(0f, -ThighLength, 0f));
            AddMesh(knee, PrimitiveType.Sphere, "KneeCap" + side,
                Vector3.zero, new Vector3(0.11f, 0.11f, 0.11f), skinMat);

            // Shin (skin) from knee down, with a white crew sock covering
            // the lower shin just above the ankle.
            AddMesh(knee, PrimitiveType.Capsule, "Shin" + side,
                new Vector3(0f, -ShinLength * 0.5f, 0f),
                new Vector3(0.10f, ShinLength, 0.10f), skinMat);
            AddMesh(knee, PrimitiveType.Cylinder, "Sock" + side,
                new Vector3(0f, -0.34f, 0f),
                new Vector3(0.105f, 0.16f, 0.105f), shoeMat);

            ankle = Joint(knee, "Ankle" + side, new Vector3(0f, -ShinLength, 0f));

            // Foot + shoe (white), pointing forward (+Z), with a dark sole.
            AddMesh(ankle, PrimitiveType.Cube, "Foot" + side,
                new Vector3(0f, -0.035f, 0.07f),
                new Vector3(0.10f, 0.07f, 0.26f), shoeMat);
            AddMesh(ankle, PrimitiveType.Cube, "Sole" + side,
                new Vector3(0f, -0.075f, 0.07f),
                new Vector3(0.11f, 0.02f, 0.27f),
                GetMaterial(new Color(0.12f, 0.12f, 0.13f)));
        }

        private static void BuildArm(Transform shoulder, string side,
            Material shirtMat, Material skinMat,
            out Transform elbow, out Transform wrist)
        {
            // Upper arm (shirt sleeve) from shoulder down.
            AddMesh(shoulder, PrimitiveType.Capsule, "UpperArm" + side,
                new Vector3(0f, -UpperArmLength * 0.5f, 0f),
                new Vector3(0.09f, UpperArmLength, 0.09f), shirtMat);

            elbow = Joint(shoulder, "Elbow" + side, new Vector3(0f, -UpperArmLength, 0f));
            AddMesh(elbow, PrimitiveType.Sphere, "ElbowCap" + side,
                Vector3.zero, new Vector3(0.08f, 0.08f, 0.08f), skinMat);

            // Forearm (skin) from elbow down.
            AddMesh(elbow, PrimitiveType.Capsule, "Forearm" + side,
                new Vector3(0f, -ForearmLength * 0.5f, 0f),
                new Vector3(0.075f, ForearmLength, 0.075f), skinMat);

            wrist = Joint(elbow, "Wrist" + side, new Vector3(0f, -ForearmLength, 0f));
            AddMesh(wrist, PrimitiveType.Sphere, "Hand" + side,
                new Vector3(0f, -0.06f, 0f), new Vector3(0.08f, 0.12f, 0.08f), skinMat);
        }

        /// <summary>
        /// Gắn vợt vào cổ tay của rig procedural (chi hướng -Y của khớp).
        /// </summary>
        private static void BuildRacket(Transform wrist, HumanoidRig rig)
        {
            BuildRacketOriented(wrist, rig,
                new Vector3(0f, -0.10f, 0f), Quaternion.identity);
        }

        /// <summary>
        /// Gắn vợt vào cổ tay của rig IMPORT (bone Blender: chi hướng +Y).
        /// Vợt xây y hệt bản procedural nhưng lật 180° quanh Z: cán vợt đi
        /// theo +Y của cổ tay (ra khỏi bàn tay, về phía ngón tay) thay vì
        /// -Y, còn mặt vợt vẫn quay +Z của cổ tay.
        /// CHƯA KIỂM CHỨNG: hướng +Z của bone hand sau khi import FBX phụ
        /// thuộc bone roll của Blender — nếu trong game mặt vợt quay ngược
        /// (RacketController.IsFacingShuttle sai), đổi Euler Z ở đây thành
        /// (180, 0, 0) hoặc (0, 180, 0) rồi soi lại Calibration Scene.
        /// </summary>
        public static void AttachRacketImported(Transform wrist, HumanoidRig rig)
        {
            BuildRacketOriented(wrist, rig,
                new Vector3(0f, 0.10f, 0f), Quaternion.Euler(0f, 0f, 180f));
        }

        private static void BuildRacketOriented(Transform wrist, HumanoidRig rig,
            Vector3 racketLocalPos, Quaternion racketLocalRot)
        {
            // Racket group hangs from the wrist. Handle + head are ~0.68 m
            // long, like a real racket. Hướng cán (dọc trục Y của racket)
            // do racketLocalRot quyết định: procedural thì -Y của cổ tay,
            // rig import thì +Y của bone tay (xem AttachRacketImported).
            GameObject racket = new GameObject("Racket");
            racket.transform.SetParent(wrist, false);
            racket.transform.localPosition = racketLocalPos;
            racket.transform.localRotation = racketLocalRot;
            rig.RacketObject = racket;

            Material frame = RacketFrameMaterial;
            Material grip = GetMaterial(new Color(0.05f, 0.05f, 0.05f));

            // Grip + shaft (handle).
            AddMesh(racket.transform, PrimitiveType.Cylinder, "RacketHandle",
                new Vector3(0f, -RacketHandleLength * 0.5f, 0f),
                new Vector3(0.035f, RacketHandleLength, 0.035f), grip);
            rig.RacketHandle = racket.transform;

            float headCentreY = -(RacketHandleLength + RacketHeadRadius);

            // Head frame: a real torus mesh (no torus primitive in Unity).
            GameObject frameObj = new GameObject("RacketHeadFrame");
            frameObj.transform.SetParent(racket.transform, false);
            frameObj.transform.localPosition = new Vector3(0f, headCentreY, 0f);
            frameObj.transform.localRotation = Quaternion.identity;
            var filter = frameObj.AddComponent<MeshFilter>();
            filter.mesh = CreateTorusMesh(RacketHeadRadius, 0.012f, 8, 24);
            var renderer = frameObj.AddComponent<MeshRenderer>();
            renderer.material = frame;

            // Strings: a thin disc in the XY plane (face normal = +Z).
            AddMesh(racket.transform, PrimitiveType.Cylinder, "RacketStrings",
                new Vector3(0f, headCentreY, 0f),
                new Vector3(RacketHeadRadius * 1.85f, 0.004f, RacketHeadRadius * 1.85f),
                StringMaterial,
                Quaternion.Euler(90f, 0f, 0f));

            // Empty contact transform at the centre of the string bed.
            GameObject head = new GameObject("RacketHead");
            head.transform.SetParent(racket.transform, false);
            head.transform.localPosition = new Vector3(0f, headCentreY, 0f);
            head.transform.localRotation = Quaternion.identity;
            rig.RacketHead = head.transform;
        }

        // --- Helpers ---------------------------------------------------------

        private static Transform Joint(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        /// <summary>
        /// Add a primitive mesh as a child of <paramref name="parent"/>.
        /// <paramref name="size"/> is the desired world size: (diameterX,
        /// height, diameterZ) for capsule/cylinder, (diameter) for sphere,
        /// (x, y, z) for cube. Colliders are stripped (no physics on the body).
        /// </summary>
        private static GameObject AddMesh(Transform parent, PrimitiveType type, string name,
            Vector3 localPos, Vector3 size, Material mat, Quaternion localRot = default)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot == default ? Quaternion.identity : localRot;
            switch (type)
            {
                case PrimitiveType.Capsule:
                case PrimitiveType.Cylinder:
                    // Base capsule/cylinder: height 2, radius 0.5.
                    go.transform.localScale = new Vector3(size.x, size.y * 0.5f, size.z);
                    break;
                case PrimitiveType.Sphere:
                    // Base sphere: diameter 1.
                    go.transform.localScale = size;
                    break;
                case PrimitiveType.Cube:
                    go.transform.localScale = size;
                    break;
                case PrimitiveType.Plane:
                    go.transform.localScale = size * 0.1f;
                    break;
                default:
                    go.transform.localScale = size;
                    break;
            }
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.material = mat;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            return go;
        }

        private static Material GetMaterial(Color color)
        {
            if (MaterialCache.TryGetValue(color, out Material mat)) return mat;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");
            if (shader == null) { Debug.LogError("[HumanoidBuilder] Không tìm thấy shader URP/Lit lẫn Standard."); return null; }
            mat = new Material(shader) { color = color };
            MaterialCache[color] = mat;
            return mat;
        }

        private static Material StringMaterial
        {
            get
            {
                if (_stringMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("Standard");
                    if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");
                    if (shader == null) { Debug.LogError("[HumanoidBuilder] Không tìm thấy shader URP/Lit lẫn Standard."); return null; }
                    _stringMaterial = new Material(shader)
                    {
                        color = new Color(0.92f, 0.92f, 0.88f)
                    };
                }
                return _stringMaterial;
            }
        }

        private static Material RacketFrameMaterial
        {
            get
            {
                if (_racketFrameMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("Standard");
                    if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");
                    if (shader == null) { Debug.LogError("[HumanoidBuilder] Không tìm thấy shader URP/Lit lẫn Standard."); return null; }
                    _racketFrameMaterial = new Material(shader)
                    {
                        color = new Color(0.15f, 0.15f, 0.18f)
                    };
                }
                return _racketFrameMaterial;
            }
        }

        /// <summary>
        /// Procedural torus mesh in the XY plane (ring normal = +Z), so the
        /// racket face points forward when the arm is raised.
        /// </summary>
        private static Mesh CreateTorusMesh(float radius, float tube,
            int radialSegments, int tubularSegments)
        {
            var mesh = new Mesh { name = "RacketHeadTorus" };
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();

            for (int j = 0; j <= tubularSegments; j++)
            {
                float u = (float)j / tubularSegments * Mathf.PI * 2f;
                Vector3 centre = new Vector3(Mathf.Cos(u) * radius, Mathf.Sin(u) * radius, 0f);
                Vector3 radial = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0f);
                for (int i = 0; i <= radialSegments; i++)
                {
                    float v = (float)i / radialSegments * Mathf.PI * 2f;
                    Vector3 offset = radial * Mathf.Cos(v) * tube
                        + Vector3.forward * Mathf.Sin(v) * tube;
                    verts.Add(centre + offset);
                    normals.Add(offset.normalized);
                }
            }

            int ring = radialSegments + 1;
            for (int j = 0; j < tubularSegments; j++)
            {
                for (int i = 0; i < radialSegments; i++)
                {
                    int a = j * ring + i;
                    int b = (j + 1) * ring + i;
                    int c = (j + 1) * ring + i + 1;
                    int d = j * ring + i + 1;
                    tris.Add(a); tris.Add(b); tris.Add(d);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
