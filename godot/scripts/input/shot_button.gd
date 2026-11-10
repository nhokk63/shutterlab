class_name ShotButton
extends Button
## Shot button: press down to record the start point, drag to aim
## (an arrow rotates with the swipe), release to hit.
## - Swipe > 30 px: the shot direction is the normalised swipe vector.
## - No swipe (tap and release): default direction toward the far
##   corner opposite the opponent.
## - Screen -> world XZ: swipe up = deep (+z, toward the far end),
##   swipe down = near the net, left/right = -x/+x
##   (behind-the-user camera view).
## - A dedicated serve button (is_serve_button): tap = request_serve,
##   no swipe.
## - Each touch fires at most once (_fired).
## Port of ShuttleLab.Input.ShotButton (Unity C#).
##
## Godot differences: Unity's version is a MonoBehaviour with
## IPointerDown/Up/Drag on a UI Button (do not add Button.onClick
## handlers for these buttons anywhere else — the pointer handlers
## here are the only activation mechanism); here the Button subclass
## handles gui_input itself and draws the aim arrow in _draw().

## The aim arrow lives in a child Control, NOT in the Button's own
## _draw(): a Button subclass cannot super._draw() (the parent's
## virtual _draw is not callable from GDScript — parse error), and
## drawing on the Button itself would fight the theme stylebox.
class AimArrow:
	extends Control
	var aim_dir: Vector2 = Vector2.ZERO

	func _draw() -> void:
		if aim_dir.length_squared() <= 0.01:
			return
		var center := size * 0.5
		# aim_dir is in screen coords (y down); draw it as-is.
		var end := center + aim_dir * 56.0
		draw_line(center, end, Color(1, 1, 1, 0.9), 4.0)
		# Arrowhead.
		var dir := (end - center).normalized()
		var perp := Vector2(-dir.y, dir.x)
		draw_colored_polygon(
			PackedVector2Array([end, end - dir * 14.0 + perp * 8.0, end - dir * 14.0 - perp * 8.0]),
			Color(1, 1, 1, 0.9))

const MIN_SWIPE_PX := 30.0

var shot_type: int = ShotData.ShotType.CLEAR

## Dedicated serve button: tap = request_serve, no swipe.
var is_serve_button: bool = false

var input_manager: MobileInputManager

var _down_pos: Vector2 = Vector2.ZERO
var _tracking: bool = false
var _fired: bool = false
var _aim_dir: Vector2 = Vector2.ZERO  # normalised swipe, while aiming
var _arrow: AimArrow

func _ready() -> void:
	# Buttons are focusable by default; keep keyboard focus off the
	# touch buttons so Space does not re-trigger them.
	focus_mode = Control.FOCUS_NONE
	_arrow = AimArrow.new()
	_arrow.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_arrow.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(_arrow)

func _gui_input(event: InputEvent) -> void:
	if event is InputEventScreenTouch:
		var touch := event as InputEventScreenTouch
		if touch.pressed:
			_on_press(touch.position)
		else:
			_on_release(touch.position)
	elif event is InputEventScreenDrag:
		var drag := event as InputEventScreenDrag
		_on_drag(drag.position)
	elif event is InputEventMouseButton:
		var mb := event as InputEventMouseButton
		if mb.button_index != MOUSE_BUTTON_LEFT:
			return
		if mb.pressed:
			_on_press(mb.position)
		else:
			_on_release(mb.position)
	elif event is InputEventMouseMotion:
		if _tracking:
			_on_drag((event as InputEventMouseMotion).position)

func _set_aim(dir: Vector2) -> void:
	_aim_dir = dir
	if _arrow != null:
		_arrow.aim_dir = dir if _tracking and not is_serve_button else Vector2.ZERO
		_arrow.queue_redraw()

func _on_press(pos: Vector2) -> void:
	_tracking = true
	_fired = false
	_down_pos = pos
	_set_aim(Vector2.ZERO)

	if is_serve_button:
		_fired = true
		if input_manager != null:
			input_manager.request_serve()

func _on_drag(pos: Vector2) -> void:
	if not _tracking or is_serve_button:
		return
	var delta := pos - _down_pos
	if delta.length() < MIN_SWIPE_PX:
		_set_aim(Vector2.ZERO)
		return
	_set_aim(delta.normalized())

func _on_release(pos: Vector2) -> void:
	if not _tracking:
		return
	_tracking = false
	_set_aim(Vector2.ZERO)

	if is_serve_button or _fired:
		return
	_fired = true

	var delta := pos - _down_pos
	var dir: Vector2
	if delta.length() >= MIN_SWIPE_PX:
		# Screen: x right = +x world, y up = +z world (deep).
		dir = Vector2(delta.x, -delta.y).normalized()
	else:
		dir = input_manager.get_default_shot_direction() if input_manager != null else Vector2(0, 1)

	if input_manager != null:
		input_manager.request_shot(shot_type, dir)
