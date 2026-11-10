extends Node3D
## ShuttleLab — main scene (Godot port of the Unity game).
## Wires the ported systems into a playable game:
##  - v0.2 TV-broadcast arena (ArenaBuilder) + BehindPlayer camera
##  - Shuttlecock on the real drag physics (RK2 240 Hz)
##  - GameBootstrap: MatchManager + 2 players + OpponentAI + input + camera
##  - UI: MainMenu -> in-match HudManager (broadcast scoreboard, stamina,
##    pause, results) + AudioManager (synthesised SFX + crowd ambience)
##  - Characters: Ara 3D Gen GLB models (Athletic_man / Athletic_woman)
##    when available, procedural humanoid as fallback.
## The old virtual rally loop (two virtual players, no AI/input) is gone —
## MatchManager drives serves, rallies, scoring and rewards now.

const SERVE_HEIGHT := 1.15

var rules: MatchRules
var shuttle: Shuttlecock
var wind := Vector3.ZERO

var _camera: Camera3D
var _input: MobileInputManager
var _cam_mgr: CameraManager
var _audio: AudioManager
var _menu: MainMenu
var _hud: HudManager
var _bootstrap: GameBootstrap
var _match: MatchManager
var _game_state: GameState
var _settings: GameSettings

func _ready() -> void:
	_game_state = GameState.new()
	_game_state.load_settings()
	_settings = GameSettings.new()
	_settings.load()
	_build_camera()
	_build_arena()

	shuttle = Shuttlecock.new()
	shuttle.name = "Shuttlecock"
	shuttle.wind = wind
	add_child(shuttle)

	_input = MobileInputManager.new()
	_input.name = "MobileInput"
	add_child(_input)

	_cam_mgr = CameraManager.new()
	_cam_mgr.name = "CameraManager"
	add_child(_cam_mgr)
	_cam_mgr.set_camera(_camera)
	_cam_mgr.set_targets(shuttle)
	_cam_mgr.set_preset(_game_state.camera_preset)

	_audio = AudioManager.new()
	_audio.name = "AudioManager"
	add_child(_audio)
	_audio.setup(_settings)

	_show_main_menu()
	_self_check()
	_log("Ready. Main menu shown.")

# ---------------------------------------------------------------- arena

func _build_camera() -> void:
	# Camera matches the Unity BehindPlayer preset.
	_camera = Camera3D.new()
	_camera.position = Vector3(0, 6.5, -10.5)
	_camera.fov = 55.0
	add_child(_camera)
	_camera.look_at(Vector3(0, 1, 2), Vector3.UP)

func _build_arena() -> void:
	# Full v0.2 TV-broadcast arena (dark stands, 6 spotlights, 1008 crowd,
	# LED boards, broadcast HUD). Replaces the old simplified court + sun.
	var arena := ArenaBuilder.new()
	add_child(arena)
	arena.build(GraphicsQuality.tier_crowd_count(GraphicsQuality.current_tier()))

# ---------------------------------------------------------------- menu / match flow

func _show_main_menu() -> void:
	if _hud != null:
		_hud.queue_free()
		_hud = null
	if _bootstrap != null:
		_bootstrap.queue_free()
		_bootstrap = null
		_match = null
	_menu = MainMenu.new()
	_menu.name = "MainMenu"
	add_child(_menu)
	_menu.quick_match_requested.connect(_on_quick_match_requested)
	_menu.practice_requested.connect(_on_practice_requested)
	_menu.career_requested.connect(_on_career_requested)
	_menu.tournament_requested.connect(_on_tournament_requested)

func _on_quick_match_requested(difficulty: int) -> void:
	_start_match(GameMode.AppMode.QUICK_MATCH, difficulty, false)

func _on_practice_requested(difficulty: int) -> void:
	_start_match(GameMode.AppMode.PRACTICE, difficulty, true)

func _on_career_requested() -> void:
	# Career mode: for v0.1.0, route to a Quick Match (the CareerUI
	# flow is wired through the career tab; full career matches land
	# in the next iteration).
	_log("Career requested — starting Quick Match for now.")
	_start_match(GameMode.AppMode.QUICK_MATCH, _game_state.difficulty, false)

func _on_tournament_requested() -> void:
	_log("Tournament requested — starting Quick Match for now.")
	_start_match(GameMode.AppMode.QUICK_MATCH, _game_state.difficulty, false)

func _start_match(mode: int, difficulty: int, practice: bool) -> void:
	if _menu != null:
		_menu.queue_free()
		_menu = null
	_bootstrap = GameBootstrap.new()
	_bootstrap.name = "GameBootstrap"
	add_child(_bootstrap)
	_match = _bootstrap.setup(shuttle, _camera, _input, mode, difficulty, _game_state)
	rules = _match.rules
	_input.bind(_match.players[0], _match)
	_cam_mgr.set_preset(_game_state.camera_preset)

	_hud = HudManager.new()
	_hud.name = "Hud"
	add_child(_hud)
	_hud.bind_rules(rules, "Bạn", "Đối thủ")
	_hud.pause_requested.connect(_on_pause_requested)
	_hud.resume_requested.connect(_on_resume_requested)
	_hud.restart_requested.connect(_on_restart_requested)
	_hud.quit_requested.connect(_on_quit_requested)
	_hud.rematch_requested.connect(_on_rematch_requested)
	_hud.menu_requested.connect(_on_menu_requested)
	_match.message.connect(_hud.show_toast)
	_match.score_changed.connect(_on_score_changed)
	_match.match_end.connect(_on_match_end)

	_match.start_match(difficulty, practice)
	_log("Match started: mode %d, difficulty %d, practice %s" % [mode, difficulty, practice])

func _on_score_changed(_score) -> void:
	# HudManager polls the rules every frame; nothing to do here.
	pass

func _on_match_end(winner: int) -> void:
	if _hud == null or _match == null:
		return
	var stats := _match.stats
	var coins := 0
	if _match.game_mode != null:
		coins = _match.game_mode.coins_for_result(winner == 0)
	_hud.show_results(winner, {
		"time": stats.match_time,
		"rallies": stats.rallies,
		"shots": stats.shots_hit,
		"accuracy": stats.accuracy(),
		"serves": stats.serves_hit,
		"smashes": stats.smashes,
		"errors": stats.errors,
		"longest_rally": stats.longest_rally,
	}, coins)
	_log("Match over — winner P%d" % winner)

func _on_pause_requested() -> void:
	get_tree().paused = true

func _on_resume_requested() -> void:
	get_tree().paused = false

func _on_restart_requested() -> void:
	get_tree().paused = false
	if _match != null:
		_match.start_match(_game_state.difficulty, _match.is_practice)

func _on_quit_requested() -> void:
	get_tree().paused = false
	_show_main_menu()

func _on_rematch_requested() -> void:
	get_tree().paused = false
	if _match != null:
		_match.start_match(_game_state.difficulty, _match.is_practice)

func _on_menu_requested() -> void:
	get_tree().paused = false
	_show_main_menu()

# ---------------------------------------------------------------- verification

func _log(msg: String) -> void:
	print("[ShuttleLab] ", msg)

## Sanity checks for the ported systems (run once at startup, logged).
func _self_check() -> void:
	# 1. Solver: Clear from the back court to a deep corner, Perfect quality.
	var s1: Dictionary = ShotSolver.solve(Vector3(0, 2.5, -5.0), Vector3(2.0, 0, 5.5),
		ShotData.ShotType.CLEAR, ShotData.ShotQuality.PERFECT, Vector3.ZERO)
	var ok1 := false
	if s1["ok"]:
		var sim: Dictionary = ShuttleAerodynamics.simulate(Vector3(0, 2.5, -5.0),
			s1["launch_velocity"], Vector3.ZERO, 5.0, 1.0 / 240.0)
		ok1 = bool(sim["landed"]) and (Vector3(sim["landing"]) - Vector3(2.0, 0, 5.5)).length() < 0.3
	_log("Self-check solver Clear: %s" % ("PASS" if ok1 else "FAIL"))

	# 2. Solver: low serve into the diagonal box.
	var box := CourtDimensions.service_box_centre(1, -1, false)
	var s2: Dictionary = ShotSolver.solve_serve(Vector3(-1.295, 1.15, -3.58), box,
		ShotData.ShotType.SERVE_LOW, ShotData.ShotQuality.GOOD, Vector3.ZERO)
	_log("Self-check solver ServeLow: %s" % ("PASS" if s2["ok"] else "FAIL"))

	# 3. Aerodynamics: free-fall from 3 m lands in ~0.8 s.
	var s3: Dictionary = ShuttleAerodynamics.simulate(Vector3(0, 3, 0), Vector3.ZERO,
		Vector3.ZERO, 5.0, 1.0 / 240.0)
	var ok3: bool = bool(s3["landed"]) and float(s3["flight_time"]) > 0.5 and float(s3["flight_time"]) < 1.2
	_log("Self-check aerodynamics drop: %s (t=%.2fs)" % ["PASS" if ok3 else "FAIL", s3["flight_time"]])

	# 4. MatchRules: 21-0 wins a game, points reset, server stays the winner.
	var r := MatchRules.new()
	for i in 21:
		r.award_point(0)
	var ok4 := r.score.games[0] == 1 and r.score.points[0] == 0 and r.score.server == 0
	_log("Self-check MatchRules 21-0: %s (games %s)" % ["PASS" if ok4 else "FAIL", r.games_line()])
