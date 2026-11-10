class_name MobileInputManager
extends Node
## Touch + keyboard controls (keyboard is the fallback for desktop testing).
## Movement source: VirtualJoystick (touch) or WASD / arrow keys.
## Shot source: ShotButton (swipe to aim) or keys J/K/L/U/I/O.
## The joystick and the shot buttons are independent UI controls, so
## multi-touch works without locking each other.
## NOTE: this file lives in the input layer — keyboard input is read
## through Godot's Input singleton (is_key_pressed / _unhandled_input).
## Joystick dead zone 0.15 is applied in set_joystick (the single funnel
## for all movement sources); VirtualJoystick only sends the raw
## clamped -1..1 vector.
## Port of ShuttleLab.Input.MobileInputManager (Unity C#).

const JOYSTICK_DEAD_ZONE := 0.15

## Current movement vector (x = left/right, y = forward/back ~ z).
var move_input: Vector2 = Vector2.ZERO

## Shot type + swipe direction (world-space XZ vector, normalised).
signal shot_requested(shot_type: int, swipe_dir: Vector2)
signal serve_requested
signal pause_requested

## Left / right hand panels (swapped in LeftHandMode).
var joystick_panel: Control
var shot_buttons_panel: Control

## Touch controls layer (built in code when the scene has no panels).
var _controls_layer: CanvasLayer

var _user: PlayerController
var _match: MatchManager
var _joystick_input: Vector2 = Vector2.ZERO

var _joy_anchor_left: float = 0.0
var _joy_anchor_right: float = 0.0
var _btn_anchor_left: float = 0.0
var _btn_anchor_right: float = 0.0
var _joy_pos_x: float = 0.0
var _btn_pos_x: float = 0.0
var _anchors_stored: bool = false

func bind(user: PlayerController, match: MatchManager) -> void:
	_user = user
	_match = match

func _ready() -> void:
	if joystick_panel == null:
		joystick_panel = get_node_or_null("JoystickPanel") as Control
	if shot_buttons_panel == null:
		shot_buttons_panel = get_node_or_null("ShotButtonsPanel") as Control
	# BUG (found on device 2026-11-10): main.tscn has NO JoystickPanel /
	# ShotButtonsPanel, and nothing ever created them — so the game
	# shipped with zero touch controls (no joystick, no shot buttons).
	# Build them here in code when the scene does not provide them.
	if joystick_panel == null or shot_buttons_panel == null:
		_build_touch_controls()

## Show / hide the touch controls. main.gd shows them during a match
## and hides them in the menu, while paused, and on the results screen.
func set_controls_visible(v: bool) -> void:
	if _controls_layer != null:
		_controls_layer.visible = v

## Build the joystick + shot buttons in code (Unity-style placement via
## UITheme.place, y-up, 1280x720 reference). Joystick bottom-left,
## shot buttons bottom-right, both 90 px above the bottom edge so they
## clear the stamina bars at the very bottom of the screen.
func _build_touch_controls() -> void:
	var layer := CanvasLayer.new()
	layer.name = "TouchControls"
	layer.layer = 10
	add_child(layer)
	_controls_layer = layer
	_controls_layer.visible = false  # shown by main.gd when a match starts

	# --- Joystick (bottom-left) ---
	joystick_panel = Control.new()
	joystick_panel.name = "JoystickPanel"
	UITheme.place(joystick_panel, Vector2.ZERO, Vector2.ZERO,
		Vector2(150, 210), Vector2(240, 240))
	layer.add_child(joystick_panel)
	var joy := VirtualJoystick.new()
	joy.name = "Joystick"
	joy.input_manager = self
	joy.set_anchors_preset(Control.PRESET_FULL_RECT)
	joystick_panel.add_child(joy)

	# --- Shot buttons (bottom-right, 2 cols x 3 rows) ---
	shot_buttons_panel = Control.new()
	shot_buttons_panel.name = "ShotButtonsPanel"
	UITheme.place(shot_buttons_panel, Vector2(1, 0), Vector2(1, 0),
		Vector2(-170, 210), Vector2(280, 240))
	layer.add_child(shot_buttons_panel)
	var grid := GridContainer.new()
	grid.name = "ShotGrid"
	grid.columns = 2
	grid.set_anchors_preset(Control.PRESET_FULL_RECT)
	grid.add_theme_constant_override("h_separation", 12)
	grid.add_theme_constant_override("v_separation", 12)
	shot_buttons_panel.add_child(grid)
	_add_shot_button(grid, "Cao", ShotData.ShotType.CLEAR, false)
	_add_shot_button(grid, "Nhỏ", ShotData.ShotType.DROP, false)
	_add_shot_button(grid, "Đập", ShotData.ShotType.SMASH, false)
	_add_shot_button(grid, "Drive", ShotData.ShotType.DRIVE, false)
	_add_shot_button(grid, "Lưới", ShotData.ShotType.NET_SHOT, false)
	_add_shot_button(grid, "Giao", ShotData.ShotType.SERVE_LOW, true)

func _add_shot_button(parent: Control, text: String, shot_type: int, is_serve: bool) -> void:
	var b := ShotButton.new()
	b.name = "Shot_%s" % text
	b.text = text
	b.shot_type = shot_type
	b.is_serve_button = is_serve
	b.input_manager = self
	b.custom_minimum_size = Vector2(128, 68)
	# Serve button is green so it is easy to find when serving.
	b.add_theme_stylebox_override("normal",
		UITheme.style(UITheme.BUTTON_SELECTED if is_serve else UITheme.BUTTON_NORMAL))
	b.add_theme_stylebox_override("hover", UITheme.style(UITheme.BUTTON_HOVER))
	b.add_theme_stylebox_override("pressed", UITheme.style(UITheme.BUTTON_SELECTED))
	b.add_theme_stylebox_override("focus", StyleBoxEmpty.new())
	b.add_theme_color_override("font_color", UITheme.TEXT_WHITE)
	b.add_theme_font_size_override("font_size", 24)
	parent.add_child(b)

func _process(_delta: float) -> void:
	if _user == null or _match == null:
		return

	var keyboard := _read_keyboard_move()
	var move := keyboard if keyboard.length_squared() > 0.01 else _joystick_input
	if move.length_squared() > 1.0:
		move = move.normalized()
	move_input = move

	# Only drive the player while a point is live; stand still otherwise.
	if _match.phase == MatchManager.Phase.RALLY or _match.phase == MatchManager.Phase.SERVING:
		_user.set_move_input(move)
	else:
		_user.set_move_input(Vector2.ZERO)

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		var k := event as InputEventKey
		match k.keycode:
			KEY_J:
				_fire_key_shot(ShotData.ShotType.CLEAR)
			KEY_K:
				_fire_key_shot(ShotData.ShotType.SMASH)
			KEY_L:
				_fire_key_shot(ShotData.ShotType.DROP)
			KEY_U:
				_fire_key_shot(ShotData.ShotType.DRIVE)
			KEY_I:
				_fire_key_shot(ShotData.ShotType.LIFT)
			KEY_O:
				_fire_key_shot(ShotData.ShotType.NET_SHOT)
			KEY_SPACE:
				request_serve()
			KEY_ESCAPE, KEY_P:
				pause_requested.emit()

# --- API for the UI (VirtualJoystick / ShotButton) -----------------------

## Joystick sends the raw vector (-1..1); dead zone + rescale here.
func set_joystick(v: Vector2) -> void:
	var mag := v.length()
	if mag < JOYSTICK_DEAD_ZONE:
		_joystick_input = Vector2.ZERO
		return
	if mag > 1.0:
		v /= mag
		mag = 1.0
	# Rescale so the range after the dead zone still spans 0..1.
	_joystick_input = v.normalized() * ((mag - JOYSTICK_DEAD_ZONE) / (1.0 - JOYSTICK_DEAD_ZONE))

func request_shot(shot_type: int, swipe_dir: Vector2) -> void:
	if swipe_dir.length_squared() < 0.01:
		swipe_dir = get_default_shot_direction()
	shot_requested.emit(shot_type, swipe_dir.normalized())

func request_serve() -> void:
	serve_requested.emit()

## Default direction when a shot button is tapped without swiping:
## the far corner opposite the opponent's current position
## (MatchManager.players[1]).
func get_default_shot_direction() -> Vector2:
	if _match == null or _match.players == null or _match.players.size() < 2 or _match.players[1] == null:
		return Vector2(0, 1)  # straight deep

	var opp: Vector3 = (_match.players[1] as PlayerController).global_position
	var side := -1.0 if opp.x >= 0.0 else 1.0  # aim opposite the opponent
	var corner := Vector2(
		side * (CourtDimensions.SINGLES_HALF_WIDTH - 0.4),
		CourtDimensions.COURT_HALF_LENGTH - 0.5)
	var me: Vector3 = _user.global_position if _user != null else Vector3.ZERO
	var d := corner - Vector2(me.x, me.z)
	return d.normalized() if d.length_squared() > 0.01 else Vector2(0, 1)

# --- Keyboard ---------------------------------------------------------

func _read_keyboard_move() -> Vector2:
	var x := 0.0
	var y := 0.0
	if Input.is_key_pressed(KEY_A) or Input.is_key_pressed(KEY_LEFT):
		x -= 1.0
	if Input.is_key_pressed(KEY_D) or Input.is_key_pressed(KEY_RIGHT):
		x += 1.0
	if Input.is_key_pressed(KEY_W) or Input.is_key_pressed(KEY_UP):
		y += 1.0
	if Input.is_key_pressed(KEY_S) or Input.is_key_pressed(KEY_DOWN):
		y -= 1.0
	var v := Vector2(x, y)
	return v.normalized() if v.length_squared() > 1.0 else v

func _fire_key_shot(shot_type: int) -> void:
	shot_requested.emit(shot_type, get_default_shot_direction())

# --- Left hand ---------------------------------------------------------

## LeftHandMode: swap the horizontal positions of the joystick panel
## and the shot-buttons panel (swap anchors / anchored positions).
## This is the only owner of the left-hand layout — do not move the
## joystick anywhere else or it will be shifted twice.
func apply_left_hand_mode(left: bool) -> void:
	if joystick_panel == null or shot_buttons_panel == null:
		return

	if not _anchors_stored:
		_anchors_stored = true
		_joy_anchor_left = joystick_panel.anchor_left
		_joy_anchor_right = joystick_panel.anchor_right
		_btn_anchor_left = shot_buttons_panel.anchor_left
		_btn_anchor_right = shot_buttons_panel.anchor_right
		_joy_pos_x = joystick_panel.position.x
		_btn_pos_x = shot_buttons_panel.position.x

	if left:
		_set_horizontal_anchors(joystick_panel, _btn_anchor_left, _btn_anchor_right, -_joy_pos_x)
		_set_horizontal_anchors(shot_buttons_panel, _joy_anchor_left, _joy_anchor_right, -_btn_pos_x)
	else:
		_set_horizontal_anchors(joystick_panel, _joy_anchor_left, _joy_anchor_right, _joy_pos_x)
		_set_horizontal_anchors(shot_buttons_panel, _btn_anchor_left, _btn_anchor_right, _btn_pos_x)

func _set_horizontal_anchors(panel: Control, min_x: float, max_x: float, pos_x: float) -> void:
	panel.anchor_left = min_x
	panel.anchor_right = max_x
	panel.position.x = pos_x
