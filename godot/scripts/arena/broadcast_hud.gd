class_name BroadcastHud
extends CanvasLayer
## Broadcast-style match HUD (v0.2 phase 1) — the HUD node structure.
## Built entirely in code (like the Unity HUDManager): a scoreboard at
## the top centre (fictional player names, points, games, server dot)
## and a stamina bar for each player at the bottom.
##
## The UI subagent owns menus and the settings panel; this node only
## owns the in-match broadcast overlay. It never handles input
## (mouse_filter IGNORE everywhere) and sits on layer 10, above the
## touch input canvas (layer 5 in the Unity build).
##
## API:
##   set_players(p0_name, p1_name)   — fictional names only, no real brands
##   set_title(text)                 — e.g. "SHUTTLELAB • CHUNG KẾT"
##   set_score(p0_pts, p1_pts, p0_games, p1_games, server)  (server: 0/1/-1)
##   set_stamina(p0, p1)             — 0.0..1.0 each
##   show_message(text, duration)    — centre flash, e.g. "ĐIỂM CHO MINH QUÂN"

const PANEL_BG := Color(0.0, 0.0, 0.0, 0.55)
const TEXT_WHITE := Color(1, 1, 1)
const ACCENT_CYAN := Color(0.50, 0.85, 1.00)
const STAMINA_P0 := Color(0.85, 0.20, 0.20)
const STAMINA_P1 := Color(0.20, 0.45, 0.90)

var _p0_name := "NGƯỜI CHƠI"
var _p1_name := "ĐỐI THỦ"

var _title_label: Label
var _name0_label: Label
var _name1_label: Label
var _points0_label: Label
var _points1_label: Label
var _games0_label: Label
var _games1_label: Label
var _stamina0: ProgressBar
var _stamina1: ProgressBar
var _message_label: Label
var _message_hide_at_msec := 0

func _process(_delta: float) -> void:
	# Hide the centre message when its time is up. Uses wall-clock time
	# instead of get_tree().create_timer() so show_message() is safe to
	# call even before this node enters the tree.
	if _message_label.visible and _message_hide_at_msec > 0 \
			and Time.get_ticks_msec() >= _message_hide_at_msec:
		_message_label.visible = false
		_message_hide_at_msec = 0

func _init() -> void:
	# Build in _init (not _ready) so the public API (set_score,
	# set_stamina, ...) is safe to call immediately after .new(),
	# before the node enters the tree.
	layer = 10
	_build_scoreboard()
	_build_stamina_bars()
	_build_message_label()

func set_players(p0_name: String, p1_name: String) -> void:
	_p0_name = p0_name
	_p1_name = p1_name
	_refresh_names(-1)

func set_title(text: String) -> void:
	if _title_label != null:
		_title_label.text = text

func set_score(p0_points: int, p1_points: int, p0_games: int, p1_games: int, server: int) -> void:
	_points0_label.text = str(p0_points)
	_points1_label.text = str(p1_points)
	_games0_label.text = str(p0_games)
	_games1_label.text = str(p1_games)
	_refresh_names(server)

func set_stamina(p0: float, p1: float) -> void:
	_stamina0.value = clampf(p0, 0.0, 1.0) * 100.0
	_stamina1.value = clampf(p1, 0.0, 1.0) * 100.0

func show_message(text: String, duration: float = 1.5) -> void:
	_message_label.text = text
	_message_label.visible = true
	_message_hide_at_msec = Time.get_ticks_msec() + int(duration * 1000.0)

func _refresh_names(server: int) -> void:
	# Server gets a cyan dot in front of their name; "-" means no server yet.
	_name0_label.text = ("● " if server == 0 else "") + _p0_name
	_name1_label.text = ("● " if server == 1 else "") + _p1_name

# ------------------------------------------------------------------ build

func _panel_style() -> StyleBoxFlat:
	var sb := StyleBoxFlat.new()
	sb.bg_color = PANEL_BG
	sb.corner_radius_top_left = 6
	sb.corner_radius_top_right = 6
	sb.corner_radius_bottom_left = 6
	sb.corner_radius_bottom_right = 6
	sb.content_margin_left = 16
	sb.content_margin_right = 16
	sb.content_margin_top = 8
	sb.content_margin_bottom = 8
	return sb

func _label(text: String, size: int, color: Color = TEXT_WHITE) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", color)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return l

func _build_scoreboard() -> void:
	var panel := PanelContainer.new()
	panel.name = "Scoreboard"
	panel.add_theme_stylebox_override("panel", _panel_style())
	panel.anchor_left = 0.5
	panel.anchor_right = 0.5
	panel.offset_left = -280
	panel.offset_right = 280
	panel.offset_top = 12
	panel.offset_bottom = 96
	panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(panel)
	var vbox := VBoxContainer.new()
	vbox.mouse_filter = Control.MOUSE_FILTER_IGNORE
	panel.add_child(vbox)
	_title_label = _label("SHUTTLELAB", 15, ACCENT_CYAN)
	_title_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	vbox.add_child(_title_label)
	var row := HBoxContainer.new()
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.add_theme_constant_override("separation", 14)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	vbox.add_child(row)
	_name0_label = _label(_p0_name, 20)
	_games0_label = _label("0", 16, ACCENT_CYAN)
	_points0_label = _label("0", 40)
	_points1_label = _label("0", 40)
	_games1_label = _label("0", 16, ACCENT_CYAN)
	_name1_label = _label(_p1_name, 20)
	row.add_child(_name0_label)
	row.add_child(_games0_label)
	row.add_child(_points0_label)
	row.add_child(_label("-", 32))
	row.add_child(_points1_label)
	row.add_child(_games1_label)
	row.add_child(_name1_label)

func _stamina_bar(color: Color) -> ProgressBar:
	var bar := ProgressBar.new()
	bar.min_value = 0
	bar.max_value = 100
	bar.value = 100
	bar.show_percentage = false
	bar.custom_minimum_size = Vector2(220, 16)
	bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var bg := StyleBoxFlat.new()
	bg.bg_color = Color(0, 0, 0, 0.5)
	bg.corner_radius_top_left = 4
	bg.corner_radius_top_right = 4
	bg.corner_radius_bottom_left = 4
	bg.corner_radius_bottom_right = 4
	var fill := StyleBoxFlat.new()
	fill.bg_color = color
	fill.corner_radius_top_left = 4
	fill.corner_radius_top_right = 4
	fill.corner_radius_bottom_left = 4
	fill.corner_radius_bottom_right = 4
	bar.add_theme_stylebox_override("background", bg)
	bar.add_theme_stylebox_override("fill", fill)
	return bar

func _build_stamina_bars() -> void:
	var left := VBoxContainer.new()
	left.anchor_top = 1.0
	left.anchor_bottom = 1.0
	left.offset_left = 16
	left.offset_top = -64
	left.offset_right = 252
	left.offset_bottom = -16
	left.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(left)
	var l0 := _label("THỂ LỰC", 13, ACCENT_CYAN)
	left.add_child(l0)
	_stamina0 = _stamina_bar(STAMINA_P0)
	left.add_child(_stamina0)
	var right := VBoxContainer.new()
	right.anchor_left = 1.0
	right.anchor_right = 1.0
	right.anchor_top = 1.0
	right.anchor_bottom = 1.0
	right.offset_left = -252
	right.offset_top = -64
	right.offset_right = -16
	right.offset_bottom = -16
	right.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(right)
	var l1 := _label("THỂ LỰC", 13, ACCENT_CYAN)
	l1.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	right.add_child(l1)
	_stamina1 = _stamina_bar(STAMINA_P1)
	right.add_child(_stamina1)

func _build_message_label() -> void:
	_message_label = _label("", 44)
	_message_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_message_label.anchor_left = 0.5
	_message_label.anchor_right = 0.5
	_message_label.anchor_top = 0.5
	_message_label.anchor_bottom = 0.5
	_message_label.offset_left = -400
	_message_label.offset_right = 400
	_message_label.offset_top = -140
	_message_label.offset_bottom = -60
	_message_label.visible = false
	add_child(_message_label)
