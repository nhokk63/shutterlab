class_name CameraManager
extends Node
## 6 camera presets, smooth-lerped position + rotation (5/s),
## clamped y >= 0.5. The default preset is read from GameState.
## DynamicFollow tracks the shuttle; BehindPlayer raises the camera
## partly with the shuttle height when it flies high.
## Port of ShuttleLab.Camera.CameraManager (Unity C#).
##
## Godot difference: this Node drives an assigned Camera3D instead of
## living on the camera GameObject; call set_camera() after the scene
## camera exists (GameBootstrap does this).

enum CamPreset { BEHIND_PLAYER, ELEVATED, BROADCAST, DYNAMIC_FOLLOW, SIDE, TACTICAL }

var current_preset: int = CamPreset.BEHIND_PLAYER

## Lerp speed (1/s).
var follow_speed: float = 5.0

var _camera: Camera3D
var _shuttle: Node3D
var _positioned: bool = false

func set_camera(camera: Camera3D) -> void:
	_camera = camera

func set_preset(preset: int) -> void:
	current_preset = clampi(preset, 0, 5)

func set_targets(shuttle: Node3D) -> void:
	_shuttle = shuttle

func snap_to_preset() -> void:
	_positioned = false

func _process(delta: float) -> void:
	if _camera == null:
		return

	# GDScript has no out parameters; desired() returns the pair.
	var d := desired()
	var desired_pos: Vector3 = d["pos"]
	var look_at: Vector3 = d["look_at"]

	# Never clip through the floor: never below 0.5 m.
	desired_pos.y = maxf(desired_pos.y, 0.5)

	var to_look := look_at - desired_pos
	var desired_basis := _camera.global_transform.basis
	if to_look.length_squared() > 0.01:
		desired_basis = Basis.looking_at(to_look.normalized(), Vector3.UP)

	if not _positioned:
		_camera.global_position = desired_pos
		_camera.global_transform.basis = desired_basis
		_positioned = true
		return

	var t := 1.0 - exp(-follow_speed * delta)
	_camera.global_position = _camera.global_position.lerp(desired_pos, t)
	_camera.global_transform.basis = _camera.global_transform.basis.slerp(desired_basis, t)

## Desired camera position + look-at for the current preset.
func desired() -> Dictionary:
	var pos := Vector3.ZERO
	var look_at := Vector3.ZERO
	match current_preset:
		CamPreset.BEHIND_PLAYER:
			# Low, flat, broadcast-style view like the reference image
			# (device feedback 2026-11-10: the old high camera made the
			# court look like a steep trapezoid). Camera just above the
			# player's head, looking almost level — the court reads flat
			# and the players are large.
			pos = Vector3(0, 2.2, -8.5)
			look_at = Vector3(0, 1.4, 2.5)
			# Shuttle flying high lifts the camera a little.
			pos.y += _shuttle_lift()
		CamPreset.ELEVATED:
			pos = Vector3(0, 10, -8)
			look_at = Vector3.ZERO
		CamPreset.BROADCAST:
			pos = Vector3(7, 5, -9)
			look_at = Vector3(0, 0.5, 1)
		CamPreset.DYNAMIC_FOLLOW:
			if _shuttle != null:
				pos = _shuttle.global_position + Vector3(0, 4, -7)
				look_at = _shuttle.global_position
			else:
				pos = Vector3(0, 2.2, -8.5)
				look_at = Vector3(0, 1.4, 2.5)
		CamPreset.SIDE:
			pos = Vector3(10, 3, 0)
			look_at = Vector3(0, 1, 0)
		CamPreset.TACTICAL:  # almost straight down at the court
			pos = Vector3(0, 13, -2)
			look_at = Vector3.ZERO
		_:
			pos = Vector3(0, 2.2, -8.5)
			look_at = Vector3(0, 1.4, 2.5)
	return {"pos": pos, "look_at": look_at}

## Camera lift from shuttle height (BehindPlayer): 30% of the height
## above 2 m, at most +2.5 m. A low shuttle does not lift it.
func _shuttle_lift() -> float:
	if _shuttle == null:
		return 0.0
	return clampf((_shuttle.global_position.y - 2.0) * 0.3, 0.0, 2.5)
