class_name UITheme
extends RefCounted
## UITheme — port of Unity UIBuilder.cs (ShuttleLab).
## Shared palette + code-built Control factory. The whole UI is built in
## code (no .tscn scenes), exactly like the Unity side. Square corners
## everywhere (no rounding), dark palette with court-green accent.
## Layout uses Unity-style placement: an anchor point (0..1) + an
## anchored position in 1280x720 reference pixels (y up, like Unity) +
## a size. `place()` converts to Godot offsets (y down).

# --- Palette (mirrors UIBuilder) -------------------------------------------
const PANEL_DARK := Color(0, 0, 0, 0.55)
const PANEL_SOLID := Color(0.05, 0.09, 0.07, 0.95)
const BUTTON_NORMAL := Color(0.10, 0.16, 0.12, 0.95)
const BUTTON_SELECTED := Color(0.16, 0.55, 0.30, 0.95)
const BUTTON_HOVER := Color(0.18, 0.30, 0.22, 0.95)
const ACCENT := Color(0.20, 0.75, 0.38)
const ACCENT_TEXT := Color(0.55, 1.0, 0.65)
const TEXT_WHITE := Color.WHITE
const TEXT_DIM := Color(0.75, 0.78, 0.75)
const DANGER := Color(0.75, 0.25, 0.22)
const BAR_GOOD := Color(0.25, 0.80, 0.40)
const BAR_LOW := Color(0.95, 0.75, 0.20)
const BAR_EMPTY := Color(0.90, 0.25, 0.20)
const SLIDER_TRACK := Color(0.20, 0.24, 0.21, 0.95)
const MENU_BACKGROUND := Color(0.03, 0.10, 0.06, 1.0)
const DIM_OVERLAY := Color(0, 0, 0, 0.6)
const DIM_OVERLAY_STRONG := Color(0, 0, 0, 0.75)

const REF_W := 1280.0
const REF_H := 720.0


static func style(color: Color) -> StyleBoxFlat:
	var s := StyleBoxFlat.new()
	s.bg_color = color
	s.set_corner_radius_all(0)
	s.content_margin_left = 0.0
	s.content_margin_right = 0.0
	s.content_margin_top = 0.0
	s.content_margin_bottom = 0.0
	return s


## Place a Control Unity-style: anchor point (0..1, y UP like Unity) +
## anchored position in 1280x720 reference pixels (y up) + size.
## Unity anchor (0,0) = bottom-left; Godot anchor (0,0) = top-left, so
## the y anchor is flipped. Works for point anchors and stretched rects
## (anchored_pos = centre offset, size = sizeDelta).
static func place(ctrl: Control, anchor_min: Vector2, anchor_max: Vector2,
		anchored_pos: Vector2, size: Vector2) -> Control:
	ctrl.anchor_left = anchor_min.x
	ctrl.anchor_right = anchor_max.x
	ctrl.anchor_top = 1.0 - anchor_max.y
	ctrl.anchor_bottom = 1.0 - anchor_min.y
	ctrl.offset_left = anchored_pos.x - size.x * 0.5
	ctrl.offset_right = anchored_pos.x + size.x * 0.5
	ctrl.offset_top = -anchored_pos.y - size.y * 0.5
	ctrl.offset_bottom = -anchored_pos.y + size.y * 0.5
	return ctrl


static func place_full(ctrl: Control) -> Control:
	ctrl.anchor_left = 0.0
	ctrl.anchor_top = 0.0
	ctrl.anchor_right = 1.0
	ctrl.anchor_bottom = 1.0
	ctrl.offset_left = 0.0
	ctrl.offset_top = 0.0
	ctrl.offset_right = 0.0
	ctrl.offset_bottom = 0.0
	return ctrl


static func panel(parent: Node, panel_name: String, color: Color,
		anchor_min: Vector2, anchor_max: Vector2,
		anchored_pos: Vector2, size: Vector2) -> Panel:
	var p := Panel.new()
	p.name = panel_name
	p.add_theme_stylebox_override("panel", style(color))
	place(p, anchor_min, anchor_max, anchored_pos, size)
	parent.add_child(p)
	return p


static func label(parent: Node, label_name: String, text: String,
		font_size: int, color: Color,
		h_align: HorizontalAlignment, v_align: VerticalAlignment,
		anchor_min: Vector2, anchor_max: Vector2,
		anchored_pos: Vector2, size: Vector2) -> Label:
	var l := Label.new()
	l.name = label_name
	l.text = text
	l.add_theme_font_size_override("font_size", font_size)
	l.add_theme_color_override("font_color", color)
	l.horizontal_alignment = h_align
	l.vertical_alignment = v_align
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	place(l, anchor_min, anchor_max, anchored_pos, size)
	parent.add_child(l)
	return l


static func button(parent: Node, btn_name: String, text: String,
		font_size: int, anchor_min: Vector2, anchor_max: Vector2,
		anchored_pos: Vector2, size: Vector2) -> Button:
	var b := Button.new()
	b.name = btn_name
	b.text = text
	b.add_theme_font_size_override("font_size", font_size)
	b.add_theme_color_override("font_color", TEXT_WHITE)
	b.add_theme_color_override("font_hover_color", TEXT_WHITE)
	b.add_theme_color_override("font_pressed_color", TEXT_WHITE)
	b.add_theme_color_override("font_disabled_color", Color(0.6, 0.6, 0.6, 0.8))
	b.add_theme_stylebox_override("normal", style(BUTTON_NORMAL))
	b.add_theme_stylebox_override("hover", style(BUTTON_HOVER))
	b.add_theme_stylebox_override("pressed", style(BUTTON_SELECTED))
	b.add_theme_stylebox_override("disabled", style(Color(0.15, 0.15, 0.15, 0.55)))
	b.add_theme_stylebox_override("focus", StyleBoxEmpty.new())
	place(b, anchor_min, anchor_max, anchored_pos, size)
	parent.add_child(b)
	return b


## Highlight a button as the current selection (Unity SetButtonSelected).
static func set_button_selected(b: Button, selected: bool) -> void:
	if b == null:
		return
	b.add_theme_stylebox_override("normal",
		style(BUTTON_SELECTED if selected else BUTTON_NORMAL))


## Danger-coloured button (e.g. "Về menu" in the pause card).
static func set_button_danger(b: Button) -> void:
	if b == null:
		return
	b.add_theme_stylebox_override("normal", style(DANGER))
	b.add_theme_stylebox_override("hover", style(Color(0.85, 0.35, 0.30, 0.95)))


static func slider(parent: Node, slider_name: String,
		min_value: float, max_value: float, value: float,
		anchor_min: Vector2, anchor_max: Vector2,
		anchored_pos: Vector2, size: Vector2) -> HSlider:
	var s := HSlider.new()
	s.name = slider_name
	s.min_value = min_value
	s.max_value = max_value
	s.value = value
	s.add_theme_stylebox_override("slider", style(SLIDER_TRACK))
	s.add_theme_stylebox_override("grabber_area", style(ACCENT))
	s.add_theme_stylebox_override("grabber_area_highlight", style(ACCENT))
	var grabber := _grabber_texture()
	s.add_theme_icon_override("grabber", grabber)
	s.add_theme_icon_override("grabber_highlight", grabber)
	place(s, anchor_min, anchor_max, anchored_pos, size)
	parent.add_child(s)
	return s


static func _grabber_texture() -> ImageTexture:
	var img := Image.create(20, 28, false, Image.FORMAT_RGBA8)
	img.fill(Color.WHITE)
	return ImageTexture.create_from_image(img)


static func toggle(parent: Node, toggle_name: String, text: String,
		font_size: int, is_on: bool,
		anchor_min: Vector2, anchor_max: Vector2,
		anchored_pos: Vector2, size: Vector2) -> CheckButton:
	var t := CheckButton.new()
	t.name = toggle_name
	t.text = text
	t.button_pressed = is_on
	t.add_theme_font_size_override("font_size", font_size)
	t.add_theme_color_override("font_color", TEXT_WHITE)
	place(t, anchor_min, anchor_max, anchored_pos, size)
	parent.add_child(t)
	return t


static func input_field(parent: Node, field_name: String,
		placeholder: String, initial_text: String,
		anchor_min: Vector2, anchor_max: Vector2,
		anchored_pos: Vector2, size: Vector2) -> LineEdit:
	var e := LineEdit.new()
	e.name = field_name
	e.placeholder_text = placeholder
	e.text = initial_text
	e.add_theme_font_size_override("font_size", 24)
	e.add_theme_color_override("font_color", TEXT_WHITE)
	e.add_theme_color_override("font_placeholder_color", Color(0.7, 0.7, 0.7, 0.7))
	e.add_theme_stylebox_override("normal", style(Color(0, 0, 0, 0.55)))
	e.add_theme_stylebox_override("focus", style(Color(0.05, 0.12, 0.08, 0.9)))
	place(e, anchor_min, anchor_max, anchored_pos, size)
	parent.add_child(e)
	return e


## Horizontal stamina bar (ProgressBar with flat square fill).
static func stamina_bar(parent: Node, bar_name: String,
		anchor_min: Vector2, anchor_max: Vector2,
		anchored_pos: Vector2, size: Vector2) -> ProgressBar:
	var bar := ProgressBar.new()
	bar.name = bar_name
	bar.min_value = 0.0
	bar.max_value = 1.0
	bar.value = 1.0
	bar.show_percentage = false
	bar.add_theme_stylebox_override("background", style(Color(0, 0, 0, 0.55)))
	bar.add_theme_stylebox_override("fill", style(BAR_GOOD))
	place(bar, anchor_min, anchor_max, anchored_pos, size)
	parent.add_child(bar)
	return bar


## Colour a stamina bar by level (Unity HUDManager thresholds).
static func set_stamina_color(bar: ProgressBar, normalized: float) -> void:
	if bar == null:
		return
	var c := BAR_GOOD
	if normalized <= 0.1:
		c = BAR_EMPTY
	elif normalized <= 0.3:
		c = BAR_LOW
	bar.add_theme_stylebox_override("fill", style(c))
