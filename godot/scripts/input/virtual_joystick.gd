class_name VirtualJoystick
extends Control
## Touch joystick drawn in code (background disc + knob): computes the
## vector from the centre to the finger, clamps it to the radius, and
## hands the raw vector (-1..1) to MobileInputManager.
## Dead zone 0.15 + rescale are applied in MobileInputManager.set_joystick
## (the single funnel) so the dead zone is never subtracted twice.
## The knob follows the finger and snaps back to centre on release.
## Multi-touch: the joystick only accepts the pointer that touched it;
## the shot buttons work independently.
## Port of ShuttleLab.Input.VirtualJoystick (Unity C#).
##
## Godot differences: Unity's version is a RectTransform with a child
## knob Image driven by EventTrigger; here the control draws itself in
## _draw() and handles InputEventScreenTouch / InputEventScreenDrag
## (plus mouse events, so the joystick is testable on desktop).

var input_manager: MobileInputManager

var _pointer_index: int = -1  # touch index (or -1 for mouse)
var _using_mouse: bool = false
var _knob_offset: Vector2 = Vector2.ZERO

func _ready() -> void:
	# A fixed size keeps the radius well-defined; the parent can resize.
	if size == Vector2.ZERO:
		custom_minimum_size = Vector2(220, 220)

func _draw() -> void:
	var center := size * 0.5
	var radius := minf(size.x, size.y) * 0.5
	# Background disc.
	draw_circle(center, radius, Color(1, 1, 1, 0.08))
	draw_arc(center, radius, 0.0, TAU, 48, Color(1, 1, 1, 0.35), 2.0)
	# Knob.
	var knob_radius := radius * 0.42
	draw_circle(center + _knob_offset, knob_radius, Color(1, 1, 1, 0.35))
	draw_arc(center + _knob_offset, knob_radius, 0.0, TAU, 32, Color(1, 1, 1, 0.6), 2.0)

func _gui_input(event: InputEvent) -> void:
	if event is InputEventScreenTouch:
		var touch := event as InputEventScreenTouch
		if touch.pressed:
			if _pointer_index != -1:
				return
			_pointer_index = touch.index
			_using_mouse = false
			_update_knob(touch.position)
		else:
			if touch.index != _pointer_index:
				return
			_pointer_index = -1
			_release()
	elif event is InputEventScreenDrag:
		var drag := event as InputEventScreenDrag
		if drag.index != _pointer_index:
			return
		_update_knob(drag.position)
	elif event is InputEventMouseButton:
		var mb := event as InputEventMouseButton
		if mb.button_index != MOUSE_BUTTON_LEFT:
			return
		if mb.pressed:
			if _pointer_index != -1:
				return
			_pointer_index = -2  # mouse sentinel
			_using_mouse = true
			_update_knob(mb.position)
		else:
			if _pointer_index != -2:
				return
			_pointer_index = -1
			_release()
	elif event is InputEventMouseMotion:
		if _pointer_index != -2:
			return
		var mm := event as InputEventMouseMotion
		_update_knob(mm.position)

func _update_knob(screen_pos: Vector2) -> void:
	var local := screen_pos - global_position - size * 0.5
	var max_radius := minf(size.x, size.y) * 0.5
	if max_radius <= 1.0:
		return

	var clamped := local.limit_length(max_radius)
	_knob_offset = clamped
	queue_redraw()

	# x -> world x, y (down on screen) -> -z world (toward the net is
	# +y in the joystick vector, matching Unity's screen-up = +z).
	# Control y grows downward, so invert y.
	if input_manager != null:
		input_manager.set_joystick(Vector2(clamped.x / max_radius, -clamped.y / max_radius))

func _release() -> void:
	_knob_offset = Vector2.ZERO
	queue_redraw()
	if input_manager != null:
		input_manager.set_joystick(Vector2.ZERO)
