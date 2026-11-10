class_name GameBootstrap
extends Node
## Builds a match scene's wiring: creates the two players, the AI
## opponent, the match manager, and connects input + camera, then
## starts the match. Contains no gameplay logic — logic lives in
## MatchManager / PlayerController / OpponentAI.
## Port of ShuttleLab.Core.GameBootstrap (Unity C#).
##
## The AI does NOT serve itself — MatchManager serves for the AI
## (see OpponentAI). The camera preset comes from GameState.
##
## Usage (from the match scene, e.g. main.gd's successor):
##   var bootstrap := GameBootstrap.new()
##   add_child(bootstrap)
##   var match := bootstrap.setup(shuttle, camera, input_manager,
##       GameMode.AppMode.QUICK_MATCH, difficulty, game_state)

signal match_ready(match: MatchManager)

var match_manager: MatchManager
var opponent_ai: OpponentAI
var game_state: GameState

## Wire a match. shuttle / camera / input_manager must already exist
## in the scene. Returns the MatchManager (also emitted via match_ready).
func setup(shuttle: Shuttlecock, camera: Camera3D, input_manager: MobileInputManager,
		mode: int, difficulty: int, p_game_state: GameState = null,
		user_stats: PlayerStats = null, ai_stats: PlayerStats = null) -> MatchManager:
	game_state = p_game_state if p_game_state != null else GameState.new()
	game_state.load_settings()
	game_state.current_mode = mode
	if mode != GameMode.AppMode.MENU:
		game_state.difficulty = clampi(difficulty, 0, 5)

	# --- Match ------------------------------------------------------
	match_manager = MatchManager.new()
	match_manager.name = "MatchManager"
	add_child(match_manager)
	match_manager.shuttle = shuttle

	# --- Players ------------------------------------------------------
	var user := PlayerController.new()
	user.name = "Player0"
	add_child(user)
	var ai_player := PlayerController.new()
	ai_player.name = "Player1"
	add_child(ai_player)
	match_manager.players = [user, ai_player]

	# Stats: the user uses their character (roster not ported yet, so
	# DefaultPlayer); the AI uses stats from the difficulty level.
	var u_stats := user_stats if user_stats != null else PlayerStats.default_player()
	var a_stats := ai_stats if ai_stats != null else PlayerStats.from_difficulty(difficulty)
	user.init(0, u_stats, shuttle, true)
	ai_player.init(1, a_stats, shuttle, false)

	# --- Mode context ---------------------------------------------------
	var game_mode := GameMode.new()
	game_mode.mode = mode
	game_mode.difficulty = difficulty
	game_mode.player_character_id = 0
	if mode == GameMode.AppMode.CAREER:
		game_mode.career = CareerManager.new()
		game_mode.career.active = true
	if mode == GameMode.AppMode.TOURNAMENT:
		game_mode.tournament = TournamentManager.new()
	match_manager.game_mode = game_mode

	# --- AI opponent ----------------------------------------------------
	opponent_ai = OpponentAI.new()
	opponent_ai.name = "OpponentAI"
	add_child(opponent_ai)
	opponent_ai.init(ai_player, match_manager, difficulty)

	# --- Input / camera ---------------------------------------------------
	if input_manager != null:
		input_manager.bind(user, match_manager)
		input_manager.apply_left_hand_mode(game_state.left_hand_mode)
		match_manager.bind_input(input_manager)

	if camera != null:
		var camera_manager := CameraManager.new()
		camera_manager.name = "CameraManager"
		add_child(camera_manager)
		camera_manager.set_camera(camera)
		camera_manager.set_targets(shuttle)
		camera_manager.set_preset(game_state.camera_preset)

	match_ready.emit(match_manager)
	match_manager.start_match(difficulty, mode == GameMode.AppMode.PRACTICE)
	return match_manager

## Convenience: Quick Match vs the AI at the given difficulty.
func start_quick_match(shuttle: Shuttlecock, camera: Camera3D,
		input_manager: MobileInputManager, difficulty: int) -> MatchManager:
	return setup(shuttle, camera, input_manager, GameMode.AppMode.QUICK_MATCH, difficulty)

## Convenience: Practice — the AI returns everything it can reach.
func start_practice(shuttle: Shuttlecock, camera: Camera3D,
		input_manager: MobileInputManager, difficulty: int) -> MatchManager:
	return setup(shuttle, camera, input_manager, GameMode.AppMode.PRACTICE, difficulty)

## Pause / quit helpers (the UI buttons call these).
func toggle_pause() -> void:
	if match_manager != null:
		match_manager.pause(match_manager.phase != MatchManager.Phase.PAUSED)

func quit_to_menu() -> void:
	if match_manager != null:
		match_manager.quit_to_menu()
