extends CanvasLayer
class_name SettingsPanel
## SettingsPanel — port of Unity UI/SettingsPanel.cs.
## Shared settings panel (opened from the main menu; a match scene can
## host another copy to adjust mid-match):
##  - Music / SFX sliders (0..1), saved immediately.
##  - 6 camera presets -> camera_preset_changed signal (the parent
##    applies it to the camera right away, like Unity's CameraManager).
##  - Left-hand toggle -> left_hand_changed signal (the parent swaps
##    the input panels, like Unity's MobileInputManager).
##  - AI difficulty readout (read-only — difficulty is picked in the
##    main menu).
##  - Graphics: preset Tiết kiệm/Cân bằng/Cao/Tối đa, FPS 30/60/90/120,
##    render scale 0.6-1.0 + live FPS meter -> GameSettings.
##  - Close button (top-right).
##
## HONEST ABOUT MUSIC (same as Unity v0.1.0): there is no music track
## yet, so the music slider stores a value but plays nothing. The panel
## says so in a small note line.

signal closed
signal camera_preset_changed(preset: int)
signal left_hand_changed(left: bool)

var settings: GameSettings = null

var _music_slider: HSlider
var _sfx_slider: HSlider
var _music_value: Label
var _sfx_value: Label
var _left_hand_toggle: CheckButton
var _difficulty_text: Label
var _preset_buttons: Array[Button] = []
var _gfx_preset_buttons: Array[Button] = []
var _fps_buttons: Array[Button] = []
var _gfx_scale_slider: HSlider
var _gfx_scale_text: Label
var _fps_meter_text: Label
var _fps_frames: int = 0
var _fps_window_start_ms: int = 0
var _built := false


func _ready() -> void:
	_build()
	visible = false


func setup(p_settings: GameSettings) -> void:
	settings = p_settings


## Open the panel (values synced from GameSettings first).
func show_panel() -> void:
	_build()
	visible = true
	_refresh_from_settings()
	_fps_frames = 0
	_fps_window_start_ms = Time.get_ticks_msec()


func hide_panel() -> void:
	if settings != null:
		settings.save()
	visible = false
	closed.emit()


func _process(_delta: float) -> void:
	if not visible or _fps_meter_text == null:
		return
	_fps_frames += 1
	var now := Time.get_ticks_msec()
	var elapsed := (now - _fps_window_start_ms) / 1000.0
	if elapsed >= 1.0:
		_fps_meter_text.text = "%d FPS" % int(round(_fps_frames / elapsed))
		_fps_frames = 0
		_fps_window_start_ms = now


# --- Controls ------------------------------------------------------------------

func _on_close_button() -> void:
	hide_panel()


func _on_music_slider(value: float) -> void:
	if settings != null:
		settings.music_volume = value
	if _music_value != null:
		_music_value.text = "%d%%" % int(round(value * 100.0))


func _on_sfx_slider(value: float) -> void:
	if settings != null:
		settings.sfx_volume = value
	if _sfx_value != null:
		_sfx_value.text = "%d%%" % int(round(value * 100.0))


func _on_camera_preset_button(preset: int) -> void:
	var p := clampi(preset, 0, 5)
	if settings != null:
		settings.camera_preset = p
	camera_preset_changed.emit(p)
	_refresh_preset_buttons()


func _on_left_hand_toggle(left: bool) -> void:
	if settings != null:
		settings.left_hand_mode = left
	left_hand_changed.emit(left)


func _on_graphics_preset_button(tier: int) -> void:
	if settings == null:
		return
	settings.apply_tier(tier)
	_refresh_graphics_buttons()
	_refresh_fps_buttons()
	_refresh_scale()


func _on_fps_button(fps: int) -> void:
	if settings == null:
		return
	settings.apply_fps(fps)
	_refresh_fps_buttons()


func _on_graphics_scale_slider(value: float) -> void:
	if settings == null:
		return
	settings.apply_scale(value)
	if _gfx_scale_text != null:
		_gfx_scale_text.text = "%d%%" % int(round(value * 100.0))


# --- Display sync ---------------------------------------------------------------

func _refresh_from_settings() -> void:
	if settings == null:
		return
	if _music_slider != null:
		_music_slider.set_value_no_signal(settings.music_volume)
	if _sfx_slider != null:
		_sfx_slider.set_value_no_signal(settings.sfx_volume)
	if _music_value != null:
		_music_value.text = "%d%%" % int(round(settings.music_volume * 100.0))
	if _sfx_value != null:
		_sfx_value.text = "%d%%" % int(round(settings.sfx_volume * 100.0))
	if _left_hand_toggle != null:
		_left_hand_toggle.set_pressed_no_signal(settings.left_hand_mode)
	if _difficulty_text != null:
		_difficulty_text.text = "Độ khó AI: %s (%d) — chọn ở menu chính" % [
			GameSettings.difficulty_name(settings.difficulty), settings.difficulty]
	_refresh_preset_buttons()
	_refresh_graphics_buttons()
	_refresh_fps_buttons()
	_refresh_scale()


func _refresh_preset_buttons() -> void:
	var current := 0
	if settings != null:
		current = settings.camera_preset
	for i in _preset_buttons.size():
		UITheme.set_button_selected(_preset_buttons[i], i == current)


func _refresh_graphics_buttons() -> void:
	var current := 1
	if settings != null:
		current = settings.gfx_tier if settings.gfx_tier >= 0 else GameSettings.auto_detect_tier()
	for i in _gfx_preset_buttons.size():
		UITheme.set_button_selected(_gfx_preset_buttons[i], i == current)


func _refresh_fps_buttons() -> void:
	var current := 60
	if settings != null:
		current = settings.gfx_fps
	for i in _fps_buttons.size():
		UITheme.set_button_selected(_fps_buttons[i], GameSettings.FPS_OPTIONS[i] == current)


func _refresh_scale() -> void:
	if settings == null:
		return
	if _gfx_scale_slider != null:
		_gfx_scale_slider.set_value_no_signal(settings.gfx_scale)
	if _gfx_scale_text != null:
		_gfx_scale_text.text = "%d%%" % int(round(settings.gfx_scale * 100.0))


# --- Build (code-built UI, Unity coordinates, 1280x720) -----------------------

func _build() -> void:
	if _built:
		return
	_built = true

	var root := Control.new()
	root.name = "SettingsRoot"
	UITheme.place_full(root)
	add_child(root)

	UITheme.panel(root, "SettingsBackground", Color(0, 0, 0, 0.7),
		Vector2.ZERO, Vector2.ONE, Vector2(0, 0), Vector2(0, 0))
	var center := Vector2(0.5, 0.5)
	var card := UITheme.panel(root, "SettingsCard", UITheme.PANEL_SOLID,
		center, center, Vector2(0, 0), Vector2(620, 620))
	var top := Vector2(0.5, 1.0)
	var left := Vector2(0.0, 1.0)

	UITheme.label(card, "SettingsTitle", "Cài đặt",
		32, UITheme.TEXT_WHITE, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(0, -45), Vector2(560, 44))

	# Music.
	UITheme.label(card, "MusicLabel", "Nhạc nền",
		22, UITheme.TEXT_WHITE, HORIZONTAL_ALIGNMENT_LEFT, VERTICAL_ALIGNMENT_CENTER,
		left, left, Vector2(130, -110), Vector2(140, 30))
	_music_slider = UITheme.slider(card, "MusicSlider", 0.0, 1.0, 0.8,
		top, top, Vector2(50, -110), Vector2(300, 30))
	_music_slider.value_changed.connect(_on_music_slider)
	_music_value = UITheme.label(card, "MusicValueText", "80%",
		22, UITheme.TEXT_DIM, HORIZONTAL_ALIGNMENT_LEFT, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(230, -110), Vector2(70, 30))

	# SFX.
	UITheme.label(card, "SfxLabel", "Hiệu ứng",
		22, UITheme.TEXT_WHITE, HORIZONTAL_ALIGNMENT_LEFT, VERTICAL_ALIGNMENT_CENTER,
		left, left, Vector2(130, -160), Vector2(140, 30))
	_sfx_slider = UITheme.slider(card, "SfxSlider", 0.0, 1.0, 0.8,
		top, top, Vector2(50, -160), Vector2(300, 30))
	_sfx_slider.value_changed.connect(_on_sfx_slider)
	_sfx_value = UITheme.label(card, "SfxValueText", "80%",
		22, UITheme.TEXT_DIM, HORIZONTAL_ALIGNMENT_LEFT, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(230, -160), Vector2(70, 30))

	# Music honesty note (Unity: no music track in v0.1.0).
	UITheme.label(card, "MusicNoteText", "Chưa có nhạc nền ở bản này — trượt nhạc chưa phát ra tiếng.",
		16, UITheme.TEXT_DIM, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(0, -188), Vector2(560, 22))

	# Camera: 6 buttons, 2 rows x 3.
	UITheme.label(card, "CameraLabel", "Góc camera",
		22, UITheme.TEXT_WHITE, HORIZONTAL_ALIGNMENT_LEFT, VERTICAL_ALIGNMENT_CENTER,
		left, left, Vector2(130, -215), Vector2(200, 30))
	_preset_buttons.clear()
	for i in 6:
		var preset := i
		var x := -170.0 + (i % 3) * 175.0
		var y := -260.0 - (i / 3) * 60.0
		var b := UITheme.button(card, "CameraPresetButton%d" % i,
			GameSettings.CAMERA_PRESET_NAMES[i], 20,
			top, top, Vector2(x, y), Vector2(165, 48))
		b.pressed.connect(func() -> void: _on_camera_preset_button(preset))
		_preset_buttons.append(b)

	# Left hand.
	_left_hand_toggle = UITheme.toggle(card, "LeftHandToggle",
		"Chế độ tay trái (đổi joystick <-> nút đánh)", 18, false,
		left, left, Vector2(280, -395), Vector2(520, 32))
	_left_hand_toggle.toggled.connect(_on_left_hand_toggle)

	# Difficulty (read-only).
	_difficulty_text = UITheme.label(card, "DifficultyText",
		"Độ khó AI: Intermediate (2) — chọn ở menu chính",
		20, UITheme.TEXT_DIM, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(0, -445), Vector2(560, 28))

	# Graphics: quality preset.
	UITheme.label(card, "GraphicsLabel", "Đồ hoạ",
		22, UITheme.TEXT_WHITE, HORIZONTAL_ALIGNMENT_LEFT, VERTICAL_ALIGNMENT_CENTER,
		left, left, Vector2(130, -480), Vector2(200, 30))
	_gfx_preset_buttons.clear()
	for i in 4:
		var tier := i
		var x := -210.0 + i * 140.0
		var b := UITheme.button(card, "GfxPresetButton%d" % i,
			GameSettings.TIER_NAMES[i], 18,
			top, top, Vector2(x, -515), Vector2(132, 40))
		b.pressed.connect(func() -> void: _on_graphics_preset_button(tier))
		_gfx_preset_buttons.append(b)

	# FPS: 30 / 60 / 90 / 120 + live FPS meter.
	UITheme.label(card, "FpsLabel", "FPS",
		22, UITheme.TEXT_WHITE, HORIZONTAL_ALIGNMENT_LEFT, VERTICAL_ALIGNMENT_CENTER,
		left, left, Vector2(130, -558), Vector2(60, 30))
	_fps_buttons.clear()
	for i in 4:
		var fps := GameSettings.FPS_OPTIONS[i]
		var x := -168.0 + i * 72.0
		var b := UITheme.button(card, "FpsButton%d" % i, str(fps), 18,
			top, top, Vector2(x, -558), Vector2(64, 40))
		b.pressed.connect(func() -> void: _on_fps_button(fps))
		_fps_buttons.append(b)
	_fps_meter_text = UITheme.label(card, "FpsMeterText", "— FPS",
		20, UITheme.TEXT_DIM, HORIZONTAL_ALIGNMENT_LEFT, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(150, -558), Vector2(110, 30))

	# Render resolution (60-100%).
	UITheme.label(card, "ScaleLabel", "Độ phân giải",
		22, UITheme.TEXT_WHITE, HORIZONTAL_ALIGNMENT_LEFT, VERTICAL_ALIGNMENT_CENTER,
		left, left, Vector2(130, -598), Vector2(140, 30))
	_gfx_scale_slider = UITheme.slider(card, "GfxScaleSlider", 0.6, 1.0, 1.0,
		top, top, Vector2(20, -598), Vector2(240, 30))
	_gfx_scale_slider.value_changed.connect(_on_graphics_scale_slider)
	_gfx_scale_text = UITheme.label(card, "GfxScaleText", "100%",
		20, UITheme.TEXT_DIM, HORIZONTAL_ALIGNMENT_LEFT, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(180, -598), Vector2(60, 30))

	# Close button, top-right of the card.
	var close_btn := UITheme.button(card, "CloseButton", "X", 24,
		top, top, Vector2(270, -45), Vector2(48, 44))
	close_btn.pressed.connect(_on_close_button)
