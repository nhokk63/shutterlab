class_name CrowdBuilder
extends RefCounted
## Crowd (v0.2 phase 1) — Godot port of SceneBuilder.BuildCrowd().
##
## Unity built ~1008 spectators as individual GameObjects (3 sitting poses
## x 3 shirt colours) and combined them per side with StaticBatching
## (max 12 draw calls, frustum culling per side). In Godot the same crowd
## is built with MultiMesh: one MultiMesh per (pose, shirt) pair, so the
## whole arena crowd costs at most 9 draw calls and there is no per-side
## culling (the fixed match camera sees 2-3 sides at once anyway).
##
## Layout (faithful to Unity): 3 tiers x 2 rows x 42 seats per side,
## 4 sides = 1008 seats. Tier height 1.5 m, tier depth 1.9 m, stands
## front at |x| = 7.0 m (W/E) and z = 10.5 m (N/S). Seat rows sit
## 0.45 m and 1.35 m behind each tier front. Fixed seed 20261110 so
## every build produces the same crowd.
##
## Counts above 1008 (High 1200 / Ultra 1500 in the graphics spec) add
## a 3rd row per tier (max 1512 seats); counts below 1008 take an evenly
## spaced subset of the 2-row layout so density stays uniform.

const SEED := 20261110
const TIERS := 3
const SEATS_PER_ROW := 42
const ROW_SPACING := 0.5
const ROW_LENGTH := 21.0  # seats span -10.5..+10.5 m along the stand
const TIER_HEIGHT := 1.5
const TIER_DEPTH := 1.9
const STAND_FRONT_WEST := 7.0
const STAND_FRONT_NORTH := 10.5
const BASE_SEATS := 1008  # 3 tiers x 2 rows x 42 x 4 sides
const MAX_SEATS := 1512  # with the 3rd row (Ultra)

# Shirt colours (Unity): red / blue / yellow.
const SHIRT_COLORS := [
	Color(0.72, 0.14, 0.14),
	Color(0.13, 0.32, 0.75),
	Color(0.85, 0.68, 0.12),
]

# Unit cube (24 verts, per-face normals) used to assemble the spectator
# meshes from boxes, mirroring SceneBuilder.AppendBox().
const CUBE_VERTS := [
	Vector3(0.5, -0.5, 0.5), Vector3(0.5, -0.5, -0.5), Vector3(0.5, 0.5, -0.5), Vector3(0.5, 0.5, 0.5),
	Vector3(-0.5, -0.5, -0.5), Vector3(-0.5, -0.5, 0.5), Vector3(-0.5, 0.5, 0.5), Vector3(-0.5, 0.5, -0.5),
	Vector3(-0.5, 0.5, 0.5), Vector3(0.5, 0.5, 0.5), Vector3(0.5, 0.5, -0.5), Vector3(-0.5, 0.5, -0.5),
	Vector3(-0.5, -0.5, -0.5), Vector3(0.5, -0.5, -0.5), Vector3(0.5, -0.5, 0.5), Vector3(-0.5, -0.5, 0.5),
	Vector3(-0.5, -0.5, 0.5), Vector3(0.5, -0.5, 0.5), Vector3(0.5, 0.5, 0.5), Vector3(-0.5, 0.5, 0.5),
	Vector3(0.5, -0.5, -0.5), Vector3(-0.5, -0.5, -0.5), Vector3(-0.5, 0.5, -0.5), Vector3(0.5, 0.5, -0.5),
]
const CUBE_NORMALS := [
	Vector3(1, 0, 0), Vector3(1, 0, 0), Vector3(1, 0, 0), Vector3(1, 0, 0),
	Vector3(-1, 0, 0), Vector3(-1, 0, 0), Vector3(-1, 0, 0), Vector3(-1, 0, 0),
	Vector3(0, 1, 0), Vector3(0, 1, 0), Vector3(0, 1, 0), Vector3(0, 1, 0),
	Vector3(0, -1, 0), Vector3(0, -1, 0), Vector3(0, -1, 0), Vector3(0, -1, 0),
	Vector3(0, 0, 1), Vector3(0, 0, 1), Vector3(0, 0, 1), Vector3(0, 0, 1),
	Vector3(0, 0, -1), Vector3(0, 0, -1), Vector3(0, 0, -1), Vector3(0, 0, -1),
]
const CUBE_TRIS := [
	0, 1, 2, 0, 2, 3,
	4, 5, 6, 4, 6, 7,
	8, 9, 10, 8, 10, 11,
	12, 13, 14, 12, 14, 15,
	16, 17, 18, 16, 18, 19,
	20, 21, 22, 20, 22, 23,
]

var _variant_meshes: Array[ArrayMesh] = []
var _shirt_mats: Array[StandardMaterial3D] = []

func _init() -> void:
	for v in 3:
		_variant_meshes.append(_build_spectator_mesh(v))
	for c in SHIRT_COLORS:
		var m := StandardMaterial3D.new()
		m.albedo_color = c
		m.roughness = 0.85
		_shirt_mats.append(m)

## Builds the crowd under `parent`. Returns the number of spectators placed.
## count <= 0 places nobody (Low tier). Counts are clamped to MAX_SEATS.
func build_crowd(parent: Node3D, count: int) -> int:
	if count <= 0:
		return 0
	var rows := 2
	if count > BASE_SEATS:
		rows = 3
	var seats := _generate_seats(rows)
	var n := mini(count, seats.size())
	var selected: Array = []
	for k in n:
		selected.append(seats[int(k * seats.size() / n)])
	# Group by (pose, shirt) -> one MultiMesh per group (max 9 draw calls).
	var buckets: Dictionary = {}
	for s in selected:
		var key: int = int(s["variant"]) * 3 + int(s["shirt"])
		if not buckets.has(key):
			buckets[key] = []
		buckets[key].append(s)
	for key in buckets:
		var variant: int = key / 3
		var shirt: int = key % 3
		var group: Array = buckets[key]
		var mm := MultiMesh.new()
		mm.transform_format = MultiMesh.TRANSFORM_3D
		mm.mesh = _variant_meshes[variant]
		mm.instance_count = group.size()
		for i in group.size():
			var s: Dictionary = group[i]
			var basis := Basis.from_euler(Vector3(0, deg_to_rad(float(s["yaw"])), 0))
			basis = basis.scaled(Vector3.ONE * float(s["scale"]))
			mm.set_instance_transform(i, Transform3D(basis, s["pos"]))
		var mmi := MultiMeshInstance3D.new()
		mmi.name = "Crowd_V%d_Shirt%d" % [variant, shirt]
		mmi.multimesh = mm
		mmi.material_override = _shirt_mats[shirt]
		# Crowd sits outside the shadow spot's cone; skip shadow casting.
		mmi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		parent.add_child(mmi)
	return selected.size()

## All candidate seats in fixed order (side W,E,N,S -> tier -> row -> seat),
## with pose/shirt/scale/yaw drawn from the fixed seed. The RNG stream is
## consumed identically regardless of the requested count, so a smaller
## count is a strict evenly-spaced subset of the full crowd.
func _generate_seats(rows: int) -> Array:
	var rng := RandomNumberGenerator.new()
	rng.seed = SEED
	var seats: Array = []
	for side in 4:
		var is_we := side < 2
		var sign := -1.0
		if side == 1 or side == 2:
			sign = 1.0
		# Facing the court: W +90 (faces +X), E -90, N 180, S 0.
		var base_yaw := 90.0
		if side == 1:
			base_yaw = -90.0
		elif side == 2:
			base_yaw = 180.0
		elif side == 3:
			base_yaw = 0.0
		for t in TIERS:
			var tier_top_y := TIER_HEIGHT * (t + 1)
			var front := (STAND_FRONT_WEST if is_we else STAND_FRONT_NORTH) + t * TIER_DEPTH
			for row in rows:
				var row_offset := 0.45 + row * 0.9
				for i in SEATS_PER_ROW:
					var along := -ROW_LENGTH * 0.5 + i * ROW_SPACING + 0.25
					var pos: Vector3
					if is_we:
						pos = Vector3(sign * (front + row_offset), tier_top_y, along)
					else:
						pos = Vector3(along, tier_top_y, sign * (front + row_offset))
					var variant := 0
					if rng.randf() >= 0.70:
						variant = 1 if rng.randi_range(0, 1) == 0 else 2
					seats.append({
						"pos": pos,
						"yaw": base_yaw + rng.randf_range(-12.0, 12.0),
						"scale": rng.randf_range(0.92, 1.08),
						"variant": variant,
						"shirt": rng.randi_range(0, 2),
					})
	return seats

## Sitting spectator (~0.9 m tall, origin at seat level, faces +Z):
## torso + head + 2 thighs + 2 arms = 6 boxes = ~72 triangles.
## Variant 0: seated. Variant 1: arms raised cheering. Variant 2: leaning.
func _build_spectator_mesh(variant: int) -> ArrayMesh:
	var verts := PackedVector3Array()
	var normals := PackedVector3Array()
	var tris := PackedInt32Array()
	var lean := Vector3(deg_to_rad(10.0), 0, 0) if variant == 2 else Vector3.ZERO
	var lean_z := 0.06 if variant == 2 else 0.0
	var lean_head_z := 0.10 if variant == 2 else 0.0
	_append_box(verts, normals, tris, Vector3(0, 0.36, lean_z), Vector3(0.46, 0.58, 0.26), lean)
	_append_box(verts, normals, tris, Vector3(0, 0.78, 0.01 + lean_head_z), Vector3(0.23, 0.23, 0.23), lean)
	_append_box(verts, normals, tris, Vector3(-0.11, 0.14, 0.20), Vector3(0.15, 0.15, 0.42), Vector3.ZERO)
	_append_box(verts, normals, tris, Vector3(0.11, 0.14, 0.20), Vector3(0.15, 0.15, 0.42), Vector3.ZERO)
	if variant == 1:
		_append_box(verts, normals, tris, Vector3(-0.30, 0.72, 0), Vector3(0.11, 0.5, 0.11), Vector3(0, 0, deg_to_rad(18.0)))
		_append_box(verts, normals, tris, Vector3(0.30, 0.72, 0), Vector3(0.11, 0.5, 0.11), Vector3(0, 0, deg_to_rad(-18.0)))
	else:
		_append_box(verts, normals, tris, Vector3(-0.29, 0.36, 0), Vector3(0.11, 0.48, 0.11), Vector3(0, 0, deg_to_rad(6.0)))
		_append_box(verts, normals, tris, Vector3(0.29, 0.36, 0), Vector3(0.11, 0.48, 0.11), Vector3(0, 0, deg_to_rad(-6.0)))
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = verts
	arrays[Mesh.ARRAY_NORMAL] = normals
	arrays[Mesh.ARRAY_INDEX] = tris
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	return mesh

func _append_box(verts: PackedVector3Array, normals: PackedVector3Array,
		tris: PackedInt32Array, center: Vector3, size: Vector3, rot: Vector3) -> void:
	var basis := Basis.from_euler(rot)
	var base := verts.size()
	for i in CUBE_VERTS.size():
		verts.append(center + basis * (CUBE_VERTS[i] * size))
		normals.append((basis * CUBE_NORMALS[i]).normalized())
	for t in CUBE_TRIS:
		tris.append(base + t)
