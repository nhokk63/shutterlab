extends CanvasLayer
class_name CareerUI
## CareerUI — port of Unity UI/CareerUI.cs.
## Career screen with 3 tabs: Tổng quan / Luyện tập / Cửa hàng.
##
## DATA CONTRACT (the progression subagent owns CareerManager /
## TrainingManager / EquipmentManager / TournamentManager; this screen
## takes plain data so the two halves stay decoupled):
##   set_data(data: Dictionary) with keys:
##     active: bool, week: int, rank: int, points: int, money: int,
##     wins: int, losses: int, training_points: int,
##     is_tournament_week: bool, tournament_name: String,
##     tournament_tier: String, weeks_until_tournament: int,
##     win_points: int, prize_money: int,
##     weekly_event_name: String, weekly_event_tier: String,
##     missions: {wins_progress: int, smashes_progress: int,
##       clean_win_done: bool},
##     tournament_active: bool, can_play_tournament: bool,
##     tournament_note: String, bracket_lines: Array[String],
##     online_message: String,
##     categories: Array[String], selected_category: String,
##     drills: Array of {id, name, description, target_text, best_text},
##     selected_drill_id: String,
##     shop_slots: Array[String], selected_slot: int,
##     shop_items: Array of {id, name, description, price, rarity_name,
##       rarity_color: Color, mod_text, owned: bool, equipped: bool},
##     selected_item_id: String, shop_message: String
## Actions are signals; the progression layer handles them and calls
## set_data() again with the new state.
##
## HONEST LIMITS (same as Unity v0.1.0):
##  - The bracket lives only in memory; leaving mid-tournament loses
##    it (re-entering the same tournament week rebuilds the same
##    bracket from the week seed).
##  - "Vào tập" is DISABLED: playing a drill needs a Practice drill
##    flow that does not exist yet.
##  - Training points are shown accumulated only; they do not convert
##    to stat boosts without a real drill flow.

signal back_requested
signal start_career_requested
signal advance_week_requested
signal play_tournament_requested
signal category_selected(category: String)
signal drill_selected(drill_id: String)
signal slot_selected(slot: int)
signal item_selected(item_id: String)
signal buy_requested(item_id: String)
signal equip_requested(item_id: String)

var _data: Dictionary = {}
var _tab: int = 0

var _tab_buttons: Array[Button] = []
var _overview_panel: Control
var _training_panel: Control
var _shop_panel: Control

var _info_text: Label
var _tournament_text: Label
var _tournament_note_text: Label
var _bracket_text: Label
var _online_text: Label
var _start_career_button: Button
var _advance_week_button: Button
var _play_tournament_button: Button

var _category_buttons: Array[Button] = []
var _drill_list_box: VBoxContainer
var _drill_detail_text: Label
var _start_drill_button: Button

var _shop_money_text: Label
var _slot_buttons: Array[Button] = []
var _shop_list_box: VBoxContainer
var _shop_detail_text: Label
var _buy_button: Button
var _equip_button: Button
var _shop_message_text: Label

var _built := false


func _ready() -> void:
	_build()
	_refresh()


## Push new career state into the screen (call after every action).
func set_data(data: Dictionary) -> void:
	_data = data
	_refresh()


# --- Tab / action handlers -----------------------------------------------------

func _on_tab_button(tab: int) -> void:
	_tab = clampi(tab, 0, 2)
	_refresh()


func _on_back_button() -> void:
	back_requested.emit()


# --- Refresh ---------------------------------------------------------------------

func _refresh() -> void:
	if not _built:
		return
	for i in _tab_buttons.size():
		UITheme.set_button_selected(_tab_buttons[i], i == _tab)
	if _overview_panel != null:
		_overview_panel.visible = _tab == 0
	if _training_panel != null:
		_training_panel.visible = _tab == 1
	if _shop_panel != null:
		_shop_panel.visible = _tab == 2
	if _tab == 0:
		_refresh_overview()
	elif _tab == 1:
		_refresh_training()
	else:
		_refresh_shop()


func _d(key: String, default_value: Variant) -> Variant:
	return _data.get(key, default_value)


func _refresh_overview() -> void:
	var active := bool(_d("active", false))
	var summary := "Tuần %d • Hạng %d • %d điểm • %d xu" % [
		int(_d("week", 1)), int(_d("rank", 512)), int(_d("points", 0)), int(_d("money", 500))]
	if _info_text != null:
		_info_text.text = "%s • Thắng %d - Thua %d • Điểm tập %d" % [
			summary, int(_d("wins", 0)), int(_d("losses", 0)), int(_d("training_points", 0))]
	if _start_career_button != null:
		_start_career_button.visible = not active
	if _advance_week_button != null:
		_advance_week_button.visible = active
	if _play_tournament_button != null:
		_play_tournament_button.visible = active

	var missions: Dictionary = _d("missions", {})
	var weekly_line := "\nGiải tuần: %s (%s) • NV: thắng %d/3 • smash %d/10%s" % [
		str(_d("weekly_event_name", "—")), str(_d("weekly_event_tier", "—")),
		int(missions.get("wins_progress", 0)), int(missions.get("smashes_progress", 0)),
		" • thắng sạch: xong" if bool(missions.get("clean_win_done", false)) else " • thắng sạch: chưa"]
	if _tournament_text != null:
		if not active:
			_tournament_text.text = "Bắt đầu sự nghiệp để chơi theo tuần: 3 tuần tập luyện, 1 tuần thi đấu, thăng hạng qua các cấp giải." + weekly_line
		elif bool(_d("is_tournament_week", false)):
			_tournament_text.text = "TUẦN GIẢI — %s (%s) • Vô địch: +%d điểm, %d xu • Còn %d tuần tới giải sau.%s" % [
				str(_d("tournament_name", "—")), str(_d("tournament_tier", "—")),
				int(_d("win_points", 0)), int(_d("prize_money", 0)),
				int(_d("weeks_until_tournament", 0)), weekly_line]
		else:
			_tournament_text.text = "Tuần tập luyện — giải tới: %s (%s) sau %d tuần nữa.%s" % [
				str(_d("tournament_name", "—")), str(_d("tournament_tier", "—")),
				int(_d("weeks_until_tournament", 0)), weekly_line]

	if _play_tournament_button != null:
		_play_tournament_button.disabled = not bool(_d("can_play_tournament", false))
	if _tournament_note_text != null:
		_tournament_note_text.text = str(_d("tournament_note", ""))
	if _bracket_text != null:
		var lines: Array = _d("bracket_lines", [])
		if lines.is_empty():
			_bracket_text.text = "Chưa có bracket giải đấu."
		else:
			_bracket_text.text = "\n".join(lines)
	if _online_text != null:
		_online_text.text = "Bảng xếp hạng online: " + str(_d("online_message", "chưa khả dụng"))


func _refresh_training() -> void:
	var categories: Array = _d("categories", [])
	var selected_category := str(_d("selected_category", ""))
	for i in _category_buttons.size():
		if i < categories.size():
			UITheme.set_button_selected(_category_buttons[i], str(categories[i]) == selected_category)
	_clear_list(_drill_list_box)
	var drills: Array = _d("drills", [])
	var selected_drill := str(_d("selected_drill_id", ""))
	for i in drills.size():
		var drill: Dictionary = drills[i]
		var drill_id := str(drill.get("id", ""))
		var b := _list_button(_drill_list_box, "DrillButton%d" % i, str(drill.get("name", "?")))
		UITheme.set_button_selected(b, drill_id == selected_drill)
		b.pressed.connect(func() -> void: drill_selected.emit(drill_id))
	if _drill_detail_text != null:
		var detail := "Chọn một bài tập để xem mục tiêu và kỷ lục."
		for drill in drills:
			var d: Dictionary = drill
			if str(d.get("id", "")) == selected_drill and selected_drill != "":
				detail = "%s\n%s\nMục tiêu: %s\n%s" % [
					str(d.get("name", "")), str(d.get("description", "")),
					str(d.get("target_text", "")), str(d.get("best_text", "Chưa có kỷ lục."))]
				break
		_drill_detail_text.text = detail
	if _start_drill_button != null:
		# No real drill flow yet — disabled with an honest reason (Unity).
		_start_drill_button.disabled = true


func _refresh_shop() -> void:
	if _shop_money_text != null:
		_shop_money_text.text = "Tiền của bạn: %d xu" % int(_d("money", 0))
	var slots: Array = _d("shop_slots", ["Vợt", "Giày", "Áo"])
	var selected_slot := int(_d("selected_slot", 0))
	for i in _slot_buttons.size():
		UITheme.set_button_selected(_slot_buttons[i], i == selected_slot)
	_clear_list(_shop_list_box)
	var items: Array = _d("shop_items", [])
	var selected_item := str(_d("selected_item_id", ""))
	for i in items.size():
		var item: Dictionary = items[i]
		var item_id := str(item.get("id", ""))
		var state := ""
		if bool(item.get("equipped", false)):
			state = " [ĐANG ĐEO]"
		elif bool(item.get("owned", false)):
			state = " [Đã có]"
		else:
			state = " — %d xu" % int(item.get("price", 0))
		var b := _list_button(_shop_list_box, "ShopItem%d" % i,
			str(item.get("name", "?")) + state)
		var rarity_color: Color = item.get("rarity_color", UITheme.TEXT_WHITE)
		b.add_theme_color_override("font_color", rarity_color)
		UITheme.set_button_selected(b, item_id == selected_item)
		b.pressed.connect(func() -> void: item_selected.emit(item_id))
	if _shop_detail_text != null:
		var detail := "Chọn một món để xem chỉ số và giá."
		var selected: Dictionary = {}
		for item in items:
			var it: Dictionary = item
			if str(it.get("id", "")) == selected_item and selected_item != "":
				selected = it
				break
		if not selected.is_empty():
			var owned := "Đã sở hữu" if bool(selected.get("owned", false)) else "Chưa sở hữu"
			var equipped := " • Đang đeo" if bool(selected.get("equipped", false)) else ""
			detail = "[%s] %s\n%s\nChỉ số: %s\nGiá: %d xu • %s%s" % [
				str(selected.get("rarity_name", "")), str(selected.get("name", "")),
				str(selected.get("description", "")), str(selected.get("mod_text", "")),
				int(selected.get("price", 0)), owned, equipped]
		_shop_detail_text.text = detail
	var selected_item_data: Dictionary = {}
	for item in items:
		var it: Dictionary = item
		if str(it.get("id", "")) == selected_item and selected_item != "":
			selected_item_data = it
			break
	if _buy_button != null:
		_buy_button.visible = (not selected_item_data.is_empty()
			and not bool(selected_item_data.get("owned", false)))
		_buy_button.disabled = selected_item_data.is_empty() or int(_d("money", 0)) < int(selected_item_data.get("price", 0))
	if _equip_button != null:
		_equip_button.visible = (not selected_item_data.is_empty()
			and bool(selected_item_data.get("owned", false))
			and not bool(selected_item_data.get("equipped", false)))
	if _shop_message_text != null and selected_item.is_empty():
		_shop_message_text.text = str(_d("shop_message", ""))


# --- Helpers ---------------------------------------------------------------------

func _clear_list(box: VBoxContainer) -> void:
	if box == null:
		return
	for child in box.get_children():
		child.queue_free()


## List button inside a VBoxContainer (container lays it out; no anchors).
func _list_button(parent: Node, btn_name: String, text: String) -> Button:
	var b := Button.new()
	b.name = btn_name
	b.text = text
	b.custom_minimum_size = Vector2(580, 46)
	b.add_theme_font_size_override("font_size", 20)
	b.add_theme_color_override("font_color", UITheme.TEXT_WHITE)
	b.add_theme_stylebox_override("normal", UITheme.style(UITheme.BUTTON_NORMAL))
	b.add_theme_stylebox_override("hover", UITheme.style(UITheme.BUTTON_HOVER))
	b.add_theme_stylebox_override("pressed", UITheme.style(UITheme.BUTTON_SELECTED))
	b.add_theme_stylebox_override("focus", StyleBoxEmpty.new())
	parent.add_child(b)
	return b


# --- Build (code-built UI, Unity coordinates, 1280x720) -----------------------

func _build() -> void:
	if _built:
		return
	_built = true

	var root := Control.new()
	root.name = "CareerRoot"
	UITheme.place_full(root)
	add_child(root)

	UITheme.panel(root, "CareerBackground", UITheme.MENU_BACKGROUND,
		Vector2.ZERO, Vector2.ONE, Vector2(0, 0), Vector2(0, 0))
	var top := Vector2(0.5, 1.0)
	UITheme.label(root, "TitleText", "SỰ NGHIỆP",
		40, UITheme.ACCENT_TEXT, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(0, -45), Vector2(700, 50))

	var tab_names: Array[String] = ["Tổng quan", "Luyện tập", "Cửa hàng"]
	_tab_buttons.clear()
	for i in 3:
		var tab := i
		var b := UITheme.button(root, "TabButton%d" % i, tab_names[i], 22,
			top, top, Vector2(-320 + i * 320, -110), Vector2(300, 52))
		b.pressed.connect(func() -> void: _on_tab_button(tab))
		_tab_buttons.append(b)

	var back_btn := UITheme.button(root, "BackButton", "Về menu", 22,
		Vector2(0.5, 0.0), Vector2(0.5, 0.0), Vector2(0, 30), Vector2(260, 52))
	back_btn.pressed.connect(_on_back_button)

	_build_overview_panel(root)
	_build_training_panel(root)
	_build_shop_panel(root)


func _tab_panel(root: Control, panel_name: String) -> Control:
	var p := Control.new()
	p.name = panel_name
	UITheme.place_full(p)
	root.add_child(p)
	return p


func _build_overview_panel(root: Control) -> void:
	_overview_panel = _tab_panel(root, "OverviewPanel")
	var p := _overview_panel
	var top := Vector2(0.5, 1.0)
	_info_text = UITheme.label(p, "InfoText", "",
		22, UITheme.TEXT_WHITE, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(0, -175), Vector2(1100, 30))
	_start_career_button = UITheme.button(p, "StartCareerButton", "Bắt đầu sự nghiệp", 24,
		top, top, Vector2(-320, -225), Vector2(320, 56))
	_start_career_button.pressed.connect(func() -> void: start_career_requested.emit())
	_advance_week_button = UITheme.button(p, "AdvanceWeekButton", "Sang tuần", 24,
		top, top, Vector2(-320, -225), Vector2(320, 56))
	_advance_week_button.pressed.connect(func() -> void: advance_week_requested.emit())
	_play_tournament_button = UITheme.button(p, "PlayTournamentButton", "Thi đấu giải", 24,
		top, top, Vector2(120, -225), Vector2(320, 56))
	_play_tournament_button.pressed.connect(func() -> void: play_tournament_requested.emit())
	_tournament_text = UITheme.label(p, "TournamentText", "",
		20, UITheme.TEXT_DIM, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(0, -285), Vector2(1100, 56))
	_tournament_note_text = UITheme.label(p, "TournamentNoteText", "",
		18, UITheme.ACCENT_TEXT, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(0, -340), Vector2(1100, 26))
	_bracket_text = UITheme.label(p, "BracketText", "",
		18, UITheme.TEXT_WHITE, HORIZONTAL_ALIGNMENT_LEFT, VERTICAL_ALIGNMENT_TOP,
		top, top, Vector2(0, -455), Vector2(1100, 200))
	_online_text = UITheme.label(p, "OnlineText", "",
		16, UITheme.TEXT_DIM, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		Vector2(0.5, 0.0), Vector2(0.5, 0.0), Vector2(0, 100), Vector2(1100, 24))


func _build_training_panel(root: Control) -> void:
	_training_panel = _tab_panel(root, "TrainingPanel")
	var p := _training_panel
	var top := Vector2(0.5, 1.0)
	_category_buttons.clear()
	var categories: Array = _d("categories", ["Footwork", "Kỹ thuật", "Thể lực", "Chiến thuật"])
	for i in categories.size():
		var cat := str(categories[i])
		var x := -490.0 + (i % 4) * 330.0
		var y := -185.0 - (i / 4) * 58.0
		var b := UITheme.button(p, "CategoryButton%d" % i, cat, 20,
			top, top, Vector2(x, y), Vector2(300, 48))
		b.pressed.connect(func() -> void: category_selected.emit(cat))
		_category_buttons.append(b)

	var scroll := ScrollContainer.new()
	scroll.name = "DrillScroll"
	UITheme.place(scroll, top, top, Vector2(-320, -310), Vector2(600, 180))
	p.add_child(scroll)
	_drill_list_box = VBoxContainer.new()
	_drill_list_box.name = "DrillList"
	_drill_list_box.add_theme_constant_override("separation", 6)
	scroll.add_child(_drill_list_box)

	_drill_detail_text = UITheme.label(p, "DrillDetailText", "",
		20, UITheme.TEXT_WHITE, HORIZONTAL_ALIGNMENT_LEFT, VERTICAL_ALIGNMENT_TOP,
		top, top, Vector2(320, -400), Vector2(480, 150))
	_start_drill_button = UITheme.button(p, "StartDrillButton", "Vào tập", 22,
		top, top, Vector2(320, -560), Vector2(300, 52))
	_start_drill_button.disabled = true
	UITheme.label(p, "DrillNoteText",
		"Bài tập sẽ chơi được khi Practice tích hợp drill (bản sau).",
		16, UITheme.TEXT_DIM, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(320, -600), Vector2(480, 24))


func _build_shop_panel(root: Control) -> void:
	_shop_panel = _tab_panel(root, "ShopPanel")
	var p := _shop_panel
	var top := Vector2(0.5, 1.0)
	_shop_money_text = UITheme.label(p, "ShopMoneyText", "",
		22, UITheme.ACCENT_TEXT, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(0, -175), Vector2(600, 30))
	var slot_names: Array[String] = ["Vợt", "Giày", "Áo"]
	_slot_buttons.clear()
	for i in 3:
		var slot := i
		var b := UITheme.button(p, "SlotButton%d" % i, slot_names[i], 20,
			top, top, Vector2(-320 + i * 320, -225), Vector2(300, 48))
		b.pressed.connect(func() -> void: slot_selected.emit(slot))
		_slot_buttons.append(b)

	var scroll := ScrollContainer.new()
	scroll.name = "ShopScroll"
	UITheme.place(scroll, top, top, Vector2(-320, -300), Vector2(600, 300))
	p.add_child(scroll)
	_shop_list_box = VBoxContainer.new()
	_shop_list_box.name = "ShopList"
	_shop_list_box.add_theme_constant_override("separation", 6)
	scroll.add_child(_shop_list_box)

	_shop_detail_text = UITheme.label(p, "ShopDetailText", "",
		20, UITheme.TEXT_WHITE, HORIZONTAL_ALIGNMENT_LEFT, VERTICAL_ALIGNMENT_TOP,
		top, top, Vector2(320, -400), Vector2(480, 150))
	_buy_button = UITheme.button(p, "BuyButton", "Mua", 22,
		top, top, Vector2(220, -560), Vector2(220, 52))
	_buy_button.pressed.connect(func() -> void: buy_requested.emit(str(_d("selected_item_id", ""))))
	_equip_button = UITheme.button(p, "EquipButton", "Đeo", 22,
		top, top, Vector2(460, -560), Vector2(220, 52))
	_equip_button.pressed.connect(func() -> void: equip_requested.emit(str(_d("selected_item_id", ""))))
	_shop_message_text = UITheme.label(p, "ShopMessageText", "",
		18, UITheme.ACCENT_TEXT, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(320, -600), Vector2(480, 26))
