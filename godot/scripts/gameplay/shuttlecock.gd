class_name Shuttlecock
extends Node3D
## The shuttlecock. Simulated with real velocity / acceleration (RK2, 240 Hz
## substeps) — never a fixed curve or a tween to a pre-chosen landing spot.
## Collisions: court plane (rally ends), net plane (reflect with damping),
## net posts (approximated as spheres). Cork leads the velocity vector.
## Port of ShuttleLab.Gameplay.ShuttlecockController (Unity C#).
##
## Godot differences: physics runs in _physics_process (60 Hz tick, 240 Hz
## substeps inside); events are signals (landed / net_hit / died).
## The "died" signal is an addition: Unity's Kill() is silent, but the rally
## loop needs to know when the shuttle dies without landing.

signal landed(landing: Vector3, in_singles: bool, in_doubles: bool)
signal net_hit
signal died

enum State { HELD, FLYING, DEAD }

const SUBSTEP_HZ := 240.0
const POST_RADIUS := 0.05
const KILL_Y := -2.0

var state: int = State.HELD
var velocity: Vector3 = Vector3.ZERO
var wind: Vector3 = Vector3.ZERO

## Player index (0 = near/user, 1 = far/AI) who hit the shuttle last. -1 = none.
var last_hitter: int = -1

## Was the last launch a serve?
var last_was_serve: bool = false

var net_hits: int = 0
var flight_time: float = 0.0
var apex_height: float = 0.0

var _launch_pos: Vector3 = Vector3.ZERO
var _prev_pos: Vector3 = Vector3.ZERO

func _ready() -> void:
	_prev_pos = global_position
	_build_visual()

## Shuttlecock mesh built in code: white cork (sphere) leading at -Z,
## feather skirt (truncated cone) opening toward +Z. The root's -Z faces
## the velocity, so the cork leads the flight.
func _build_visual() -> void:
	var visual := Node3D.new()
	visual.name = "Visual"
	add_child(visual)

	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(1, 1, 1)
	mat.roughness = 0.55

	var cork := MeshInstance3D.new()
	var cork_mesh := SphereMesh.new()
	cork_mesh.radius = 0.025
	cork_mesh.height = 0.05
	cork.mesh = cork_mesh
	cork.position = Vector3(0, 0, -0.045)
	cork.set_surface_override_material(0, mat)
	visual.add_child(cork)

	var skirt := MeshInstance3D.new()
	var skirt_mesh := CylinderMesh.new()
	skirt_mesh.top_radius = 0.02  # narrow end, toward the cork (-Z)
	skirt_mesh.bottom_radius = 0.06  # wide end, trailing (+Z)
	skirt_mesh.height = 0.07
	skirt.mesh = skirt_mesh
	# Cylinder axis is Y; rotate -90 deg about X so +Y (narrow) maps to -Z.
	skirt.rotation.x = -PI / 2.0
	skirt.position = Vector3(0, 0, 0.0)
	skirt.set_surface_override_material(0, mat)
	visual.add_child(skirt)

func hold_at(position: Vector3) -> void:
	state = State.HELD
	velocity = Vector3.ZERO
	global_position = position
	_prev_pos = position
	flight_time = 0.0
	apex_height = position.y

func launch(initial_velocity: Vector3, hitter: int, is_serve: bool) -> void:
	state = State.FLYING
	velocity = initial_velocity
	last_hitter = hitter
	last_was_serve = is_serve
	net_hits = 0
	flight_time = 0.0
	apex_height = global_position.y
	_launch_pos = global_position
	_prev_pos = global_position

func kill() -> void:
	if state == State.DEAD:
		return
	state = State.DEAD
	velocity = Vector3.ZERO
	died.emit()

## Where the shuttle will land from its current state (for AI + UI).
func predict_landing() -> Dictionary:
	if state != State.FLYING:
		return {"landed": false, "landing": global_position, "flight_time": 0.0, "apex": global_position.y}
	return ShuttleAerodynamics.predict_landing(global_position, velocity, wind)

func _physics_process(delta: float) -> void:
	if state != State.FLYING:
		return

	var steps := maxi(1, int(round(delta * SUBSTEP_HZ)))
	var h := delta / steps

	for i in steps:
		_prev_pos = global_position
		var res: Array = ShuttleAerodynamics.step_rk2(global_position, velocity, h, wind)
		global_position = res[0]
		velocity = res[1]
		flight_time += h
		if global_position.y > apex_height:
			apex_height = global_position.y

		if _check_court_landing():
			return
		if _check_net_collision():
			break
		if _check_post_collision():
			break
		if global_position.y < KILL_Y or global_position.length() > 60.0:
			kill()
			return

	# Cork leads the flight direction.
	if velocity.length_squared() > 0.25:
		var vel_n := velocity.normalized()
		if absf(vel_n.dot(Vector3.UP)) < 0.95:
			var target_basis := Basis.looking_at(vel_n, Vector3.UP)
			global_transform.basis = global_transform.basis.slerp(target_basis, 0.35)

func _check_court_landing() -> bool:
	var pos := global_position
	if pos.y > 0.0:
		return false

	var f := clampf(_prev_pos.y / maxf(0.00001, _prev_pos.y - pos.y), 0.0, 1.0)
	var landing := _prev_pos.lerp(pos, f)
	landing.y = 0.0
	global_position = landing
	state = State.DEAD
	velocity = Vector3.ZERO
	landed.emit(landing, CourtDimensions.is_in_singles(landing), CourtDimensions.is_in_doubles(landing))
	return true

func _check_net_collision() -> bool:
	var pos := global_position
	# Crossing the net plane z = 0?
	if CourtDimensions.sign_of(_prev_pos.z) == CourtDimensions.sign_of(pos.z) and pos.z != 0.0:
		return false
	if absf(pos.x) > CourtDimensions.DOUBLES_HALF_WIDTH + 0.1:
		return false

	# Interpolate crossing point.
	var denom := _prev_pos.z - pos.z
	if absf(denom) < 0.000001:
		return false
	var f := clampf(_prev_pos.z / denom, 0.0, 1.0)
	var cross := _prev_pos.lerp(pos, f)

	var net_h := CourtDimensions.net_height_at(cross.x)
	if cross.y >= net_h:
		return false  # cleared the net

	# Hit the net: reflect z with heavy damping, bleed the rest.
	net_hits += 1
	var v := velocity
	v.z = -v.z * 0.22
	v.x *= 0.55
	v.y *= 0.45
	velocity = v
	# Push back to the hitter's side so we don't tunnel through.
	var p := global_position
	var side: int
	if absf(_prev_pos.z) < 0.000001:
		side = CourtDimensions.sign_of(pos.z)
	else:
		side = CourtDimensions.sign_of(_prev_pos.z)
	p.z = _prev_pos.z + float(side) * 0.02
	global_position = p
	net_hit.emit()
	return true

func _check_post_collision() -> bool:
	var pos := global_position
	for px in [-CourtDimensions.DOUBLES_HALF_WIDTH, CourtDimensions.DOUBLES_HALF_WIDTH]:
		var post_top := Vector3(px, CourtDimensions.POST_HEIGHT, 0.0)
		var post_bottom := Vector3(px, 0.0, 0.0)
		var closest := _closest_point_on_segment(pos, post_bottom, post_top)
		if (pos - closest).length_squared() < POST_RADIUS * POST_RADIUS:
			var n := (pos - closest).normalized()
			if n.length_squared() < 0.000001:
				n = Vector3(0, 0, 1)  # Unity Vector3.forward
			velocity = velocity.bounce(n) * 0.3
			global_position = closest + n * (POST_RADIUS + 0.01)
			net_hits += 1
			net_hit.emit()
			return true
	return false

static func _closest_point_on_segment(p: Vector3, a: Vector3, b: Vector3) -> Vector3:
	var ab := b - a
	var t := clampf((p - a).dot(ab) / maxf(ab.length_squared(), 0.000001), 0.0, 1.0)
	return a + ab * t
