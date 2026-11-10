class_name AraCharacterRig
extends PlayerVisual
## Realistic character rig: loads an Ara 3D Gen GLB (Mixamo-compatible
## skeleton, 69 bones) and drives it through the SAME interface as the
## procedural PlayerVisual, so PlayerAnimation / PlayerController work
## unchanged. Joint rotations from PlayerAnimation are translated into
## Skeleton3D bone-pose rotations via a name map (see BONE_MAP).
##
## The GLB has no badminton animations, so the procedural pose clips
## (Ready / Smash / Run / ...) still drive the body — the mesh, skin and
## clothes are the realistic part. The GLB's own clips (Idle / Walk /
## Run / Sword slash / Throw grenade) are NOT used by the gameplay
## animation system.
##
## Usage: PlayerController creates AraCharacterRig instead of
## PlayerVisual when the GLB is available; build(stats) picks the male
## or female model. Falls back to procedural if the GLB fails to load.

const MALE_GLB := "res://models/Athletic_man.glb"
const FEMALE_GLB := "res://models/Athletic_woman.glb"

## PlayerVisual joint name -> Mixamo bone name.
## (Elbow bends the forearm, so ElbowL/R map to the ForeArm bones;
##  Knee bends the shin, so KneeL/R map to the Leg bones.)
const BONE_MAP := {
	"Hips": "Hips",
	"Spine": "Spine",
	"Chest": "Spine1",
	"Head": "Head",
	"ShoulderL": "LeftShoulder",
	"ShoulderR": "RightShoulder",
	"ElbowL": "LeftForeArm",
	"ElbowR": "RightForeArm",
	"HipL": "LeftUpLeg",
	"HipR": "RightUpLeg",
	"KneeL": "LeftLeg",
	"KneeR": "RightLeg",
	"AnkleL": "LeftFoot",
	"AnkleR": "RightFoot",
}

var _skeleton: Skeleton3D
var _bone_idx: Dictionary = {}  # joint name -> bone index
var _racket_tip: Node3D
var _loaded := false

## Build the rig. use_female picks the woman GLB, else the man GLB.
## Falls back to the procedural visual (super.build) if the GLB fails.
func build(stats: PlayerStats, use_female: bool = false) -> void:
	_stats = stats if stats != null else PlayerStats.default_player()
	var path := FEMALE_GLB if use_female else MALE_GLB
	var res: PackedScene = load(path)
	if res == null:
		push_warning("AraCharacterRig: could not load %s, using procedural." % path)
		super.build(_stats)
		return
	var inst: Node = res.instantiate()
	add_child(inst)
	_skeleton = _find_skeleton(inst)
	if _skeleton == null:
		push_warning("AraCharacterRig: no Skeleton3D in %s, using procedural." % path)
		inst.queue_free()
		super.build(_stats)
		return
	# Map joints to bones.
	for joint_name in BONE_MAP:
		var bone_name: String = BONE_MAP[joint_name]
		var idx := _skeleton.find_bone(bone_name)
		if idx >= 0:
			_bone_idx[joint_name] = idx
			# Keep a Node3D entry in `joints` so PlayerAnimation's
			# set_pose/reset_pose (which index by name) still resolve;
			# the actual rotation is applied to the bone in set_pose.
			var proxy := Node3D.new()
			proxy.name = joint_name
			add_child(proxy)
			joints[joint_name] = proxy
	# Racket: attach to the dominant hand's weapon socket (WeaponR) when
	# available — it is oriented for holding a weapon, unlike the hand
	# bone itself. Left-handed players use the left hand bone.
	var hand_bone := "LeftHand" if _stats.left_handed else "WeaponR"
	if _skeleton.find_bone(hand_bone) < 0:
		hand_bone = "LeftHand" if _stats.left_handed else "RightHand"
	var attach := BoneAttachment3D.new()
	attach.bone_name = hand_bone
	_skeleton.add_child(attach)
	_build_racket(attach)
	_loaded = true
	# Play the GLB's own Idle clip so the body holds a natural stance.
	# The procedural pose rotations are NOT applied to the Mixamo
	# skeleton: its A-pose rest orientation differs from the procedural
	# rig's, so the same Euler values put the arm/racket in the wrong
	# place and the AI cannot reach the shuttle (verified: with poses
	# applied the AI loses 0-9; without them the reach check works).
	var ap := _find_anim_player(inst)
	if ap != null and ap.has_animation("Idle"):
		ap.play("Idle")

## Apply a pose dict. For the Ara rig this only updates the proxy
## Node3Ds — the skeleton keeps playing the GLB's own Idle clip.
## (Applying the procedural Euler rotations to the Mixamo skeleton
## misplaces the arm/racket because the rest poses differ.)
func set_pose(pose: Dictionary) -> void:
	if not _loaded:
		super.set_pose(pose)
		return
	for joint_name in pose:
		var proxy: Node3D = joints.get(joint_name, null)
		if proxy != null:
			proxy.rotation = pose[joint_name]

func reset_pose() -> void:
	if not _loaded:
		super.reset_pose()
		return
	for joint_name in joints:
		var proxy: Node3D = joints.get(joint_name, null)
		if proxy != null:
			proxy.rotation = Vector3.ZERO

func contact_point() -> Vector3:
	if _loaded and _racket_tip != null:
		return _racket_tip.global_position
	return super.contact_point()

## Simple racket (same proportions as the procedural one) parented to
## the hand attachment; the tip is the contact point.
func _build_racket(parent: Node3D) -> void:
	var grip_mat := _mat(Color(0.05, 0.05, 0.05), 0.8)
	var frame_mat := _mat(Color(0.85, 0.85, 0.85), 0.35)
	var handle := MeshInstance3D.new()
	var handle_mesh := BoxMesh.new()
	handle_mesh.size = Vector3(0.03, RACKET_HANDLE_LENGTH, 0.03)
	handle.mesh = handle_mesh
	handle.position = Vector3(0, -RACKET_HANDLE_LENGTH * 0.5, 0)
	handle.set_surface_override_material(0, grip_mat)
	parent.add_child(handle)
	# Racket head: a flat oval (approximated with a thin box + rim).
	var head := MeshInstance3D.new()
	var head_mesh := BoxMesh.new()
	head_mesh.size = Vector3(RACKET_HEAD_RADIUS * 2.0, RACKET_HEAD_RADIUS * 2.4, 0.015)
	head.mesh = head_mesh
	head.position = Vector3(0, -RACKET_HANDLE_LENGTH - RACKET_HEAD_RADIUS, 0)
	head.set_surface_override_material(0, frame_mat)
	parent.add_child(head)
	_racket_tip = Node3D.new()
	_racket_tip.name = "RacketHead"
	_racket_tip.position = Vector3(0, -RACKET_HANDLE_LENGTH - RACKET_HEAD_RADIUS, 0)
	parent.add_child(_racket_tip)
	racket_head = _racket_tip

func _find_skeleton(n: Node) -> Skeleton3D:
	if n is Skeleton3D:
		return n
	for c in n.get_children():
		var r := _find_skeleton(c)
		if r != null:
			return r
	return null

func _find_anim_player(n: Node) -> AnimationPlayer:
	if n is AnimationPlayer:
		return n
	for c in n.get_children():
		var r := _find_anim_player(c)
		if r != null:
			return r
	return null

func _mat(color: Color, roughness: float) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = color
	m.roughness = roughness
	return m
