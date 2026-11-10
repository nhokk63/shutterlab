// CC0CharacterRig.cs — ShuttleLab (Unity 6)
//
// Gắn model nhân vật CC0 đã import (Quaternius Universal Base Characters,
// bản Superhero — xem Assets/Models/CC0/README_CC0.md) vào PlayerController
// thay cho humanoid procedural của HumanoidBuilder.
//
// NGUỒN / GIẤY PHÉP:
//   - Quaternius "Universal Base Characters" (Standard), CC0 1.0.
//     Model nam/nữ lấy qua mirror GitHub (powder-toy-3d), convert GLB -> FBX
//     bằng Blender headless (tools/glb_to_fbx.py). Giấy phép CC0, dùng
//     thương mại thoải mái, không cần ghi nguồn.
//   - ĐÂY LÀ BẢN SUPERHERO (cơ bắp), KHÔNG phải bản Regular (dáng vận động
//     viên) trong gói đầy đủ 122 MB. Dùng tạm cho v0.1.0; muốn bản Regular
//     + 20 kiểu tóc thì tải ZIP gốc từ itch.io rồi convert lại (xem README).
//
// CÁCH HOẠT ĐỘNG:
//   - SceneBuilder gắn component này lên 2 Player GameObject của scene Match
//     và gán sẵn MaleModel / FemaleModel (FBX đã import).
//   - PlayerController.Init gọi TryBuildRig TRƯỚC khi fallback sang
//     HumanoidBuilder.Build: model load được + đủ xương thì dùng model CC0,
//     bất cứ lỗi nào (thiếu file, thiếu xương, exception) cũng trả false và
//     game dùng humanoid procedural như cũ — KHÔNG BAO GIỜ dừng build vì
//     thiếu model. Tắt hẳn bằng field UseCC0Model = false.
//   - Animation procedural (PlayerAnimationController) vẫn lái rig CC0 qua
//     CCOPoseRetargeter: pose procedural là local rotation tuyệt đối theo
//     rig procedural (bind = identity, tay chân buông -Y), còn rig CC0 là
//     T-pose với bone roll của Blender. Retargeter chuyển pose sang hướng
//     thế giới (world orientation) từng khớp, đi từ trên xuống (cha trước
//     con sau), giữ nguyên bind rotation của các xương trung gian
//     (clavicle, spine_02, ball) và của xương gốc.
//
// CHƯA KIỂM CHỨNG TRONG UNITY (ghi rõ để khỏi tưởng đã xong):
//   - Hướng mặt của model sau khi import FBX: pipeline Blender -> Unity
//     chuẩn cho nhân vật quay +Z, nhưng chưa chạy thử. Nếu nhân vật quay
//     lưng lại camera thì chỉnh ModelYawOffset = 180.
//   - Mặt vợt (RacketHead.forward) phụ thuộc roll của bone hand: vợt được
//     gắn lật 180° quanh Z để cán đi theo +Y của bone tay (ra khỏi bàn tay).
//     Nếu trong game vợt quay ngược mặt, sửa AttachRacketImported trong
//     HumanoidBuilder (đổi Euler Z) — xem ghi chú ở đó.
//   - Retargeter đúng về mặt toán học (world orientation) nhưng chưa được
//     nhìn bằng mắt trong Calibration Scene: lần chạy Unity đầu tiên phải
//     soi Idle / Ready / Smash / Run rồi mới chốt.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShuttleLab.Players
{
    public class CC0CharacterRig : MonoBehaviour
    {
        [Header("Model import (FBX) — gán bởi SceneBuilder")]
        public GameObject MaleModel;
        public GameObject FemaleModel;

        [Header("Tùy chọn")]
        public bool UseFemaleModel = false;

        /// <summary>
        /// Kill-switch: false = bỏ qua model CC0, dùng humanoid procedural.
        /// </summary>
        public bool UseCC0Model = true;

        /// <summary>
        /// Xoay model quanh Y nếu sau khi import nhân vật quay sai hướng
        /// (pipeline chuẩn là +Z; nếu quay lưng thì đặt 180).
        /// </summary>
        public float ModelYawOffset = 0f;

        /// <summary>Retargeter cho PlayerAnimationController (null nếu fallback procedural).</summary>
        public CCOPoseRetargeter Retargeter { get; private set; }

        // Map khớp procedural -> tên bone Quaternius 65-bone.
        private static readonly Dictionary<string, string> BoneNames =
            new Dictionary<string, string>
            {
                { "Hips", "pelvis" },
                { "Spine", "spine_01" },
                { "Chest", "spine_03" },
                { "Neck", "neck_01" },
                { "Head", "Head" },
                { "ShoulderL", "upperarm_l" },
                { "ShoulderR", "upperarm_r" },
                { "ElbowL", "lowerarm_l" },
                { "ElbowR", "lowerarm_r" },
                { "WristL", "hand_l" },
                { "WristR", "hand_r" },
                { "HipL", "thigh_l" },
                { "HipR", "thigh_r" },
                { "KneeL", "calf_l" },
                { "KneeR", "calf_r" },
                { "AnkleL", "foot_l" },
                { "AnkleR", "foot_r" },
            };

        /// <summary>
        /// Thử dựng rig từ model CC0 import. Trả false (không throw) khi
        /// model thiếu / thiếu xương / lỗi khác — caller fallback procedural.
        /// </summary>
        public bool TryBuildRig(PlayerController owner, PlayerStats stats, out HumanoidRig rig)
        {
            rig = null;
            if (!UseCC0Model) return false;
            if (owner == null) return false;

            GameObject modelPrefab = UseFemaleModel ? FemaleModel : MaleModel;
            if (modelPrefab == null)
            {
                Debug.LogWarning("[CC0CharacterRig] Thiếu model FBX "
                    + (UseFemaleModel ? "(nữ)" : "(nam)")
                    + " — chưa convert GLB->FBX? Fallback humanoid procedural.");
                return false;
            }

            GameObject model = null;
            try
            {
                model = Instantiate(modelPrefab, owner.transform, false);
                model.name = modelPrefab.name;
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(0f, ModelYawOffset, 0f);
                model.transform.localScale = Vector3.one;

                var built = new HumanoidRig();
                built.Root = model.transform;

                foreach (var kv in BoneNames)
                {
                    Transform bone = FindDeep(model.transform, kv.Value);
                    if (bone == null)
                    {
                        Debug.LogWarning("[CC0CharacterRig] Thiếu bone '" + kv.Value
                            + "' trong model " + modelPrefab.name
                            + " — fallback humanoid procedural.");
                        Retargeter = null;
                        Destroy(model);
                        return false;
                    }
                    SetRigJoint(built, kv.Key, bone);
                }

                // Vợt vào tay thuận (bone tay hướng +Y — xem AttachRacketImported).
                bool leftHanded = stats != null && stats.LeftHanded;
                Transform dominantWrist = leftHanded ? built.WristL : built.WristR;
                HumanoidBuilder.AttachRacketImported(dominantWrist, built);

                Retargeter = new CCOPoseRetargeter(built, model.transform);
                rig = built;
                Debug.Log("[CC0CharacterRig] Đã dùng model CC0: " + modelPrefab.name);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CC0CharacterRig] Lỗi khi dựng model CC0, "
                    + "fallback humanoid procedural: " + e.Message);
                if (model != null) Destroy(model);
                Retargeter = null;
                return false;
            }
        }

        private static void SetRigJoint(HumanoidRig rig, string joint, Transform bone)
        {
            switch (joint)
            {
                case "Hips": rig.Hips = bone; break;
                case "Spine": rig.Spine = bone; break;
                case "Chest": rig.Chest = bone; break;
                case "Neck": rig.Neck = bone; break;
                case "Head": rig.Head = bone; break;
                case "ShoulderL": rig.ShoulderL = bone; break;
                case "ShoulderR": rig.ShoulderR = bone; break;
                case "ElbowL": rig.ElbowL = bone; break;
                case "ElbowR": rig.ElbowR = bone; break;
                case "WristL": rig.WristL = bone; break;
                case "WristR": rig.WristR = bone; break;
                case "HipL": rig.HipL = bone; break;
                case "HipR": rig.HipR = bone; break;
                case "KneeL": rig.KneeL = bone; break;
                case "KneeR": rig.KneeR = bone; break;
                case "AnkleL": rig.AnkleL = bone; break;
                case "AnkleR": rig.AnkleR = bone; break;
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }

    /// <summary>
    /// Retarget pose procedural sang rig CC0 import.
    ///
    /// Bài toán: pose procedural cho mỗi khớp một local rotation TUYỆT ĐỐI,
    /// viết cho rig procedural (bind = identity, chi buông -Y). Rig CC0 có
    /// bind rotation khác 0 (T-pose, bone roll Blender) nên gán trực tiếp
    /// sẽ sai hướng chi. Cách giải: giữ nguyên HƯỚNG THẾ GIỚI của từng khớp.
    ///   1. Tính hướng thế giới của mỗi khớp procedural từ pose (đi từ gốc
    ///      xuống: world[con] = world[cha] * poseLocal[con]; bind procedural
    ///      là identity nên world[cha] lúc nghỉ = frame nhân vật).
    ///   2. Đi dọc cây xương CC0 từ trên xuống: với khớp đã map, đặt
    ///      D = B^-1 * worldParent^-1 * worldMongMuốn rồi gán
    ///      localRotation = B * D (B = bind local rotation, ghi lúc dựng).
    ///      Xương trung gian không có trong pose (clavicle, spine_02,
    ///      ball, root) giữ D = identity, tức giữ nguyên bind.
    ///   3. Khớp nào pose không có thì giữ giá trị lần trước (giống hành vi
    ///      "giữ nguyên" của ApplyPose procedural) thay vì snap về identity.
    /// </summary>
    public class CCOPoseRetargeter : IJointPoseRetargeter
    {
        // Cha của mỗi khớp trong cây procedural (null = Hips, con của Root).
        private static readonly Dictionary<string, string> ProceduralParent =
            new Dictionary<string, string>
            {
                { "Hips", null },
                { "Spine", "Hips" },
                { "Chest", "Spine" },
                { "Neck", "Chest" },
                { "Head", "Neck" },
                { "ShoulderL", "Chest" },
                { "ShoulderR", "Chest" },
                { "ElbowL", "ShoulderL" },
                { "ElbowR", "ShoulderR" },
                { "WristL", "ElbowL" },
                { "WristR", "ElbowR" },
                { "HipL", "Hips" },
                { "HipR", "Hips" },
                { "KneeL", "HipL" },
                { "KneeR", "HipR" },
                { "AnkleL", "KneeL" },
                { "AnkleR", "KneeR" },
            };

        private class ChainNode
        {
            public Transform Bone;
            public string ProceduralJoint; // null = xương trung gian
            public Quaternion BindLocal;
            public ChainNode Parent;
            public Quaternion World;
        }

        private readonly List<ChainNode> _nodes = new List<ChainNode>();
        private readonly Dictionary<string, Quaternion> _lastPose =
            new Dictionary<string, Quaternion>();

        public CCOPoseRetargeter(HumanoidRig rig, Transform modelRoot)
        {
            if (rig == null || modelRoot == null)
                throw new ArgumentNullException("CCOPoseRetargeter cần rig + modelRoot.");

            // Map khớp procedural -> Transform xương CC0.
            var jointToBone = new Dictionary<string, Transform>
            {
                { "Hips", rig.Hips }, { "Spine", rig.Spine }, { "Chest", rig.Chest },
                { "Neck", rig.Neck }, { "Head", rig.Head },
                { "ShoulderL", rig.ShoulderL }, { "ShoulderR", rig.ShoulderR },
                { "ElbowL", rig.ElbowL }, { "ElbowR", rig.ElbowR },
                { "WristL", rig.WristL }, { "WristR", rig.WristR },
                { "HipL", rig.HipL }, { "HipR", rig.HipR },
                { "KneeL", rig.KneeL }, { "KneeR", rig.KneeR },
                { "AnkleL", rig.AnkleL }, { "AnkleR", rig.AnkleR },
            };

            // Thu thập toàn bộ xương trên đường từ mỗi khớp đã map lên tới
            // model root (gồm cả xương trung gian như clavicle, spine_02).
            var byTransform = new Dictionary<Transform, ChainNode>();
            foreach (var kv in jointToBone)
            {
                Transform t = kv.Value;
                bool first = true; // chỉ xương đầu tiên của đường đi là khớp đã map
                while (t != null && t != modelRoot)
                {
                    if (!byTransform.TryGetValue(t, out ChainNode node))
                    {
                        node = new ChainNode
                        {
                            Bone = t,
                            BindLocal = t.localRotation,
                            // Xương trung gian (clavicle, spine_02, root...)
                            // giữ ProceduralJoint = null -> giữ nguyên bind.
                            ProceduralJoint = first ? kv.Key : null,
                        };
                        byTransform[t] = node;
                        _nodes.Add(node);
                    }
                    else if (first && node.ProceduralJoint == null)
                    {
                        // Xương này từng được tạo khi đi qua với vai trò
                        // trung gian (vd spine_03 trên đường của ShoulderL),
                        // giờ mới tới lượt nó là khớp đã map (Chest).
                        node.ProceduralJoint = kv.Key;
                    }
                    first = false;
                    t = t.parent;
                }
            }

            // Nối cha + sắp xếp theo độ sâu (cha trước, con sau).
            foreach (ChainNode node in _nodes)
            {
                Transform p = node.Bone.parent;
                if (p != null && byTransform.TryGetValue(p, out ChainNode parentNode))
                    node.Parent = parentNode;
            }
            _nodes.Sort((a, b) => Depth(a).CompareTo(Depth(b)));
        }

        private static int Depth(ChainNode node)
        {
            int d = 0;
            ChainNode cur = node;
            while (cur.Parent != null) { d++; cur = cur.Parent; }
            return d;
        }

        public void ApplyPose(Dictionary<string, Quaternion> pose, HumanoidRig rig)
        {
            // 1. Hướng thế giới mong muốn cho từng khớp procedural.
            var srcWorld = new Dictionary<string, Quaternion>();
            foreach (string joint in PlayerAnimationController.JointNames)
            {
                Quaternion local;
                if (pose != null && pose.TryGetValue(joint, out Quaternion q))
                    local = q;
                else if (_lastPose.TryGetValue(joint, out Quaternion prev))
                    local = prev; // khớp không có trong pose: giữ giá trị cũ
                else
                    local = Quaternion.identity;

                _lastPose[joint] = local;
                string parent = ProceduralParent[joint];
                srcWorld[joint] = parent == null
                    ? local
                    : srcWorld[parent] * local;
            }

            // 2. Đi dọc cây xương CC0 từ trên xuống, gán localRotation.
            foreach (ChainNode node in _nodes)
            {
                Quaternion worldParent = node.Parent != null
                    ? node.Parent.World
                    : Quaternion.identity;
                Quaternion d = Quaternion.identity;
                if (node.ProceduralJoint != null
                    && srcWorld.TryGetValue(node.ProceduralJoint, out Quaternion wanted))
                {
                    d = Quaternion.Inverse(node.BindLocal)
                        * Quaternion.Inverse(worldParent)
                        * wanted;
                }
                node.World = worldParent * node.BindLocal * d;
                node.Bone.localRotation = node.BindLocal * d;
            }
        }
    }
}
