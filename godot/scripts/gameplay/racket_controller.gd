class_name RacketController
extends Node
## Owns the racket: its contact point (racket head from the visual rig)
## and the swing. A swing plays the matching procedural clip on the
## animation controller; the actual shot is executed by ShotController
## from the clip's on_contact callback (wired by PlayerController), so
## the racket never launches the shuttle itself.
## Port of ShuttleLab.Gameplay.RacketController (Unity C#).
##
## Godot difference: the racket head is a Node3D from PlayerVisual
## (no CC0 rig in this port); Bind takes the visual instead of a
## HumanoidRig.

## Centre of the string bed (contact point), from the visual rig.
var racket_head: Node3D

## World position of the string-bed centre.
func contact_point() -> Vector3:
	if racket_head != null:
		return racket_head.global_position
	if get_parent() != null and get_parent() is Node3D:
		return (get_parent() as Node3D).global_position + Vector3.UP * 1.2
	return Vector3.UP * 1.2

var _anim: PlayerAnimation
var _visual: PlayerVisual

## Wire the visual (racket) and the animation controller.
## Called by PlayerController.init.
func bind(visual: PlayerVisual, anim: PlayerAnimation) -> void:
	_visual = visual
	_anim = anim
	racket_head = visual.racket_head if visual != null else null

## Play the swing for a shot type over the given duration (seconds).
func play_swing(shot_type: int, duration: float) -> void:
	play_swing_callbacks(shot_type, duration, Callable(), Callable())

## Play the swing; on_contact fires at the clip's ContactTime,
## on_end when the swing finishes.
func play_swing_callbacks(shot_type: int, duration: float, on_contact: Callable, on_end: Callable) -> void:
	if _anim == null:
		if on_contact.is_valid():
			on_contact.call()
		if on_end.is_valid():
			on_end.call()
		return

	var clip_name := clip_name_for_shot(shot_type)
	if _anim.get_clip_duration(clip_name) <= 0.0:
		if on_contact.is_valid():
			on_contact.call()
		if on_end.is_valid():
			on_end.call()
		return

	# Scale playback so the swing lasts `duration` seconds. Each shot
	# type still has its own distinct clip — speed only stretches it.
	var clip_duration := _anim.get_clip_duration(clip_name)
	var speed := clip_duration / duration if duration > 0.01 else 1.0
	_anim.play_clip(clip_name, speed, on_contact, on_end)

## Is the shuttle inside the contact window? Within 0.45 m of the
## string bed AND in front of the racket face (positive dot with the
## racket-head forward, i.e. the string-bed normal).
func is_in_contact_window(shuttle_pos: Vector3) -> bool:
	if racket_head == null:
		return false
	var to_shuttle := shuttle_pos - racket_head.global_position
	if to_shuttle.length() > 0.45:
		return false
	if to_shuttle.length_squared() < 0.000001:
		return true
	return to_shuttle.normalized().dot(-racket_head.global_transform.basis.z) > 0.0

## Map a shot type to its animation clip (nearest neighbour).
static func clip_name_for_shot(shot_type: int) -> String:
	match shot_type:
		ShotData.ShotType.SMASH, ShotData.ShotType.JUMP_SMASH, ShotData.ShotType.HALF_SMASH:
			return "Smash"
		ShotData.ShotType.CLEAR, ShotData.ShotType.LIFT, ShotData.ShotType.DRIVE:
			return "Clear"
		ShotData.ShotType.DROP:
			return "Drop"
		ShotData.ShotType.NET_SHOT, ShotData.ShotType.NET_KILL, ShotData.ShotType.PUSH:
			return "NetShot"
		ShotData.ShotType.BLOCK, ShotData.ShotType.DEFENSIVE_DRIVE:
			return "Block"
		ShotData.ShotType.SERVE_LOW, ShotData.ShotType.SERVE_HIGH, ShotData.ShotType.SERVE_FLICK:
			return "Serve"
		_:
			return "Clear"
