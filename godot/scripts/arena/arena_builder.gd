class_name ArenaBuilder
extends Node3D
## TV-broadcast arena (v0.2 phase 1) — Godot port of SceneBuilder's
## Match scene (BuildCourt / BuildStandsAndBoards / BuildArenaLights /
## BuildCrowd), adapted to Godot 4.5 nodes on the Mobile renderer.
##
## What it builds (all values faithful to the Unity source):
##   - Court: blue TV court (8 x 15 m) on a green surround (26 x 32 m)
##     on the dark arena floor (44 x 40 m), all 11 BWF white lines,
##     net (transparent quad + white top tape) and 2 metal posts.
##   - Stands: 3 stepped tiers on all 4 sides (tier 1.5 m high, 1.9 m
##     deep), dark walls + ceiling at 12 m (BWF standard).
##   - LED strips: emissive cyan along the front edge of tier 1 and 3.
##   - Ad boards: green/blue boards with the fictional brand "SHUTTLELAB"
##     (no real brand or tournament name is used anywhere).
##   - Lighting: 6 spotlights, 5600K (1, 0.96, 0.88), 10 m high, 2 rows
##     of 3, intensity 3.0, range 22 m, 50 deg cone; only the far-middle
##     spot casts shadows (1024). 4 fake additive light cones under the
##     corner spots, a dim cold directional fill (0.1), and a dark
##     WorldEnvironment (ambient 0.05/0.06/0.09, exponential fog 0.008).
##   - Crowd: via CrowdBuilder (MultiMesh, max 9 draw calls).
##
## INTEGRATION (read this): this builder creates the FULL court + the
## fill light. The current main.gd builds its own simplified court and
## a bright directional sun — when this arena is attached, REMOVE
## main.gd's _build_court() and the sun from _build_camera_and_light(),
## then:
##     var arena := ArenaBuilder.new()
##     add_child(arena)
##     arena.build(GraphicsQuality.tier_crowd_count(GraphicsQuality.current_tier()))
## The camera stays in main.gd (0, 6.5, -10.5, fov 55, look at (0,1,2)).
##
## Brightness note (VERIFIED ON DEVICE 2026-11-10): the original
## intensity 300 was copied from the Unity value (candela), but Godot 4
## in non-physical mode treats light_energy as a plain multiplier where
## 1.0 is normal — 6 spots x 300 blew the whole court white on the
## phone (screenshots: court, stands and players all washed out).
## SPOT_INTENSITY 3.0 + ambient 0.25 is the corrected value for Godot.
## Do NOT copy Unity candela numbers into Godot light_energy again.
## All colours are copied from the Unity Linear-space values.

const CROWD_DEFAULT := 1008
const SPOT_INTENSITY := 3.0
const SPOT_HEIGHT := 10.0
const SPOT_RANGE := 22.0
const SPOT_ANGLE := 50.0
const AD_BRAND := "SHUTTLELAB"

const COURT_GREEN := Color(0.15, 0.55, 0.30)  # green playing surface (Pure Badminton)
const SURROUND_BLUE := Color(0.07, 0.10, 0.30)  # dark blue surround, like the video
const FLOOR_DARK := Color(0.05, 0.08, 0.14)
const LINE_WHITE := Color(0.95, 0.95, 0.92)
const POST_DARK := Color(0.10, 0.10, 0.12)
const STAND_DARK := Color(0.07, 0.08, 0.11)
const BOARD_GREEN := Color(0.05, 0.35, 0.20)
const BOARD_BLUE := Color(0.08, 0.20, 0.45)
const ARENA_WALL := Color(0.02, 0.02, 0.04)
const LED_CYAN := Color(0.50, 0.85, 1.00)
const SPOT_COLOR := Color(1.0, 0.96, 0.88)  # ~5600K
const FILL_COLOR := Color(0.55, 0.65, 0.85)

const STAND_TIER_HEIGHT := 1.5
const STAND_TIER_DEPTH := 1.9
const STAND_LENGTH := 26.0
const STAND_FRONT_WEST := 7.0
const STAND_FRONT_NORTH := 10.5

var environment: Environment
var crowd_count_built := 0

var _built := false
var _shadow_spot: SpotLight3D
var _world_env: WorldEnvironment
var _mats: Dictionary = {}

func build(crowd_count: int = CROWD_DEFAULT) -> void:
	if _built:
		return
	_built = true
	_build_environment()
	_build_court()
	_build_stands()
	_build_led_strips()
	_build_ad_boards()
	_build_arena_lights()
	_build_fill_light()
	if crowd_count > 0:
		crowd_count_built = CrowdBuilder.new().build_crowd(self, crowd_count)
	print("[ArenaBuilder] Arena built (crowd: %d)." % crowd_count_built)

## Toggle the single shadow-casting spot (Low tier turns shadows off).
func set_shadows_enabled(enabled: bool) -> void:
	if _shadow_spot != null:
		_shadow_spot.shadow_enabled = enabled

## Bloom / glow through the arena environment (v0.2 post-processing flag).
## Off by default; GraphicsQuality turns it on for High/Ultra.
func set_glow_enabled(enabled: bool) -> void:
	if environment != null:
		environment.glow_enabled = enabled

# ------------------------------------------------------------------ env

func _build_environment() -> void:
	environment = Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color(0.02, 0.03, 0.05)
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color(0.05, 0.06, 0.09)
	environment.ambient_light_energy = 0.25
	# Fog so the far stands sink into darkness (court:stands ~8:1-12:1).
	environment.fog_enabled = true
	environment.fog_light_color = Color(0.008, 0.009, 0.015)
	environment.fog_density = 0.008
	# Glow exists for the bloom flag but stays off until enabled.
	environment.glow_enabled = false
	environment.glow_intensity = 0.4
	environment.glow_strength = 1.0
	_world_env = WorldEnvironment.new()
	_world_env.environment = environment
	add_child(_world_env)

# ------------------------------------------------------------------ court

func _build_court() -> void:
	var court_root := Node3D.new()
	court_root.name = "Court"
	add_child(court_root)
	# Green Pure Badminton court (semi-gloss), dark blue surround, dark arena floor.
	_add_box(court_root, "CourtSurface", Vector3(0, -0.05, 0), Vector3(8, 0.1, 15),
		_mat("CourtGreen", COURT_GREEN, 0.4))
	_add_box(court_root, "SurroundFloor", Vector3(0, -0.06, 0), Vector3(26, 0.08, 32),
		_mat("SurroundBlue", SURROUND_BLUE, 0.85))
	_add_box(court_root, "ArenaFloor", Vector3(0, -0.08, 0), Vector3(44, 0.08, 40),
		_mat("FloorDark", FLOOR_DARK, 0.9))
	# White lines (0.04 m wide; box centre y = 0.006, top y = 0.026).
	var line := _mat("LineWhite", LINE_WHITE, 0.6)
	var lw := CourtDimensions.LINE_WIDTH
	var hw_d := CourtDimensions.DOUBLES_HALF_WIDTH  # 3.05
	var hw_s := CourtDimensions.SINGLES_HALF_WIDTH  # 2.59
	var hl := CourtDimensions.COURT_HALF_LENGTH  # 6.70
	var ssl := CourtDimensions.SHORT_SERVICE_DISTANCE  # 1.98
	var dsl := CourtDimensions.DOUBLES_LONG_SERVICE_DISTANCE  # 5.94
	_add_line(court_root, "BaselineNear", Vector3(0, 0, -hl), Vector3(hw_d * 2 + lw, lw, lw), line)
	_add_line(court_root, "BaselineFar", Vector3(0, 0, hl), Vector3(hw_d * 2 + lw, lw, lw), line)
	_add_line(court_root, "DoublesSidelineL", Vector3(-hw_d, 0, 0), Vector3(lw, lw, hl * 2 + lw), line)
	_add_line(court_root, "DoublesSidelineR", Vector3(hw_d, 0, 0), Vector3(lw, lw, hl * 2 + lw), line)
	_add_line(court_root, "SinglesSidelineL", Vector3(-hw_s, 0, 0), Vector3(lw, lw, hl * 2 + lw), line)
	_add_line(court_root, "SinglesSidelineR", Vector3(hw_s, 0, 0), Vector3(lw, lw, hl * 2 + lw), line)
	_add_line(court_root, "ShortServiceNear", Vector3(0, 0, -ssl), Vector3(hw_s * 2 + lw, lw, lw), line)
	_add_line(court_root, "ShortServiceFar", Vector3(0, 0, ssl), Vector3(hw_s * 2 + lw, lw, lw), line)
	_add_line(court_root, "CentreServiceLine", Vector3.ZERO, Vector3(lw, lw, ssl * 2 + lw), line)
	_add_line(court_root, "DoublesLongServiceNear", Vector3(0, 0, -dsl), Vector3(hw_d * 2 + lw, lw, lw), line)
	_add_line(court_root, "DoublesLongServiceFar", Vector3(0, 0, dsl), Vector3(hw_d * 2 + lw, lw, lw), line)
	# Net posts (dark metal) + transparent net quad + white top tape.
	var post_mat := _mat_smooth("PostMetal", POST_DARK, 0.6, 0.8)
	for px in [-hw_d, hw_d]:
		var post := MeshInstance3D.new()
		post.name = "NetPost"
		var cyl := CylinderMesh.new()
		cyl.top_radius = 0.03
		cyl.bottom_radius = 0.03
		cyl.height = CourtDimensions.POST_HEIGHT
		post.mesh = cyl
		post.position = Vector3(px, CourtDimensions.POST_HEIGHT * 0.5, 0)
		post.set_surface_override_material(0, post_mat)
		court_root.add_child(post)
	var net := MeshInstance3D.new()
	net.name = "Net"
	var quad := QuadMesh.new()
	quad.size = Vector2(hw_d * 2, CourtDimensions.NET_HEIGHT_CENTER)
	net.mesh = quad
	net.position = Vector3(0, CourtDimensions.NET_HEIGHT_CENTER * 0.5, 0)
	net.set_surface_override_material(0, _net_material())
	court_root.add_child(net)
	_add_box(court_root, "NetTopTape", Vector3(0, CourtDimensions.NET_HEIGHT_CENTER - 0.025, 0),
		Vector3(hw_d * 2, 0.05, 0.04), line)

# ------------------------------------------------------------------ stands

func _build_stands() -> void:
	var root := Node3D.new()
	root.name = "Arena"
	add_child(root)
	var stand := _mat_smooth("StandTier", STAND_DARK, 0.7)
	for t in 3:
		var y := STAND_TIER_HEIGHT * (t + 0.5)
		var front_w := STAND_FRONT_WEST + t * STAND_TIER_DEPTH
		var front_n := STAND_FRONT_NORTH + t * STAND_TIER_DEPTH
		_add_box(root, "StandWest_Tier%d" % t,
			Vector3(-(front_w + STAND_TIER_DEPTH * 0.5), y, 0), Vector3(STAND_TIER_DEPTH, STAND_TIER_HEIGHT, STAND_LENGTH), stand)
		_add_box(root, "StandEast_Tier%d" % t,
			Vector3(front_w + STAND_TIER_DEPTH * 0.5, y, 0), Vector3(STAND_TIER_DEPTH, STAND_TIER_HEIGHT, STAND_LENGTH), stand)
		_add_box(root, "StandNorth_Tier%d" % t,
			Vector3(0, y, front_n + STAND_TIER_DEPTH * 0.5), Vector3(STAND_LENGTH, STAND_TIER_HEIGHT, STAND_TIER_DEPTH), stand)
		_add_box(root, "StandSouth_Tier%d" % t,
			Vector3(0, y, -(front_n + STAND_TIER_DEPTH * 0.5)), Vector3(STAND_LENGTH, STAND_TIER_HEIGHT, STAND_TIER_DEPTH), stand)
	# Dark walls + ceiling (12 m, BWF standard).
	var wall := _mat_smooth("ArenaWall", ARENA_WALL, 0.75)
	_add_box(root, "ArenaWallWest", Vector3(-14.5, 6, 0), Vector3(1, 12, 38), wall)
	_add_box(root, "ArenaWallEast", Vector3(14.5, 6, 0), Vector3(1, 12, 38), wall)
	_add_box(root, "ArenaWallNorth", Vector3(0, 6, 18.5), Vector3(30, 12, 1), wall)
	_add_box(root, "ArenaWallSouth", Vector3(0, 6, -18.5), Vector3(30, 12, 1), wall)
	_add_box(root, "ArenaCeiling", Vector3(0, 12.25, 0), Vector3(31, 0.5, 39), wall)

func _build_led_strips() -> void:
	var root := _get_or_create_child("Arena")
	var led := _emissive("LedStrip", Color(0.05, 0.08, 0.10), LED_CYAN, 2.0)
	var strip_y0 := STAND_TIER_HEIGHT + 0.07  # 1.57
	var strip_y2 := STAND_TIER_HEIGHT * 3.0 + 0.07  # 4.57
	var front_w2 := STAND_FRONT_WEST + 2.0 * STAND_TIER_DEPTH  # 10.8
	var front_n2 := STAND_FRONT_NORTH + 2.0 * STAND_TIER_DEPTH  # 14.3
	_add_box(root, "LedStripWest0", Vector3(-STAND_FRONT_WEST, strip_y0, 0), Vector3(0.1, 0.12, STAND_LENGTH), led)
	_add_box(root, "LedStripWest2", Vector3(-front_w2, strip_y2, 0), Vector3(0.1, 0.12, STAND_LENGTH), led)
	_add_box(root, "LedStripEast0", Vector3(STAND_FRONT_WEST, strip_y0, 0), Vector3(0.1, 0.12, STAND_LENGTH), led)
	_add_box(root, "LedStripEast2", Vector3(front_w2, strip_y2, 0), Vector3(0.1, 0.12, STAND_LENGTH), led)
	_add_box(root, "LedStripNorth0", Vector3(0, strip_y0, STAND_FRONT_NORTH), Vector3(STAND_LENGTH, 0.12, 0.1), led)
	_add_box(root, "LedStripNorth2", Vector3(0, strip_y2, front_n2), Vector3(STAND_LENGTH, 0.12, 0.1), led)
	_add_box(root, "LedStripSouth0", Vector3(0, strip_y0, -STAND_FRONT_NORTH), Vector3(STAND_LENGTH, 0.12, 0.1), led)
	_add_box(root, "LedStripSouth2", Vector3(0, strip_y2, -front_n2), Vector3(STAND_LENGTH, 0.12, 0.1), led)

func _build_ad_boards() -> void:
	var root := _get_or_create_child("Arena")
	_add_box(root, "AdBoardWest", Vector3(-4.6, 0.5, 0), Vector3(0.15, 1, 12), _mat("BoardGreen", BOARD_GREEN, 0.8))
	_add_box(root, "AdBoardEast", Vector3(4.6, 0.5, 0), Vector3(0.15, 1, 12), _mat("BoardBlue", BOARD_BLUE, 0.8))
	# Fictional brand text, facing the court (no real brand / tournament).
	_add_board_text(root, Vector3(-4.5, 0.5, 0), 90.0)
	_add_board_text(root, Vector3(4.5, 0.5, 0), -90.0)

func _add_board_text(parent: Node3D, pos: Vector3, rot_y_deg: float) -> void:
	var label := Label3D.new()
	label.name = "AdBoardText"
	label.text = AD_BRAND
	label.font_size = 64
	label.pixel_size = 0.01
	label.modulate = Color.WHITE
	label.shaded = false
	label.position = pos
	label.rotation_degrees = Vector3(0, rot_y_deg, 0)
	parent.add_child(label)

# ------------------------------------------------------------------ lights

func _build_arena_lights() -> void:
	var root := Node3D.new()
	root.name = "ArenaLights"
	add_child(root)
	var fixture_mat := _mat_smooth("ArenaWall", ARENA_WALL, 0.75)
	var xs := [-4.5, 0.0, 4.5]
	var zs := [-3.5, 3.5]
	for x in xs:
		for z in zs:
			var spot_pos := Vector3(x, SPOT_HEIGHT, z)
			var target := Vector3(x, 0, 2.0 if z > 0.0 else -2.0)
			var spot := SpotLight3D.new()
			spot.name = "Spot_%s_%s" % [str(x), str(z)]
			spot.light_color = SPOT_COLOR
			spot.light_energy = SPOT_INTENSITY
			spot.spot_range = SPOT_RANGE
			spot.spot_angle = SPOT_ANGLE
			spot.spot_attenuation = 1.0
			root.add_child(spot)
			# Orient with a local transform (Basis.looking_at, -Z at the
			# target) instead of look_at(), which errors when the node
			# is not inside the tree yet.
			spot.transform = Transform3D(
				Basis.looking_at((target - spot_pos).normalized(), Vector3.UP), spot_pos)
			# Far-middle spot is the only shadow caster (1024).
			if x == 0.0 and z > 0.0:
				spot.shadow_enabled = true
				_shadow_spot = spot
			_add_box(root, "SpotFixture_%s_%s" % [str(x), str(z)],
				spot_pos, Vector3(0.6, 0.25, 0.6), fixture_mat)
	# Fake volumetric cones (additive) under the 4 corner spots.
	var cone_mat := _cone_material()
	for x in [-4.5, 4.5]:
		for z in zs:
			var spot_pos := Vector3(x, SPOT_HEIGHT, z)
			var target := Vector3(x, 0, 2.0 if z > 0.0 else -2.0)
			var length := spot_pos.distance_to(target)
			var cone := MeshInstance3D.new()
			cone.name = "LightCone_%s_%s" % [str(x), str(z)]
			cone.mesh = _build_cone_mesh(0.35, 2.8, length, 16)
			cone.set_surface_override_material(0, cone_mat)
			cone.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			root.add_child(cone)
			cone.transform = Transform3D(
				Basis.looking_at((target - spot_pos).normalized(), Vector3.UP), spot_pos)

func _build_fill_light() -> void:
	# Very dim cold fill; the 6 spots are the main light (same as Unity:
	# directional is 1.2 only in the light-for-tests scene).
	var sun := DirectionalLight3D.new()
	sun.name = "DirectionalLight"
	sun.light_color = FILL_COLOR
	sun.light_energy = 0.1
	sun.shadow_enabled = false
	sun.rotation_degrees = Vector3(-50, -30, 0)
	add_child(sun)

## Open cone (no caps), top radius at z=0, bottom radius at z=-length.
## The node -Z axis is the beam axis (orient with look_at).
func _build_cone_mesh(top_radius: float, bottom_radius: float, length: float, segments: int) -> ArrayMesh:
	var verts := PackedVector3Array()
	var uvs := PackedVector2Array()
	var tris := PackedInt32Array()
	for i in segments + 1:
		var a := float(i) / segments * TAU
		var c := cos(a)
		var s := sin(a)
		verts.append(Vector3(c * top_radius, s * top_radius, 0.0))
		verts.append(Vector3(c * bottom_radius, s * bottom_radius, -length))
		uvs.append(Vector2(float(i) / segments, 1.0))
		uvs.append(Vector2(float(i) / segments, 0.0))
	for i in segments:
		var a0 := i * 2
		var b0 := i * 2 + 1
		var a1 := i * 2 + 2
		var b1 := i * 2 + 3
		tris.append_array([a0, b0, a1, b0, b1, a1])
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = verts
	arrays[Mesh.ARRAY_TEX_UV] = uvs
	arrays[Mesh.ARRAY_INDEX] = tris
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	return mesh

# ------------------------------------------------------------------ helpers

func _get_or_create_child(node_name: String) -> Node3D:
	var n := get_node_or_null(node_name)
	if n == null:
		n = Node3D.new()
		n.name = node_name
		add_child(n)
	return n

func _add_box(parent: Node3D, node_name: String, center: Vector3, size: Vector3, mat: Material) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	mi.name = node_name
	var box := BoxMesh.new()
	box.size = size
	mi.mesh = box
	mi.position = center
	mi.set_surface_override_material(0, mat)
	parent.add_child(mi)
	return mi

func _add_line(parent: Node3D, node_name: String, pos: Vector3, size: Vector3, mat: Material) -> MeshInstance3D:
	# Thin box on the court surface: centre y = 0.006, top y = 0.026.
	return _add_box(parent, node_name, Vector3(pos.x, 0.006, pos.z), size, mat)

func _mat(key: String, color: Color, roughness: float) -> StandardMaterial3D:
	if _mats.has(key):
		return _mats[key]
	var m := StandardMaterial3D.new()
	m.albedo_color = color
	m.roughness = roughness
	_mats[key] = m
	return m

func _mat_smooth(key: String, color: Color, roughness: float, metallic: float = 0.0) -> StandardMaterial3D:
	if _mats.has(key):
		return _mats[key]
	var m := StandardMaterial3D.new()
	m.albedo_color = color
	m.roughness = roughness
	m.metallic = metallic
	_mats[key] = m
	return m

func _emissive(key: String, base_color: Color, emission: Color, intensity: float) -> StandardMaterial3D:
	if _mats.has(key):
		return _mats[key]
	var m := StandardMaterial3D.new()
	m.albedo_color = base_color
	m.emission_enabled = true
	m.emission = emission
	m.emission_energy_multiplier = intensity
	_mats[key] = m
	return m

func _net_material() -> StandardMaterial3D:
	if _mats.has("NetWhite"):
		return _mats["NetWhite"]
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(0.92, 0.92, 0.90, 0.45)
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	m.roughness = 0.7
	m.cull_mode = BaseMaterial3D.CULL_DISABLED
	_mats["NetWhite"] = m
	return m

func _cone_material() -> StandardMaterial3D:
	if _mats.has("LightCone"):
		return _mats["LightCone"]
	var m := StandardMaterial3D.new()
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.albedo_color = Color(1.0, 0.96, 0.88) * 0.055
	m.blend_mode = BaseMaterial3D.BLEND_MODE_ADD
	m.cull_mode = BaseMaterial3D.CULL_DISABLED
	m.depth_draw_mode = BaseMaterial3D.DEPTH_DRAW_DISABLED
	_mats["LightCone"] = m
	return m
