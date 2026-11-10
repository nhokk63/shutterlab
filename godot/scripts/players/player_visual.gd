class_name PlayerVisual
extends Node3D
## Procedural humanoid built from primitives (Godot equivalent of
## Unity's HumanoidBuilder). Proportions match HumanoidBuilder exactly
## (total height ~1.75 m): hip height 0.95, thigh 0.42, shin 0.42,
## upper arm 0.28, forearm 0.26, head radius 0.11, shoulder width 0.46,
## racket handle 0.38, racket head radius 0.115.
## Joints are named Node3Ds so PlayerAnimation can pose them by name.
## The racket is on the dominant hand (right by default, left when the
## player is left-handed).
##
## CC0 NOTE: Unity's CC0CharacterRig (Quaternius models) is NOT ported —
## the Godot project has no CC0 model files yet. This procedural rig is
## the visual, the same fallback Unity uses when the CC0 model fails to
## load. When CC0 GLB/FBX models are imported, a CC0 rig script can
## replace this node and expose the same joint names + contact_point().

const HIP_HEIGHT := 0.95
const THIGH_LENGTH := 0.42
const SHIN_LENGTH := 0.42
const UPPER_ARM_LENGTH := 0.28
const FOREARM_LENGTH := 0.26
const HEAD_RADIUS := 0.11
const SHOULDER_WIDTH := 0.46
const RACKET_HANDLE_LENGTH := 0.38
const RACKET_HEAD_RADIUS := 0.115

var joints: Dictionary = {}

## Centre of the string bed (contact point for shots).
var racket_head: Node3D

var _stats: PlayerStats

func build(stats: PlayerStats) -> void:
	_stats = stats if stats != null else PlayerStats.default_player()
	var left_handed: bool = _stats.left_handed
	var shirt := _mat(_stats.shirt_color, 0.7)
	var shorts := _mat(_stats.shorts_color, 0.7)
	var skin := _mat(_stats.skin_color, 0.55)
	var hair := _mat(_stats.hair_color, 0.6)
	var shoe := _mat(_stats.shoe_color, 0.5)
	var grip := _mat(Color(0.05, 0.05, 0.05), 0.8)
	var frame := _mat(Color(0.85, 0.85, 0.85), 0.35)

	# --- Legs -------------------------------------------------------
	var hip_l := _joint("HipL", Vector3(-0.11, HIP_HEIGHT, 0))
	var hip_r := _joint("HipR", Vector3(0.11, HIP_HEIGHT, 0))
	_add_limb(hip_l, Vector3(0, -THIGH_LENGTH * 0.5, 0), Vector3(0.10, THIGH_LENGTH, 0.10), skin)
	var knee_l := _joint("KneeL", Vector3(0, -THIGH_LENGTH, 0), hip_l)
	var knee_r := _joint("KneeR", Vector3(0, -THIGH_LENGTH, 0), hip_r)
	_add_limb(knee_l, Vector3(0, -SHIN_LENGTH * 0.5, 0), Vector3(0.09, SHIN_LENGTH, 0.09), skin)
	var ankle_l := _joint("AnkleL", Vector3(0, -SHIN_LENGTH, 0), knee_l)
	var ankle_r := _joint("AnkleR", Vector3(0, -SHIN_LENGTH, 0), knee_r)
	_add_box(ankle_l, Vector3(0, -0.02, 0.06), Vector3(0.10, 0.06, 0.26), shoe)
	_add_box(ankle_r, Vector3(0, -0.02, 0.06), Vector3(0.10, 0.06, 0.26), shoe)

	# --- Hips / torso ----------------------------------------------
	var hips := _joint("Hips", Vector3(0, HIP_HEIGHT, 0))
	_add_box(hips, Vector3(0, 0.03, 0), Vector3(0.34, 0.16, 0.20), shorts)
	var spine := _joint("Spine", Vector3(0, 0.18, 0), hips)
	var chest := _joint("Chest", Vector3(0, 0.22, 0), spine)
	_add_box(chest, Vector3(0, 0.12, 0), Vector3(0.40, 0.48, 0.24), shirt)
	# Polo collar.
	_add_box(chest, Vector3(0, 0.36, 0), Vector3(0.30, 0.05, 0.20), shirt)

	# --- Head --------------------------------------------------------
	var head := _joint("Head", Vector3(0, 0.48, 0), chest)
	var head_mesh := MeshInstance3D.new()
	var sphere := SphereMesh.new()
	sphere.radius = HEAD_RADIUS
	sphere.height = HEAD_RADIUS * 2.0
	head_mesh.mesh = sphere
	head_mesh.set_surface_override_material(0, skin)
	head.add_child(head_mesh)
	# Hair cap.
	var hair_mesh := MeshInstance3D.new()
	var hair_sphere := SphereMesh.new()
	hair_sphere.radius = HEAD_RADIUS * 1.02
	hair_sphere.height = HEAD_RADIUS * 1.4
	hair_mesh.mesh = hair_sphere
	hair_mesh.position = Vector3(0, 0.04, -0.02)
	hair_mesh.set_surface_override_material(0, hair)
	head.add_child(hair_mesh)

	# --- Arms --------------------------------------------------------
	var shoulder_l := _joint("ShoulderL", Vector3(-SHOULDER_WIDTH * 0.5, 0.30, 0), chest)
	var shoulder_r := _joint("ShoulderR", Vector3(SHOULDER_WIDTH * 0.5, 0.30, 0), chest)
	_add_limb(shoulder_l, Vector3(0, -UPPER_ARM_LENGTH * 0.5, 0), Vector3(0.09, UPPER_ARM_LENGTH, 0.09), shirt)
	_add_limb(shoulder_r, Vector3(0, -UPPER_ARM_LENGTH * 0.5, 0), Vector3(0.09, UPPER_ARM_LENGTH, 0.09), shirt)
	var elbow_l := _joint("ElbowL", Vector3(0, -UPPER_ARM_LENGTH, 0), shoulder_l)
	var elbow_r := _joint("ElbowR", Vector3(0, -UPPER_ARM_LENGTH, 0), shoulder_r)
	_add_limb(elbow_l, Vector3(0, -FOREARM_LENGTH * 0.5, 0), Vector3(0.075, FOREARM_LENGTH, 0.075), skin)
	_add_limb(elbow_r, Vector3(0, -FOREARM_LENGTH * 0.5, 0), Vector3(0.075, FOREARM_LENGTH, 0.075), skin)

	# --- Racket on the dominant hand ---------------------------------
	var dom_elbow: Node3D = elbow_l if left_handed else elbow_r
	var hand := Node3D.new()
	hand.name = "HandR" if not left_handed else "HandL"
	hand.position = Vector3(0, -FOREARM_LENGTH, 0)
	dom_elbow.add_child(hand)
	# Handle extends along the forearm direction.
	var handle := MeshInstance3D.new()
	var handle_mesh := BoxMesh.new()
	handle_mesh.size = Vector3(0.035, RACKET_HANDLE_LENGTH, 0.035)
	handle.mesh = handle_mesh
	handle.position = Vector3(0, -RACKET_HANDLE_LENGTH * 0.5, 0)
	handle.set_surface_override_material(0, grip)
	hand.add_child(handle)
	# String bed: flat disc at the handle tip (Godot has no torus
	# primitive; the disc reads as the racket head at game distance).
	racket_head = Node3D.new()
	racket_head.name = "RacketHead"
	racket_head.position = Vector3(0, -RACKET_HANDLE_LENGTH - RACKET_HEAD_RADIUS, 0)
	hand.add_child(racket_head)
	var bed := MeshInstance3D.new()
	var bed_mesh := CylinderMesh.new()
	bed_mesh.top_radius = RACKET_HEAD_RADIUS
	bed_mesh.bottom_radius = RACKET_HEAD_RADIUS
	bed_mesh.height = 0.012
	bed.mesh = bed_mesh
	bed.set_surface_override_material(0, frame)
	racket_head.add_child(bed)

## World position of the string-bed centre.
func contact_point() -> Vector3:
	if racket_head != null:
		return racket_head.global_position
	return global_position + Vector3.UP * 1.2

## Apply a pose: {joint_name: Vector3 euler radians}. Joints not in
## the pose keep their current rotation.
func set_pose(pose: Dictionary) -> void:
	for joint_name in pose:
		var joint: Node3D = joints.get(joint_name, null)
		if joint != null:
			joint.rotation = pose[joint_name]

## Switch the locomotion clip (Idle / Walk / Run). The procedural rig
## animates locomotion through set_pose, so this is a no-op here; the
## Ara rig overrides it to switch the GLB's own clips (otherwise the
## body glides across the court while the legs stand still).
func set_locomotion(_clip_name: String) -> void:
	pass

## Reset all joints to the identity rotation.
func reset_pose() -> void:
	for joint_name in joints:
		(joints[joint_name] as Node3D).rotation = Vector3.ZERO

func _joint(joint_name: String, pos: Vector3, parent: Node3D = null) -> Node3D:
	var j := Node3D.new()
	j.name = joint_name
	j.position = pos
	if parent != null:
		parent.add_child(j)
	else:
		add_child(j)
	joints[joint_name] = j
	return j

func _mat(color: Color, roughness: float) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = color
	m.roughness = roughness
	return m

func _add_limb(parent: Node3D, center: Vector3, size: Vector3, mat: Material) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	var box := BoxMesh.new()
	box.size = size
	mi.mesh = box
	mi.position = center
	mi.set_surface_override_material(0, mat)
	parent.add_child(mi)
	return mi

func _add_box(parent: Node3D, center: Vector3, size: Vector3, mat: Material) -> MeshInstance3D:
	return _add_limb(parent, center, size, mat)
