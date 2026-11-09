# Nhân vật CC0 — ShuttleLab

Model 3D miễn phí (CC0) dùng cho 2 người chơi trong scene Match.
File gốc (.glb) nằm ở `~/workspace/shuttlelab/assets-cc0/` (kèm README nguồn);
thư mục này chỉ chứa bản đã convert sang FBX để Unity 6000 import thẳng
(Unity không import `.glb` mặc định).

## File trong thư mục này

| File | Nguồn | Ghi chú |
|---|---|---|
| `UBC_SuperheroMale.fbx` | `powder-body.glb` (mirror `codyhxyz/powder-toy-3d`) | Nam, cao 1,82 m, rig 65-bone, 8 animation nhúng (idle/walk/jog/sprint/fall/swim/tread/death), texture suit + mắt + tóc |
| `UBC_SuperheroFemale.fbx` | `powder-woman.glb` (cùng mirror) | Nữ, cao 1,775 m, tương tự |
| `UAL1_Standard.fbx` | Mirror `IAFahim/quaternius.universalAnimationLibrary.standard` | 45 clip animation (Idle/Walk/Jump/Hit...) trên cùng skeleton 65-bone — **import sẵn, CHƯA nối vào Animator** (v0.1.0 vẫn dùng animation procedural; nối clip UAL1 là việc bản sau) |
| `LICENSE-UAL.txt` | Kèm theo UAL1 | CC0 1.0 |
| `Textures/*.png` | Tách từ texture WebP nhúng trong GLB | Suit / mắt / tóc (PNG để FBX + Unity đọc chắc chắn) |

## Giấy phép — CC0 1.0 Universal (public domain)

Tất cả các file trên: Quaternius Universal Base Characters + Universal
Animation Library, CC0 1.0 — dùng thương mại thoải mái, không cần ghi nguồn.
Không được bán lại file asset thô dạng standalone.

## LƯU Ý QUAN TRỌNG — bản Superhero, không phải Regular

Đây là bản **Superhero** (cơ bắp cuồn cuộn), KHÔNG phải bản **Regular**
(dáng vận động viên bình thường) trong gói đầy đủ 122 MB của Quaternius.
Dùng tạm cho v0.1.0. Muốn bản Regular + 20 kiểu tóc + texture đầy đủ:
tải 2 ZIP gốc trên itch.io (FREE $0, CC0, không cần tài khoản) rồi convert
lại bằng script dưới:

- https://quaternius.itch.io/universal-base-characters
  ("Universal Base Characters[Standard].zip", 122 MB)
- https://quaternius.itch.io/universal-animation-library
  ("Universal Animation Library[Standard].zip", 15 MB)

## Cách dùng trong game

- `SceneBuilder` (menu `ShuttleLab/Build All Scenes`) gắn
  `CC0CharacterRig` lên 2 Player của scene Match (user = nam, AI = nữ)
  và gán sẵn 2 model FBX này.
- `PlayerController.Init` thử dựng rig CC0 trước; bất cứ lỗi nào (thiếu
  file, thiếu xương) cũng **fallback sang humanoid procedural**
  (`HumanoidBuilder`) — không bao giờ dừng build vì thiếu model.
  Tắt hẳn bằng field `UseCC0Model = false` trên component.
- Animation procedural (`PlayerAnimationController`) lái rig CC0 qua
  `CCOPoseRetargeter` (giữ nguyên hướng thế giới từng khớp).
- Lần chạy Unity đầu tiên phải soi Idle / Ready / Smash / Run trong
  Calibration Scene rồi mới chốt (hướng mặt model, mặt vợt, tư thế T-pose
  — xem ghi chú CHƯA KIỂM CHỨNG trong `CC0CharacterRig.cs`).

## Convert lại (GLB -> FBX)

Quy trình 2 bước (đã chạy thành công ngày 2026-11-09):

```bash
# B1: giải nén meshopt — GLB gốc dùng EXT_meshopt_compression mà Blender
# KHÔNG import được (kể cả 4.5). Cần Node 18+:
npx -y @gltf-transform/cli copy <in.glb> <plain.glb>

# B2: Blender 4.5+ convert sang FBX (đừng dùng 4.2 — cũng không import
# được meshopt):
~/workspace/shuttlelab/tools/blender-4.5.15-linux-x64/blender -b \
  --python ~/workspace/shuttlelab/tools/glb_to_fbx.py -- \
  <plain.glb> <out.fbx> <textures_dir>
```

Script `glb_to_fbx.py` tự làm 3 việc dễ quên:

- Xóa mesh "Icosphere" rác do Blender tự sinh khi import glTF (42 verts,
  không material, không armature, bán kính ~1 m tại gốc) — export kèm nó
  thì Unity vẽ một quả cầu xám 2 m ở chân nhân vật.
- Lưu texture WebP nhúng trong GLB thành PNG thật bằng `save_render()`
  (lưu ý: `Image.save()` của Blender giữ nguyên encoding WebP dù đặt
  tên `.png`, file đó Unity decode sai).
- Tự verify bằng cách import lại FBX vừa xuất (đủ 65 bone / 3 mesh /
  8 action mới coi là OK).
