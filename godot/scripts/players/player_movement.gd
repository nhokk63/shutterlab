class_name PlayerMovement
extends Node
## 360-degree court movement with acceleration/inertia: no teleporting,
## no instant direction changes. Runs in _physics_process. Clamped to the
## singles court (CourtDimensions). Facing turns toward the movement
## direction, or toward the shuttle when standing still, or toward the
## net otherwise. This component only moves where it is told to go
## (input / move_towards): it never picks a target by itself.
## Port of ShuttleLab.Players.PlayerMovement (Unity C#).
##
## Godot difference: this is a child Node of the player Node3D
## (owner_node) instead of a component on the same GameObject; it moves
## owner_node's global transform.

var acceleration: float = 14.0  # m/s^2 toward the desired velocity
var deceleration: float = 12.0  # m/s^2 when no input (inertia)
var turn_speed: float = 10.0  # rad/s facing turn rate
var jump_velocity: float = 6.0  # m/s initial vertical speed
var gravity: float = 20.0  # m/s^2 (snappy arcade feel)
var lunge_multiplier: float = 1.6  # speed burst while lunging
var lunge_duration: float = 0.25  # seconds
var lunge_cooldown: float = 0.8  # seconds

## External speed multiplier (set by PlayerController from
## PlayerStamina.speed_factor()). 1 = full speed.
var speed_factor: float = 1.0

## -1 = near side, faces +z (toward the net). +1 = far side, faces -z.
var side_sign: int = -1

## Current shuttle position, for idle facing. Set by PlayerController.
var shuttle_position: Vector3 = Vector3.ZERO

## Whether shuttle_position is meaningful right now.
var has_shuttle_position: bool = false

var _owner_node: Node3D
var _stats: PlayerStats
var _move_input: Vector2 = Vector2.ZERO  # world-space XZ, magnitude 0..1
var _move_target: Vector3 = Vector3.ZERO
var _has_move_target: bool = false
var _move_target_speed_factor: float = 1.0
var _velocity: Vector3 = Vector3.ZERO  # horizontal velocity (y unused)
var _height: float = 0.0  # feet height above the court
var _vertical_velocity: float = 0.0
var _is_airborne: bool = false
var _lunge_timer: float = 0.0
var _lunge_cooldown_timer: float = 0.0
var _lunge_dir: Vector3 = Vector3.FORWARD

func max_speed() -> float:
	return (_stats.move_speed() if _stats != null else 6.0) * speed_factor

func velocity() -> Vector3:
	return _velocity

## Last joystick input (world-space XZ, magnitude 0..1).
func move_input() -> Vector2:
	return _move_input

func is_moving() -> bool:
	return Vector2(_velocity.x, _velocity.z).length() > 0.3

func is_airborne() -> bool:
	return _is_airborne

func is_lunging() -> bool:
	return _lunge_timer > 0.0

func has_move_target() -> bool:
	return _has_move_target

## Current world position of the player this movement drives.
func owner_position() -> Vector3:
	return _owner_node.global_position if _owner_node != null else Vector3.ZERO

func init(stats: PlayerStats, p_side_sign: int, owner_node: Node3D) -> void:
	_stats = stats
	side_sign = -1 if p_side_sign < 0 else 1
	_owner_node = owner_node

## Joystick input, world-space XZ: x = strafe, y = toward the net
## (+z for the near player). The input layer is responsible for
## mirroring for the far side if it uses screen-space input.
## Clears any move_towards target.
func set_move_input(input: Vector2) -> void:
	_move_input = input.limit_length(1.0)
	_has_move_target = false

## Steer toward a court position (used by AI / recovery).
func move_towards(target: Vector3, p_speed_factor: float) -> void:
	_move_target = CourtDimensions.clamp_to_court(target, false)
	_move_target_speed_factor = clampf(p_speed_factor, 0.0, 1.0)
	_move_input = Vector2.ZERO
	_has_move_target = true

## Head back to the home (base) position at 0.8x max speed.
func recover_to_home(home_position: Vector3) -> void:
	move_towards(home_position, 0.8)

func stop_moving() -> void:
	_move_input = Vector2.ZERO
	_has_move_target = false

## Short speed burst (1.6x) in the given direction for 0.25 s.
## Ignored while on cooldown.
func lunge(dir: Vector3) -> void:
	if _lunge_cooldown_timer > 0.0:
		return
	var d := Vector3(dir.x, 0.0, dir.z)
	if d.length_squared() < 0.0001:
		return
	_lunge_dir = d.normalized()
	_lunge_timer = lunge_duration
	_lunge_cooldown_timer = lunge_cooldown

## Full jump (e.g. jump smash). Ignored while airborne.
func jump() -> void:
	if _is_airborne:
		return
	_is_airborne = true
	_vertical_velocity = jump_velocity

## Split step: a tiny hop in place (called by FootworkController when
## the opponent has just hit). Much smaller than jump().
func split_step() -> void:
	if _is_airborne:
		return
	_is_airborne = true
	_vertical_velocity = 1.4

## Hard reposition (point reset only — never used for rally movement).
## Local position: the owner sits under GameBootstrap (identity
## transform), and global_position errors if the node is not in the
## tree yet (e.g. setup during SceneTree._initialize).
func teleport(position: Vector3) -> void:
	_owner_node.position = Vector3(position.x, 0.0, position.z)
	_velocity = Vector3.ZERO
	_height = 0.0
	_is_airborne = false
	_vertical_velocity = 0.0
	_has_move_target = false
	_move_input = Vector2.ZERO
	_lunge_timer = 0.0

func reset_state() -> void:
	_velocity = Vector3.ZERO
	_height = 0.0
	_is_airborne = false
	_vertical_velocity = 0.0
	_has_move_target = false
	_move_input = Vector2.ZERO
	_lunge_timer = 0.0
	_lunge_cooldown_timer = 0.0

func _physics_process(delta: float) -> void:
	if _owner_node == null:
		return
	if _lunge_cooldown_timer > 0.0:
		_lunge_cooldown_timer -= delta
	if _lunge_timer > 0.0:
		_lunge_timer -= delta

	# --- Desired horizontal velocity --------------------------------
	var desired := Vector3.ZERO
	if _lunge_timer > 0.0:
		desired = _lunge_dir * max_speed() * lunge_multiplier
	elif _has_move_target:
		var to_target := _move_target - _owner_node.global_position
		to_target.y = 0.0
		if to_target.length() < 0.15:
			# Arrived.
			_has_move_target = false
		else:
			desired = to_target.normalized() * max_speed() * _move_target_speed_factor
	elif _move_input.length_squared() > 0.0001:
		desired = Vector3(_move_input.x, 0.0, _move_input.y) * max_speed()

	# Accelerate toward the desired velocity; decelerate with inertia
	# when there is no desired velocity. The accel limit is what makes
	# direction changes take time instead of snapping.
	var rate := acceleration if desired.length_squared() > 0.0001 else deceleration
	_velocity = _velocity.move_toward(desired, rate * delta)

	var pos := _owner_node.global_position
	pos += _velocity * delta
	pos = CourtDimensions.clamp_to_court(pos, false)

	# --- Vertical (jump / split step) --------------------------------
	if _is_airborne:
		_vertical_velocity -= gravity * delta
		_height += _vertical_velocity * delta
		if _height <= 0.0:
			_height = 0.0
			_vertical_velocity = 0.0
			_is_airborne = false
	pos.y = _height
	_owner_node.global_position = pos

	_update_facing(delta)

func _update_facing(delta: float) -> void:
	var face_dir: Vector3
	var flat_vel := Vector2(_velocity.x, _velocity.z)
	if flat_vel.length() > 0.5:
		face_dir = Vector3(_velocity.x, 0.0, _velocity.z).normalized()
	elif has_shuttle_position:
		var to_shuttle := shuttle_position - _owner_node.global_position
		to_shuttle.y = 0.0
		face_dir = to_shuttle.normalized() if to_shuttle.length_squared() > 0.01 else Vector3(0, 0, -side_sign)
	else:
		# Face the net.
		face_dir = Vector3(0, 0, -side_sign)

	# Node3D faces -Z; yaw so -Z points along face_dir.
	var target_yaw := atan2(-face_dir.x, -face_dir.z)
	_owner_node.rotation.y = lerp_angle(_owner_node.rotation.y, target_yaw, clampf(turn_speed * delta, 0.0, 1.0))
