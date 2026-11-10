class_name ShotController
extends Node
## Executes a shot for a player: solves the launch velocity with
## ShotSolver (from the racket-head contact point) and launches the shuttle.
## If the requested shot is physically unreachable, falls back to the
## height-appropriate shot (Clear / Lift / Drive) and retries once; if that
## also fails, the shot does not happen (caller keeps the whiff animation).
## Shot statistics are published through shot_executed (MatchManager
## subscribes); this component owns no UI.
## Port of ShuttleLab.Gameplay.ShotController (Unity C#).

## Fired after a shot is successfully launched. Args: type, quality.
signal shot_executed(shot_type: int, quality: int)

var smash_count: int = 0
var clear_count: int = 0
var drop_count: int = 0
var net_shot_count: int = 0
var serve_count: int = 0
var total_shots: int = 0

var _shuttle: Shuttlecock
var _owner: PlayerController
var _racket: RacketController

func init(shuttle: Shuttlecock, owner: PlayerController) -> void:
	_shuttle = shuttle
	_owner = owner
	_racket = owner.racket if owner != null else null

## Execute a shot of the given type toward the target with the given
## quality. Returns false when the shot could not be launched at all
## (unsolvable even after the fallback). The shuttle is snapped to
## the racket-head contact point at the contact frame, then launched —
## the solver's start is the racket head position by contract.
func execute_shot(shot_type: int, target: Vector3, quality: int) -> bool:
	if _shuttle == null or _owner == null:
		return false

	var start: Vector3 = _racket.contact_point() if _racket != null else _owner.global_position + Vector3.UP * 1.5
	var wind: Vector3 = _shuttle.wind

	var res: Dictionary = ShotSolver.solve(start, target, shot_type, quality, wind)
	if not res["ok"]:
		# Fallback: height-appropriate shot, same target, one retry.
		# Use the contact-point height (start), not the shuttle's current
		# position (the shuttle has not snapped to the racket yet and can
		# be up to 1.15 m off).
		var fallback := ShotSolver.fallback_shot(start.y)
		if fallback == shot_type:
			return false
		res = ShotSolver.solve(start, target, fallback, quality, wind)
		if not res["ok"]:
			return false
		shot_type = fallback

	_shuttle.global_position = start
	_shuttle.launch(res["launch_velocity"], _owner.player_index, ShotData.ShotLibrary.is_serve(shot_type))

	_count_shot(shot_type)
	shot_executed.emit(shot_type, quality)
	return true

func _count_shot(shot_type: int) -> void:
	total_shots += 1
	match shot_type:
		ShotData.ShotType.SMASH, ShotData.ShotType.JUMP_SMASH, ShotData.ShotType.HALF_SMASH:
			smash_count += 1
		ShotData.ShotType.CLEAR, ShotData.ShotType.LIFT, ShotData.ShotType.DRIVE:
			clear_count += 1
		ShotData.ShotType.DROP:
			drop_count += 1
		ShotData.ShotType.NET_SHOT, ShotData.ShotType.NET_KILL, ShotData.ShotType.PUSH:
			net_shot_count += 1
		ShotData.ShotType.SERVE_LOW, ShotData.ShotType.SERVE_HIGH, ShotData.ShotType.SERVE_FLICK:
			serve_count += 1
