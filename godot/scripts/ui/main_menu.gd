extends CanvasLayer
class_name MainMenu
## MainMenu — port of Unity UI/MainMenu.cs.
## Main menu: player name, 6 AI difficulty buttons, character browser
## (< > + unlock), mode buttons (Đấu nhanh / Luyện tập / Sự nghiệp /
## Giải đấu), settings button. Built entirely in code.
##
## Differences from Unity (deliberate, per the full-port spec):
##  - A "Giải đấu" button is added: the brief lists Tournament as a
##    mode, but Unity's menu only entered it from inside CareerUI.
##    It emits tournament_requested; the parent routes to the career
##    tournament flow.
##  - Subtitle says "Godot 4.5" instead of "Unity 6".
##  - Version text moved to the top-right corner (the extra mode
##    button leaves no room at the bottom).
##
## Character unlock: the roster data (stats + unlock text) is ported
## from CharacterRoster.cs. Unlock conditions are evaluated locally
## from a career snapshot (set_career_snapshot); the actual purchase
## is the progression layer's job — the unlock button emits
## character_unlock_requested(id) and the parent confirms via
## set_character_unlocked(id, true).

signal quick_match_requested(difficulty: int)
signal practice_requested(difficulty: int)
signal career_requested
signal tournament_requested
signal character_unlock_requested(character_id: int)

# Roster ported from CharacterRoster.cs (Power/Accuracy/Speed/Stamina/
# Defense; unlock text as shown in the Unity menu).
const ROSTER: Array[Dictionary] = [
	{"name": "Minh Quân", "power": 60, "accuracy": 62, "speed": 60,
		"stamina": 60, "defense": 58, "unlock_text": "Đã mở",
		"unlock_coins": 0, "unlock_rank": 0, "unlock_titles": 0},
	{"name": "Bảo Long", "power": 78, "accuracy": 55, "speed": 58,
		"stamina": 62, "defense": 52, "unlock_text": "1000 xu",
		"unlock_coins": 1000, "unlock_rank": 0, "unlock_titles": 0},
	{"name": "Thanh Thảo", "power": 52, "accuracy": 70, "speed": 80,
		"stamina": 65, "defense": 60, "unlock_text": "hạng 256 trở lên",
		"unlock_coins": 0, "unlock_rank": 256, "unlock_titles": 0},
	{"name": "Đức Anh", "power": 55, "accuracy": 66, "speed": 60,
		"stamina": 72, "defense": 82, "unlock_text": "2500 xu",
		"unlock_coins": 2500, "unlock_rank": 0, "unlock_titles": 0},
	{"name": "Hải Yến", "power": 58, "accuracy": 84, "speed": 66,
		"stamina": 60, "defense": 64, "unlock_text": "vô địch 1 giải đấu",
		"unlock_coins": 0, "unlock_rank": 0, "unlock_titles": 1},
	{"name": "Quốc Huy", "power": 76, "accuracy": 74, "speed": 74,
		"stamina": 72, "defense": 72, "unlock_text": "4000 xu",
		"unlock_coins": 4000, "unlock_rank": 0, "unlock_titles": 0},
]

var settings: GameSettings = null

var _difficulty: int = 2
var _character_index: int = 0
var _selected_character: int = 0
var _unlocked: Array[bool] = [true, false, false, false, false, false]
var _coins: int = 0
var _rank: int = 512
var _titles: int = 0

var _name_input: LineEdit
var _difficulty_buttons: Array[Button] = []
var _character_info: Label
var _character_unlock_button: Button
var _settings_panel: SettingsPanel


func _ready() -> void:
	_build()
	_refresh_difficulty_buttons()
	_refresh_character_ui()


func setup(p_settings: GameSettings) -> void:
	settings = p_settings
	if settings != null:
		_difficulty = settings.difficulty
		if _name_input != null:
			_name_input.text = settings.player_name
	_refresh_difficulty_buttons()


func set_career_snapshot(coins: int, rank: int, titles_won: int) -> void:
	_coins = coins
	_rank = rank
	_titles = titles_won
	_refresh_character_ui()


func set_character_unlocked(character_id: int, unlocked: bool) -> void:
	if character_id < 0 or character_id >= _unlocked.size():
		return
	_unlocked[character_id] = unlocked
	if unlocked:
		_selected_character = character_id
	_refresh_character_ui()


# --- Buttons ------------------------------------------------------------------

func _on_quick_match() -> void:
	quick_match_requested.emit(_difficulty)


func _on_practice() -> void:
	practice_requested.emit(_difficulty)


func _on_career() -> void:
	career_requested.emit()


func _on_tournament() -> void:
	tournament_requested.emit()


func _on_settings() -> void:
	if _settings_panel != null:
		_settings_panel.show_panel()


func _on_difficulty_button(level: int) -> void:
	_difficulty = clampi(level, 0, 5)
	if settings != null:
		settings.difficulty = _difficulty
		settings.save()
	_refresh_difficulty_buttons()


func _on_character_prev() -> void:
	_browse_character(-1)


func _on_character_next() -> void:
	_browse_character(1)


func _on_character_unlock() -> void:
	character_unlock_requested.emit(_character_index)


func _on_name_submitted(new_text: String) -> void:
	var value := new_text.strip_edges()
	if value == "":
		value = "Bạn"
	if settings != null:
		settings.player_name = value
		settings.save()
	if _name_input != null:
		_name_input.text = value


# --- Display -------------------------------------------------------------------

func _browse_character(delta: int) -> void:
	_character_index = (_character_index + delta + ROSTER.size()) % ROSTER.size()
	# Auto-select when already unlocked; a locked character is view-only.
	if _unlocked[_character_index]:
		_selected_character = _character_index
	_refresh_character_ui()


func _is_unlock_condition_met(index: int) -> bool:
	var entry: Dictionary = ROSTER[index]
	if int(entry["unlock_coins"]) > 0 and _coins < int(entry["unlock_coins"]):
		return false
	if int(entry["unlock_rank"]) > 0 and _rank > int(entry["unlock_rank"]):
		return false
	if int(entry["unlock_titles"]) > 0 and _titles < int(entry["unlock_titles"]):
		return false
	return true


func _refresh_difficulty_buttons() -> void:
	for i in _difficulty_buttons.size():
		UITheme.set_button_selected(_difficulty_buttons[i], i == _difficulty)


func _refresh_character_ui() -> void:
	if _character_info == null:
		return
	var entry: Dictionary = ROSTER[_character_index]
	var unlocked := _unlocked[_character_index]
	var selected := _selected_character == _character_index
	var line1: String = entry["name"]
	if selected:
		line1 += " (Đang chọn)"
	elif not unlocked:
		line1 += " (Khóa - " + str(entry["unlock_text"]) + ")"
	var line2 := "SM %d • CX %d • TĐ %d • TL %d • PT %d" % [
		entry["power"], entry["accuracy"], entry["speed"],
		entry["stamina"], entry["defense"]]
	_character_info.text = line1 + "\n" + line2
	if _character_unlock_button != null:
		_character_unlock_button.visible = (not unlocked) and _is_unlock_condition_met(_character_index)


# --- Build (code-built UI, Unity coordinates, 1280x720) -----------------------

func _build() -> void:
	var root := Control.new()
	root.name = "MenuRoot"
	UITheme.place_full(root)
	add_child(root)

	UITheme.panel(root, "MenuBackground", UITheme.MENU_BACKGROUND,
		Vector2.ZERO, Vector2.ONE, Vector2(0, 0), Vector2(0, 0))

	var top := Vector2(0.5, 1.0)
	UITheme.label(root, "TitleText", "SHUTTLELAB",
		64, UITheme.ACCENT_TEXT, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(0, -70), Vector2(700, 80))
	UITheme.label(root, "SubtitleText", "Cầu lông 3D  •  Godot 4.5",
		22, UITheme.TEXT_DIM, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(0, -125), Vector2(700, 30))
	UITheme.label(root, "VersionText", "ShuttleLab v0.1.0",
		16, UITheme.TEXT_DIM, HORIZONTAL_ALIGNMENT_RIGHT, VERTICAL_ALIGNMENT_CENTER,
		Vector2(1, 1), Vector2(1, 1), Vector2(-70, -18), Vector2(140, 22))

	UITheme.label(root, "NameLabel", "Tên người chơi",
		22, UITheme.TEXT_WHITE, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(0, -175), Vector2(400, 28))
	var saved_name := "Bạn"
	if settings != null:
		saved_name = settings.player_name
	_name_input = UITheme.input_field(root, "PlayerNameInput",
		"Nhập tên của bạn...", saved_name,
		top, top, Vector2(0, -215), Vector2(360, 48))
	_name_input.text_submitted.connect(_on_name_submitted)

	UITheme.label(root, "DifficultyLabel", "Độ khó AI",
		22, UITheme.TEXT_WHITE, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(0, -285), Vector2(400, 28))
	_difficulty_buttons.clear()
	for i in 6:
		var level := i
		var x := -375.0 + i * 150.0
		var b := UITheme.button(root, "DifficultyButton%d" % i,
			GameSettings.difficulty_name(i), 20,
			top, top, Vector2(x, -325), Vector2(140, 52))
		b.pressed.connect(func() -> void: _on_difficulty_button(level))
		_difficulty_buttons.append(b)

	# Character browser row (y=-388). "<" ">" are ASCII on purpose, like
	# Unity (the default font is guaranteed to render them).
	var prev_btn := UITheme.button(root, "CharacterPrevButton", "<", 20,
		top, top, Vector2(-228, -388), Vector2(52, 48))
	prev_btn.pressed.connect(_on_character_prev)
	_character_info = UITheme.label(root, "CharacterInfoText", "",
		16, UITheme.TEXT_WHITE, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(0, -388), Vector2(400, 48))
	var next_btn := UITheme.button(root, "CharacterNextButton", ">", 20,
		top, top, Vector2(228, -388), Vector2(52, 48))
	next_btn.pressed.connect(_on_character_next)
	_character_unlock_button = UITheme.button(root, "CharacterUnlockButton", "Mở khóa", 20,
		top, top, Vector2(330, -388), Vector2(150, 48))
	_character_unlock_button.pressed.connect(_on_character_unlock)
	_character_unlock_button.visible = false

	# Mode buttons (compressed to fit the extra Tournament button).
	var quick_btn := UITheme.button(root, "QuickMatchButton", "Đấu nhanh", 26,
		top, top, Vector2(0, -442), Vector2(340, 50))
	quick_btn.pressed.connect(_on_quick_match)
	var practice_btn := UITheme.button(root, "PracticeButton", "Luyện tập", 26,
		top, top, Vector2(0, -496), Vector2(340, 50))
	practice_btn.pressed.connect(_on_practice)
	var career_btn := UITheme.button(root, "CareerButton", "Sự nghiệp", 26,
		top, top, Vector2(0, -550), Vector2(340, 50))
	career_btn.pressed.connect(_on_career)
	var tournament_btn := UITheme.button(root, "TournamentButton", "Giải đấu", 26,
		top, top, Vector2(0, -604), Vector2(340, 50))
	tournament_btn.pressed.connect(_on_tournament)
	UITheme.label(root, "CareerNoteText",
		"Chơi theo tuần: tập luyện rồi thi đấu, thăng hạng qua các cấp giải.",
		18, UITheme.TEXT_DIM, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(0, -640), Vector2(560, 26))

	var settings_btn := UITheme.button(root, "SettingsButton", "Cài đặt", 24,
		top, top, Vector2(0, -678), Vector2(340, 44))
	settings_btn.pressed.connect(_on_settings)

	# Settings panel (hidden until opened). CanvasLayer needs no anchors.
	_settings_panel = SettingsPanel.new()
	_settings_panel.name = "SettingsPanel"
	root.add_child(_settings_panel)
