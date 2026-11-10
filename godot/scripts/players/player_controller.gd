class_name PlayerController
extends Node3D
## Orchestrator for one player: owns the procedural visual, the movement /
## footwork / stamina / animation / racket / shot components and wires
## them together in init. Other modules (OpponentAI, MatchManager,
## MobileInputManager) drive this controller only through its public API.
## _process does NOT decide anything for the AI: the AI calls
## move_towards / try_hit itself. User input arrives via set_move_input
## (called by the input layer); PlayerMovement consumes it in
## _physics_process. Shot quality is computed from contact timing +
## position + stamina — never from a bare random roll (ARCHITECTURE.md).
## Port of ShuttleLab.Players.PlayerController (Unity C#).
##
## Godot differences: sub-components are child Nodes (movement, anim,
## stamina, racket, shot_ctrl) and the visual is a child PlayerVisual;
## the CC0 rig is not ported — the procedural visual is used always
## (the same fallback Unity uses when the CC0 model fails to load).

## 0 = near side (user), 1 = far side (AI).
var player_index: int = 0
var is_user_controlled: bool = false

var stats: PlayerStats
var stamina: PlayerStamina
var movement: PlayerMovement
var anim: PlayerAnimation
var racket: RacketController
var shot_ctrl: ShotController

var visual: PlayerVisual
var footwork: FootworkController
var shuttle: Shuttlecock

var home_position: Vector3 = Vector3.ZERO:
	set(value):
		home_position = value
		if footwork != null:
			footwork.set_home_position(value)

## Reach radius (m) from the racket head for can_hit_shuttle.
var reach_radius: float = 1.15

## True while a swing animation is playing.
var is_swinging: bool = false

## Shuttle heights this player can reach (m).
const MIN_HIT_HEIGHT := 0.15
const MAX_HIT_HEIGHT := 3.20

# --- Setup ------------------------------------------------------------

## Build the visual, attach and wire all sub-components.
## HomePosition is set later via reset_for_point (or the property).
func init(index: int, p_stats: PlayerStats, p_shuttle: Shuttlecock, user_controlled: bool) -> void:
	player_index = index
	is_user_controlled = user_controlled
	stats = p_stats if p_stats != null else PlayerStats.default_player()
	shuttle = p_shuttle

	# Realistic Ara 3D Gen character (GLB, Mixamo rig); falls back to the
	# procedural visual inside AraCharacterRig.build() if the model fails
	# to load. P0 (user) = male model, P1 (AI) = female model.
	var rig := AraCharacterRig.new()
	rig.name = "Visual"
	add_child(rig)
	visual = rig
	rig.build(stats, index == 1)

	movement = PlayerMovement.new()
	movement.name = "Movement"
	add_child(movement)
	anim = PlayerAnimation.new()
	anim.name = "Animation"
	add_child(anim)
	stamina = PlayerStamina.new()
	stamina.name = "Stamina"
	add_child(stamina)
	racket = RacketController.new()
	racket.name = "Racket"
	add_child(racket)
	shot_ctrl = ShotController.new()
	shot_ctrl.name = "ShotController"
	add_child(shot_ctrl)

	var side_sign := -1 if player_index == 0 else 1
	movement.init(stats, side_sign, self)
	stamina.init(stats)
	anim.bind(visual)
	racket.bind(visual, anim)
	shot_ctrl.init(shuttle, self)

	footwork = FootworkController.new(movement, Vector3.ZERO)

func _process(delta: float) -> void:
	# Stamina regen: slow during a rally, faster between rallies.
	if stamina != null and shuttle != null:
		var in_rally := shuttle.state == Shuttlecock.State.FLYING
		stamina.regen(delta, in_rally)
		if movement != null:
			movement.speed_factor = stamina.speed_factor()

	if footwork != null:
		footwork.update(delta)

	# Idle facing: let Movement look at the shuttle when standing still.
	if movement != null and shuttle != null:
		movement.shuttle_position = shuttle.global_position
		movement.has_shuttle_position = shuttle.state == Shuttlecock.State.FLYING

	# Locomotion pose follows actual movement (user and AI alike):
	# Run while moving, back to Ready when still. Shot swings set
	# is_swinging and take over the animation while they play.
	if not is_swinging and anim != null and movement != null:
		if movement.is_moving() or movement.has_move_target():
			if anim.current_clip_name() != "Run":
				anim.play_loop("Run")
			anim.set_run_speed(clampf(movement.velocity().length() / maxf(0.1, movement.max_speed()), 0.0, 1.0))
		elif anim.current_clip_name() == "Run":
			anim.play_loop("Ready")

# --- Hitting ----------------------------------------------------------

## Can this player reach the shuttle right now? The shuttle must be
## within reach_radius of the racket head and at a hittable height.
## Returns {"ok": bool, "distance": float}.
func can_hit_shuttle(shuttle_pos: Vector3) -> Dictionary:
	var contact := racket.contact_point() if racket != null else global_position + Vector3.UP * 1.2
	var distance := shuttle_pos.distance_to(contact)
	if distance > reach_radius:
		return {"ok": false, "distance": distance}
	if shuttle_pos.y < MIN_HIT_HEIGHT or shuttle_pos.y > MAX_HIT_HEIGHT:
		return {"ok": false, "distance": distance}
	return {"ok": true, "distance": distance}

## Attempt a shot. Returns false when the shot cannot be attempted
## (out of reach, wrong turn, already swinging, shuttle not flying).
## Returns true when a swing was executed — including a Miss, which
## plays the whiff animation and never touches the shuttle.
## On a good swing the shot is launched from the animation's
## on_contact callback, at the clip's ContactTime.
func try_hit(shot_type: int, target: Vector3) -> bool:
	if is_swinging:
		return false
	if shuttle == null:
		return false
	if shuttle.state != Shuttlecock.State.FLYING:
		return false
	if shuttle.last_hitter == player_index:
		return false  # not our turn to hit

	var shuttle_pos := shuttle.global_position
	var hit_check := can_hit_shuttle(shuttle_pos)
	if not hit_check["ok"]:
		return false
	var distance: float = hit_check["distance"]

	var quality := _evaluate_quality(shot_type, shuttle_pos, distance)
	var profile: ShotData.ShotProfile = ShotData.ShotLibrary.get_profile(shot_type)

	if shot_type == ShotData.ShotType.JUMP_SMASH and movement != null:
		movement.jump()

	if quality == ShotData.ShotQuality.MISS:
		# Whiff: swing and miss, no contact, small stamina cost.
		is_swinging = true
		if stamina != null:
			stamina.drain(profile.stamina_cost * 0.4, _current_intensity())
		racket.play_swing_callbacks(shot_type, _swing_duration(shot_type), Callable(), Callable(self, "_on_swing_finished"))
		return true

	is_swinging = true
	if stamina != null:
		stamina.drain(profile.stamina_cost, _current_intensity())
	var on_contact := Callable(shot_ctrl, "execute_shot").bind(shot_type, target, quality)
	racket.play_swing_callbacks(shot_type, _swing_duration(shot_type), on_contact, Callable(self, "_on_swing_finished"))
	return true

## Serve into the target service box. The shuttle must be Held;
## it is placed at the racket contact point, then launched from the
## Serve clip's on_contact callback. Serve quality is deterministic
## (Perfect when stamina is high, else Good).
func serve(serve_type: int, target_box: Vector3) -> bool:
	if not ShotData.ShotLibrary.is_serve(serve_type):
		serve_type = ShotData.ShotType.SERVE_LOW
	if is_swinging:
		return false
	if shuttle == null:
		return false

	if shuttle.state == Shuttlecock.State.HELD and racket != null:
		shuttle.hold_at(racket.contact_point())

	var quality := ShotData.ShotQuality.PERFECT if (stamina != null and stamina.current >= 70.0) else ShotData.ShotQuality.GOOD

	is_swinging = true
	if stamina != null:
		stamina.drain(ShotData.ShotLibrary.get_profile(serve_type).stamina_cost, _current_intensity())
	var on_contact := Callable(shot_ctrl, "execute_shot").bind(serve_type, target_box, quality)
	var serve_duration := anim.get_clip_duration("Serve") if anim != null else 0.8
	racket.play_swing_callbacks(serve_type, serve_duration, on_contact, Callable(self, "_on_swing_finished"))
	return true

func _on_swing_finished() -> void:
	is_swinging = false
	if footwork != null:
		footwork.start_recovery()

# --- Movement (input / AI) --------------------------------------------

## Joystick input from the user (world-space XZ). See PlayerMovement.set_move_input.
func set_move_input(input: Vector2) -> void:
	if movement != null:
		movement.set_move_input(input)

## AI steering: move toward a court position. Footwork slows the player into small adjustment steps near the target.
func move_towards(target: Vector3, speed_factor: float) -> void:
	if movement == null:
		return
	var factor := speed_factor
	if footwork != null:
		factor *= footwork.adjustment_speed_factor(target)
	movement.move_towards(target, factor)
	# The Run pose is switched on by _process from the actual movement.

## Called when the opponent has just hit: split step.
func notify_opponent_hit() -> void:
	if footwork != null:
		footwork.on_opponent_hit()

# --- Point lifecycle / reactions --------------------------------------

## Reset for a new point: teleport to the given position (point
## resets are the only teleport allowed), face the net, recover a
## little stamina, return to the Ready pose.
func reset_for_point(position: Vector3) -> void:
	home_position = position
	# Local position, not global: setup() may run before the players
	# enter the tree (global_position errors outside the tree), and
	# the parent (GameBootstrap) has an identity transform, so local
	# == global here.
	position = position
	var face_dir := Vector3(0, 0, 1) if player_index == 0 else Vector3(0, 0, -1)
	rotation.y = atan2(-face_dir.x, -face_dir.z)
	if movement != null:
		movement.teleport(position)
	is_swinging = false
	if anim != null:
		anim.play_loop("Ready")
	if stamina != null:
		stamina.restore(6.0)

func play_celebrate() -> void:
	if anim == null:
		return
	anim.play_clip("Celebrate", 1.0, Callable(), Callable())

func play_disappointed() -> void:
	if anim == null:
		return
	anim.play_clip("Disappointed", 1.0, Callable(), Callable())

func set_intensity(mode: int) -> void:
	if stamina != null:
		stamina.mode = mode

func _current_intensity() -> int:
	return stamina.mode if stamina != null else PlayerStamina.IntensityMode.DEFAULT

# --- Quality evaluation -------------------------------------------------

## Quality from contact timing + position + stamina. Timing error is
## derived from the distance to the ideal contact point and whether
## the shuttle is still approaching (Early) or already going past
## (Late). Poor height or low stamina degrades the result one step.
func _evaluate_quality(shot_type: int, shuttle_pos: Vector3, distance: float) -> int:
	var contact := racket.contact_point() if racket != null else global_position + Vector3.UP * 1.2
	var to_racket := contact - shuttle_pos
	var approach := 0.0
	if to_racket.length_squared() > 0.000001:
		approach = shuttle.velocity.dot(to_racket.normalized())

	# ~0.25 s of error per metre away from the ideal 0.35 m contact.
	var error := (distance - 0.35) * 0.25
	error = -absf(error) if approach >= 0.0 else absf(error)
	var quality := ShotData.ShotTiming.from_timing_error(error)

	var profile: ShotData.ShotProfile = ShotData.ShotLibrary.get_profile(shot_type)
	if shuttle_pos.y < profile.min_shuttle_height or shuttle_pos.y > profile.max_shuttle_height:
		quality = _degrade(quality)

	if stamina != null and stamina.power_factor() < 0.9:
		quality = _degrade(quality)

	return quality

func _degrade(q: int) -> int:
	match q:
		ShotData.ShotQuality.PERFECT:
			return ShotData.ShotQuality.GOOD
		ShotData.ShotQuality.GOOD:
			return ShotData.ShotQuality.LATE
		ShotData.ShotQuality.LATE:
			return ShotData.ShotQuality.MISS
		ShotData.ShotQuality.EARLY:
			return ShotData.ShotQuality.MISS
		_:
			return ShotData.ShotQuality.MISS

## Swing duration (s) for a shot type: its clip duration.
func _swing_duration(shot_type: int) -> float:
	if anim == null:
		return 0.5
	return anim.get_clip_duration(RacketController.clip_name_for_shot(shot_type))
