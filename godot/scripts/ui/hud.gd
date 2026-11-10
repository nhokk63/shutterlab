extends CanvasLayer
class_name HudManager
## HudManager — port of Unity UI/HUDManager.cs.
## In-match broadcast HUD: score bar at the top (names, points, game,
## server), Deuce / Match-point callout, toast messages, pause button,
## stamina bars at the bottom, pause panel, and the results screen.
##
## Data flow (read-only, never touches physics or scoring):
##  - bind_rules(rules, user_name, ai_name) then the HUD polls the
##    score every frame and refreshes only when it changes.
##  - show_toast(msg): 1.5 s, measured in real time so it still runs
##    while paused (Unity: unscaled time).
##  - set_stamina(player, normalized, is_low) for the bottom bars.
##  - show_results(winner, stats, coins) delegates to ResultsScreen.
## Buttons emit signals; the match owner drives the real pause/restart
## (get_tree().paused = true, MatchRules reset, ...).
## Set process_mode ALWAYS so buttons work while the tree is paused.

signal pause_requested
signal resume_requested
signal restart_requested
signal quit_requested
signal rematch_requested
signal menu_requested

const TOAST_DURATION_MS := 1500

var rules: MatchRules = null
var user_name: String = "Bạn"
var ai_name: String = "Đối thủ"

var _board := Scoreboard.new()
var _last_score_text := ""
var _last_sub_text := ""
var _last_deuce_text := ""
var _toast_until_ms := 0

var _names_label: Label
var _score_label: Label
var _sub_label: Label
var _deuce_label: Label
var _toast_panel: Panel
var _toast_label: Label
var _stamina_panel_user: Panel
var _stamina_bar_user: ProgressBar
var _stamina_label_user: Label
var _stamina_panel_ai: Panel
var _stamina_bar_ai: ProgressBar
var _stamina_label_ai: Label
var _pause_panel: Panel
var _results: ResultsScreen


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	_build()


func _process(_delta: float) -> void:
	if _toast_until_ms > 0 and Time.get_ticks_msec() >= _toast_until_ms:
		_toast_until_ms = 0
		if _toast_panel != null:
			_toast_panel.visible = false
	if rules != null:
		_refresh_score()


# --- Binding ---------------------------------------------------------------

func bind_rules(p_rules: MatchRules, p_user_name: String, p_ai_name: String) -> void:
	rules = p_rules
	user_name = p_user_name if p_user_name != "" else "Bạn"
	ai_name = p_ai_name if p_ai_name != "" else "Đối thủ"
	_last_score_text = ""
	_last_sub_text = ""
	_last_deuce_text = ""
	_refresh_names()
	_refresh_score()


func refresh_names() -> void:
	_refresh_names()


func show_toast(message: String) -> void:
	if message == "" or _toast_panel == null:
		return
	_toast_label.text = message
	_toast_panel.visible = true
	_toast_until_ms = Time.get_ticks_msec() + TOAST_DURATION_MS


func set_stamina(player: int, normalized: float, is_low: bool) -> void:
	var n := clampf(normalized, 0.0, 1.0)
	if player == 0:
		if _stamina_panel_user != null:
			_stamina_panel_user.visible = true
		if _stamina_bar_user != null:
			_stamina_bar_user.value = n
			UITheme.set_stamina_color(_stamina_bar_user, n)
		if _stamina_label_user != null:
			_stamina_label_user.visible = true
			_stamina_label_user.text = "Thể lực (thấp!)" if is_low else "Thể lực"
	else:
		if _stamina_panel_ai != null:
			_stamina_panel_ai.visible = true
		if _stamina_bar_ai != null:
			_stamina_bar_ai.value = n
			UITheme.set_stamina_color(_stamina_bar_ai, n)
		if _stamina_label_ai != null:
			_stamina_label_ai.visible = true


func set_paused(paused: bool) -> void:
	if _pause_panel == null:
		return
	var results_open := _results != null and _results.visible
	_pause_panel.visible = paused and not results_open


func show_results(winner: int, stats: Dictionary, coins_earned: int = 0) -> void:
	if _pause_panel != null:
		_pause_panel.visible = false
	if _results == null or rules == null:
		return
	_results.show_results(winner, rules.score, stats, user_name, ai_name, coins_earned)


func hide_results() -> void:
	if _results != null:
		_results.hide_results()


# --- Score display ----------------------------------------------------------

func _refresh_names() -> void:
	if _names_label != null:
		_names_label.text = "%s  vs  %s" % [user_name, ai_name]


func _refresh_score() -> void:
	if rules == null:
		return
	_board.update(rules.score)
	var score_text := _board.format_score()
	if score_text != _last_score_text:
		_last_score_text = score_text
		if _score_label != null:
			_score_label.text = score_text
	var sub_text := "Ván %d  •  Thắng ván: %s  •  %s" % [
		_board.score.current_game, _board.format_games(), _board.server_indicator()]
	if sub_text != _last_sub_text:
		_last_sub_text = sub_text
		if _sub_label != null:
			_sub_label.text = sub_text
	var deuce_text := ""
	if _board.is_deuce():
		deuce_text = "Deuce — phải hơn 2 điểm!"
	elif _board.is_match_point(0):
		deuce_text = "Match point — Bạn!"
	elif _board.is_match_point(1):
		deuce_text = "Match point — Đối thủ!"
	if deuce_text != _last_deuce_text:
		_last_deuce_text = deuce_text
		if _deuce_label != null:
			_deuce_label.visible = deuce_text != ""
			_deuce_label.text = deuce_text


# --- Buttons -----------------------------------------------------------------

func _on_pause_button() -> void:
	pause_requested.emit()
	if AudioManager.instance != null:
		AudioManager.instance.play_click()


func _on_resume_button() -> void:
	resume_requested.emit()


func _on_restart_button() -> void:
	restart_requested.emit()


func _on_quit_button() -> void:
	quit_requested.emit()


# --- Build (code-built UI, Unity coordinates, 1280x720) ---------------------

func _build() -> void:
	var root := Control.new()
	root.name = "HUDRoot"
	UITheme.place_full(root)
	add_child(root)

	var top := Vector2(0.5, 1.0)
	# --- Top broadcast bar ---
	var top_bar := UITheme.panel(root, "TopBar", UITheme.PANEL_DARK,
		top, top, Vector2(0, -52), Vector2(480, 96))
	_names_label = UITheme.label(top_bar, "NamesText", "Bạn  vs  Đối thủ",
		20, UITheme.TEXT_DIM, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(0, -16), Vector2(460, 26))
	_score_label = UITheme.label(top_bar, "ScoreText", "0-0",
		34, UITheme.TEXT_WHITE, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(0, -50), Vector2(460, 44))
	_sub_label = UITheme.label(top_bar, "SubText", "",
		18, UITheme.TEXT_DIM, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(0, -82), Vector2(460, 24))

	# --- Deuce / Match-point callout ---
	_deuce_label = UITheme.label(root, "DeuceText", "",
		22, UITheme.ACCENT_TEXT, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(0, -118), Vector2(480, 30))
	_deuce_label.visible = false

	# --- Toast ---
	_toast_panel = UITheme.panel(root, "ToastPanel", UITheme.PANEL_DARK,
		top, top, Vector2(0, -160), Vector2(600, 44))
	_toast_label = UITheme.label(_toast_panel, "ToastText", "",
		22, UITheme.TEXT_WHITE, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		Vector2(0.5, 0.5), Vector2(0.5, 0.5), Vector2(0, 0), Vector2(600, 44))
	_toast_panel.visible = false

	# --- Pause button, top-right corner ---
	var pause_btn := UITheme.button(root, "PauseButton", "II", 28,
		Vector2(1, 1), Vector2(1, 1), Vector2(-48, -48), Vector2(64, 64))
	pause_btn.pressed.connect(_on_pause_button)

	# --- Stamina bars, bottom corners ---
	_stamina_panel_user = UITheme.panel(root, "StaminaPanelUser",
		Color(0, 0, 0, 0.55), Vector2.ZERO, Vector2.ZERO,
		Vector2(120, 30), Vector2(220, 24))
	_stamina_bar_user = UITheme.stamina_bar(_stamina_panel_user, "StaminaFillUser",
		Vector2.ZERO, Vector2.ONE, Vector2(0, 0), Vector2(220, 24))
	_stamina_label_user = UITheme.label(root, "StaminaLabelUser", "Thể lực",
		18, UITheme.TEXT_DIM, HORIZONTAL_ALIGNMENT_LEFT, VERTICAL_ALIGNMENT_CENTER,
		Vector2.ZERO, Vector2.ZERO, Vector2(70, 66), Vector2(140, 24))
	_stamina_panel_user.visible = false
	_stamina_label_user.visible = false

	_stamina_panel_ai = UITheme.panel(root, "StaminaPanelAI",
		Color(0, 0, 0, 0.55), Vector2.ONE, Vector2.ONE,
		Vector2(-120, 30), Vector2(220, 24))
	_stamina_bar_ai = UITheme.stamina_bar(_stamina_panel_ai, "StaminaFillAI",
		Vector2.ZERO, Vector2.ONE, Vector2(0, 0), Vector2(220, 24))
	_stamina_label_ai = UITheme.label(root, "StaminaLabelAI", "Thể lực đối thủ",
		18, UITheme.TEXT_DIM, HORIZONTAL_ALIGNMENT_RIGHT, VERTICAL_ALIGNMENT_CENTER,
		Vector2.ONE, Vector2.ONE, Vector2(-70, 66), Vector2(140, 24))
	_stamina_panel_ai.visible = false
	_stamina_label_ai.visible = false

	# --- Pause panel ---
	pause_panel_build(root)

	# --- Results screen (child, hidden until the match ends) ---
	_results = ResultsScreen.new()
	_results.name = "ResultsScreen"
	UITheme.place_full(_results)
	root.add_child(_results)
	_results.rematch_requested.connect(func() -> void: rematch_requested.emit())
	_results.menu_requested.connect(func() -> void: menu_requested.emit())


func pause_panel_build(root: Control) -> void:
	_pause_panel = UITheme.panel(root, "PausePanel", UITheme.DIM_OVERLAY,
		Vector2.ZERO, Vector2.ONE, Vector2(0, 0), Vector2(0, 0))
	var center := Vector2(0.5, 0.5)
	var card := UITheme.panel(_pause_panel, "PauseCard", UITheme.PANEL_SOLID,
		center, center, Vector2(0, 0), Vector2(400, 340))
	var top := Vector2(0.5, 1.0)
	UITheme.label(card, "PauseTitle", "Tạm dừng",
		32, UITheme.TEXT_WHITE, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(0, -45), Vector2(360, 44))
	var resume_btn := UITheme.button(card, "ResumeButton", "Tiếp tục", 24,
		top, top, Vector2(0, -120), Vector2(280, 56))
	resume_btn.pressed.connect(_on_resume_button)
	var restart_btn := UITheme.button(card, "RestartButton", "Chơi lại", 24,
		top, top, Vector2(0, -190), Vector2(280, 56))
	restart_btn.pressed.connect(_on_restart_button)
	var quit_btn := UITheme.button(card, "QuitButton", "Về menu", 24,
		top, top, Vector2(0, -260), Vector2(280, 56))
	UITheme.set_button_danger(quit_btn)
	quit_btn.pressed.connect(_on_quit_button)
	_pause_panel.visible = false
