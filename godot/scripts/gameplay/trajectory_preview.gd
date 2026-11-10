class_name TrajectoryPreview
extends Node3D
## Predicted shuttle trajectory (Pure Badminton style): a yellow arc
## showing where the shuttle will fly, plus a ring + dot marking the
## landing spot on the court. Only visible while the shuttle is flying.
## The arc is a camera-facing ribbon (triangle strip) so it stays
## visible on mobile; the landing point comes from the same RK2
## simulation the physics uses (ShuttleAerodynamics.step_rk2).

const ARC_COLOR := Color(1.0, 0.85, 0.1)
const ARC_HALF_WIDTH := 0.045
const RING_RADIUS := 0.35
const MAX_POINTS := 90
const DT := 1.0 / 60.0
const MAX_TIME := 3.0

var _shuttle: Shuttlecock
var _arc: MeshInstance3D
var _arc_mesh: ImmediateMesh
var _ring: MeshInstance3D
var _ring_mesh: ImmediateMesh
var _dot: MeshInstance3D

func setup(shuttle: Shuttlecock) -> void:
	_shuttle = shuttle
	_arc_mesh = ImmediateMesh.new()
	_arc = MeshInstance3D.new()
	_arc.name = "TrajectoryArc"
	_arc.mesh = _arc_mesh
	_arc.material_override = _unshaded(ARC_COLOR)
	_arc.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(_arc)
	_ring_mesh = ImmediateMesh.new()
	_ring = MeshInstance3D.new()
	_ring.name = "LandingRing"
	_ring.mesh = _ring_mesh
	_ring.material_override = _unshaded(ARC_COLOR)
	_ring.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(_ring)
	_dot = MeshInstance3D.new()
	_dot.name = "LandingDot"
	var sphere := SphereMesh.new()
	sphere.radius = 0.09
	sphere.height = 0.18
	_dot.mesh = sphere
	_dot.material_override = _unshaded(ARC_COLOR)
	_dot.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(_dot)
	visible = false

func _process(_delta: float) -> void:
	if _shuttle == null or _shuttle.state != Shuttlecock.State.FLYING:
		visible = false
		return
	visible = true
	var points := _simulate()
	_update_arc(points)
	if points.is_empty():
		_ring.visible = false
		_dot.visible = false
		return
	var landing: Vector3 = points[points.size() - 1]
	landing.y = 0.0
	_update_ring(landing)
	_dot.visible = true
	_dot.global_position = landing + Vector3(0, 0.05, 0)

## Step the RK2 integrator and collect the flight path (up to landing).
func _simulate() -> Array:
	var points: Array = []
	var pos: Vector3 = _shuttle.global_position
	var vel: Vector3 = _shuttle.velocity
	var wind: Vector3 = _shuttle.wind
	var t := 0.0
	points.append(pos)
	while t < MAX_TIME and points.size() < MAX_POINTS:
		var res: Array = ShuttleAerodynamics.step_rk2(pos, vel, DT, wind)
		pos = res[0]
		vel = res[1]
		t += DT
		points.append(pos)
		if pos.y <= 0.0:
			break
	return points

## Camera-facing ribbon along the trajectory points.
func _update_arc(points: Array) -> void:
	_arc_mesh.clear_surfaces()
	if points.size() < 2:
		return
	var cam := get_viewport().get_camera_3d()
	var cam_pos := cam.global_position if cam != null else Vector3(0, 6, -10)
	_arc_mesh.surface_begin(Mesh.PRIMITIVE_TRIANGLE_STRIP)
	for i in points.size():
		var p: Vector3 = points[i]
		var prev: Vector3 = points[maxi(0, i - 1)]
		var next: Vector3 = points[mini(points.size() - 1, i + 1)]
		var tangent: Vector3 = (next - prev).normalized()
		if tangent.length_squared() < 0.001:
			tangent = Vector3.FORWARD
		var to_cam: Vector3 = (cam_pos - p).normalized()
		var side: Vector3 = tangent.cross(to_cam)
		if side.length_squared() < 0.001:
			side = Vector3.RIGHT * ARC_HALF_WIDTH
		else:
			side = side.normalized() * ARC_HALF_WIDTH
		_arc_mesh.surface_add_vertex(p + side)
		_arc_mesh.surface_add_vertex(p - side)
	_arc_mesh.surface_end()

## Flat ring on the court at the landing point.
func _update_ring(landing: Vector3) -> void:
	_ring.visible = true
	_ring_mesh.clear_surfaces()
	_ring_mesh.surface_begin(Mesh.PRIMITIVE_LINE_STRIP)
	var segments := 32
	for i in segments + 1:
		var a := TAU * float(i) / float(segments)
		_ring_mesh.surface_add_vertex(landing + Vector3(cos(a) * RING_RADIUS, 0.03, sin(a) * RING_RADIUS))
	_ring_mesh.surface_end()

func _unshaded(color: Color) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.albedo_color = color
	m.cull_mode = BaseMaterial3D.CULL_DISABLED
	return m
