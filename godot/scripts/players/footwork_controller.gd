class_name FootworkController
extends RefCounted
## Plain class (not a Node) owned by PlayerController. Handles the three
## footwork behaviours from the architecture doc:
##   - split step: a tiny hop in place the moment the opponent hits
##     (0.12 s window, gives the player a loaded, ready stance);
##   - recovery step: walk back to HomePosition at 0.8x max speed after
##     the player has hit;
##   - adjustment steps: small, slower steps when close to the pickup point.
## Drives PlayerMovement; it never moves the transform itself.
## Port of ShuttleLab.Players.FootworkController (Unity C#).

## How long the split-step "loaded" window lasts.
const SPLIT_STEP_DURATION := 0.12

## Recovery speed as a fraction of max speed.
const RECOVERY_SPEED_FACTOR := 0.8

## Within this distance of the target, use small adjustment steps.
const ADJUSTMENT_STEP_RANGE := 0.6

## Speed factor while taking adjustment steps.
const ADJUSTMENT_SPEED_FACTOR_VALUE := 0.45

var _movement: PlayerMovement
var _home_position: Vector3 = Vector3.ZERO
var _split_timer: float = 0.0
var _recovering: bool = false

func is_split_stepping() -> bool:
	return _split_timer > 0.0

func is_recovering() -> bool:
	return _recovering

func _init(movement: PlayerMovement, home_position: Vector3) -> void:
	_movement = movement
	_home_position = home_position

func set_home_position(home_position: Vector3) -> void:
	_home_position = home_position

## Called the moment the opponent hits the shuttle: split step
## (small hop in place) so the player is loaded and ready to move.
func on_opponent_hit() -> void:
	_split_timer = SPLIT_STEP_DURATION
	if _movement != null:
		_movement.split_step()

## Start walking back to the home (base) position after a hit.
func start_recovery() -> void:
	if _movement == null:
		return
	_recovering = true
	_movement.recover_to_home(_home_position)

## Speed factor for approaching a target: full speed far away, small
## adjustment steps (0.45x) when within AdjustmentStepRange so the
## player does not overshoot the pickup point.
func adjustment_speed_factor(target: Vector3) -> float:
	if _movement == null:
		return 1.0
	var to_target := target - _movement.owner_position()
	to_target.y = 0.0
	return ADJUSTMENT_SPEED_FACTOR_VALUE if to_target.length() < ADJUSTMENT_STEP_RANGE else 1.0

## Called every frame by PlayerController.
func update(dt: float) -> void:
	if _split_timer > 0.0:
		_split_timer -= dt

	if _recovering and _movement != null:
		var to_home := _home_position - _movement.owner_position()
		to_home.y = 0.0
		if to_home.length() < 0.15 or not _movement.has_move_target():
			_recovering = false
			_movement.stop_moving()
