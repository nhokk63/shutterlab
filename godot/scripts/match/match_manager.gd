class_name MatchManager
extends Node
## State machine of a match: Serving -> Rally -> PointEnd / GameEnd ->
## MatchEnd. Owns the score (through MatchRules), the serve turn, and
## match statistics. It does NOT steer the AI — OpponentAI reads
## phase / shuttle and drives players[1] through PlayerController.
## The HUD only reads through the signals.
##
## HOW STATS ARE COUNTED (all observable from MatchManager):
## - Rallies: every time last_hitter changes while phase == Rally
##   (a real exchange; the serve does not count — it is hit in Serving).
## - ShotsHit: every shot + serve actually performed (serves counted in
##   do_serve, shots counted when last_hitter changes).
## - ShotsGood: a shot the opponent returned, or a shot that scored.
##   ServesGood: a serve that was not a fault (returned, or scored directly).
## - Errors: a point ending on the last hitter's fault (out, net, serve fault).
## - Smashes: counted in on_shot_executed (subscribed in start_match) —
##   only when the shuttle really launches after the fallback, never a
##   whiff or a solver failure.
## - A PlayerController serve / try_hit is ASYNCHRONOUS: serve() /
##   try_hit() only start the swing; the shuttle launches from the
##   animation's on_contact callback. So MatchManager never checks
##   shuttle.state right after calling serve / try_hit; shot counting
##   relies on last_hitter changing in _process.
## Port of ShuttleLab.Match.MatchManager (Unity C#).

enum Phase { IDLE, SERVING, RALLY, POINT_END, GAME_END, MATCH_END, PAUSED }

const POINT_END_DELAY := 1.2
const GAME_END_DELAY := 2.0  # time to read "game won"
const AI_SERVE_DELAY := 0.8
const STUCK_RALLY_TIMEOUT := 3.0

class MatchStats extends RefCounted:
	var rallies: int = 0  # total exchanges in rallies
	var smashes: int = 0  # Smash / JumpSmash performed (both sides)
	var user_smashes: int = 0  # Smash / JumpSmash by the USER (weekly mission)
	var errors: int = 0  # points lost on the last hitter's fault
	var shots_hit: int = 0  # total shots + serves performed
	var shots_good: int = 0  # shots the opponent returned or that scored
	var serves_good: int = 0  # serves that were not faults
	var match_time: float = 0.0  # match time (s, excluding pause)
	var longest_rally: int = 0  # longest rally (number of shots)

	func accuracy() -> float:
		return float(shots_good) / float(shots_hit) if shots_hit > 0 else 0.0

signal message(text: String)
signal score_changed(score)
signal match_end(winner: int)
signal quit_requested

var phase: int = Phase.IDLE
var rules: MatchRules
var shuttle: Shuttlecock
var players: Array = []  # [PlayerController, PlayerController]

var difficulty_level: int = 2
var is_practice: bool = false

var stats: MatchStats

## Optional mode context (GameMode) for end-of-match rewards.
## When null, no economy rewards are granted.
var game_mode: GameMode

var _input: MobileInputManager
var _resume_phase: int = Phase.SERVING

var _last_seen_hitter: int = -1
var _current_rally_hits: int = 0
var _pending_shot_hitter: int = -1
var _pending_is_serve: bool = false

var _ai_serve_timer: float = 0.0
var _point_end_timer: float = 0.0
var _pending_point_winner: int = 0
var _pending_match_won: bool = false
var _rally_stuck_timer: float = 0.0
var _shuttle_subscribed: bool = false
var _shots_subscribed: bool = false

func _init() -> void:
	rules = MatchRules.new()
	stats = MatchStats.new()

## The shuttle must be assigned (and players) before start_match.
func start_match(difficulty: int, practice: bool = false) -> void:
	difficulty_level = difficulty
	is_practice = practice
	rules = MatchRules.new()
	rules.reset()
	stats = MatchStats.new()
	_pending_shot_hitter = -1
	_pending_is_serve = false

	if players.size() >= 2 and players[0] != null and players[1] != null:
		(players[0] as PlayerController).home_position = Vector3(0, 0, -3.0)
		(players[1] as PlayerController).home_position = Vector3(0, 0, 3.0)

	_subscribe_shuttle()
	_subscribe_shot_executed()
	phase = Phase.SERVING
	score_changed.emit(rules.score)
	start_point()

func start_point() -> void:
	if phase == Phase.MATCH_END:
		return
	if rules == null:
		rules = MatchRules.new()
	if players.size() < 2 or players[0] == null or players[1] == null or shuttle == null:
		push_warning("[MatchManager] start_point: Players/Shuttle not assigned.")
		return

	var server: int = rules.score.server
	var receiver: int = rules.score.receiver
	var server_side := -1 if server == 0 else 1
	var server_court_x := rules.server_court_x_sign()

	var server_pos := CourtDimensions.server_position(server_side, server_court_x)
	var receiver_pos := CourtDimensions.receiver_position(rules.receiver_side_sign(), rules.target_box_x_sign())

	(players[server] as PlayerController).reset_for_point(server_pos)
	(players[receiver] as PlayerController).reset_for_point(receiver_pos)

	# Shuttle placed in front of the server, 1.1 m high, slightly toward the net.
	var hold_pos := Vector3(server_pos.x, 1.1, server_pos.z - server_side * 0.5)
	shuttle.hold_at(hold_pos)

	_current_rally_hits = 0
	_last_seen_hitter = server
	_pending_shot_hitter = -1
	_pending_is_serve = false
	_rally_stuck_timer = 0.0

	phase = Phase.SERVING
	if server == 1:
		_ai_serve_timer = AI_SERVE_DELAY
	score_changed.emit(rules.score)

func do_serve(serve_type: int) -> void:
	if phase != Phase.SERVING or rules == null or players.size() < 2 or shuttle == null:
		return
	if not ShotData.ShotLibrary.is_serve(serve_type):
		serve_type = ShotData.ShotType.SERVE_LOW

	var server: int = rules.score.server
	if players[server] == null:
		return

	var target := CourtDimensions.service_box_centre(rules.receiver_side_sign(), rules.target_box_x_sign(), false)

	# PlayerController.serve() is asynchronous: the shuttle launches
	# from the animation's on_contact callback, not right now.
	# If serve() returns false (already swinging / no shuttle), do not
	# change phase and do not count the stat — the watchdog re-serves.
	if not (players[server] as PlayerController).serve(serve_type, target):
		return
	stats.shots_hit += 1
	_pending_shot_hitter = server
	_pending_is_serve = true
	_last_seen_hitter = server
	phase = Phase.RALLY

## User serve (Serve button / Space key). Only on the user's turn.
func request_user_serve() -> void:
	if phase != Phase.SERVING:
		return
	if rules != null and rules.score.server != 0:
		return
	do_serve(ShotData.ShotType.SERVE_LOW)

func pause(paused: bool) -> void:
	if paused:
		if phase == Phase.PAUSED or phase == Phase.IDLE or phase == Phase.MATCH_END:
			return
		_resume_phase = phase
		phase = Phase.PAUSED
		get_tree().paused = true
	else:
		if phase != Phase.PAUSED:
			return
		phase = _resume_phase
		get_tree().paused = false

func quit_to_menu() -> void:
	get_tree().paused = false
	quit_requested.emit()

## OpponentAI calls this after every successful try_hit. Smashes are
## NOT counted here anymore (counted in on_shot_executed — only when
## the shuttle really launches).
func notify_shot_hit(_hitter: int, _shot_type: int) -> void:
	# Kept so OpponentAI does not need to change; rally/smash
	# numbers are counted in _process (last_hitter changes) and
	# on_shot_executed.
	pass

func bind_input(input: MobileInputManager) -> void:
	if _input == input:
		return
	unbind_input()
	_input = input
	if _input == null:
		return
	_input.shot_requested.connect(_on_shot_requested)
	_input.serve_requested.connect(_on_serve_requested)
	_input.pause_requested.connect(_on_pause_requested)

func unbind_input() -> void:
	if _input == null:
		return
	_input.shot_requested.disconnect(_on_shot_requested)
	_input.serve_requested.disconnect(_on_serve_requested)
	_input.pause_requested.disconnect(_on_pause_requested)
	_input = null

# --- Update ---------------------------------------------------------

func _process(delta: float) -> void:
	if phase == Phase.SERVING or phase == Phase.RALLY or phase == Phase.POINT_END or phase == Phase.GAME_END:
		stats.match_time += delta

	# The AI serves by itself after 0.8 s when it is its turn.
	if phase == Phase.SERVING and rules != null and rules.score.server == 1:
		_ai_serve_timer -= delta
		if _ai_serve_timer <= 0.0:
			var serve := ShotData.ShotType.SERVE_LOW if randf() < 0.7 else ShotData.ShotType.SERVE_HIGH
			do_serve(serve)

	# Count exchanges: last_hitter changes during Rally.
	if phase == Phase.RALLY and shuttle != null:
		if shuttle.state == Shuttlecock.State.FLYING:
			_rally_stuck_timer = 0.0
			var hitter := shuttle.last_hitter
			if hitter >= 0 and hitter != _last_seen_hitter:
				_last_seen_hitter = hitter
				_current_rally_hits += 1
				stats.rallies += 1
				stats.shots_hit += 1
				# The previous shot was returned by the opponent -> good.
				_resolve_pending_shot_as_good()
				_pending_shot_hitter = hitter
				_pending_is_serve = false
		else:
			# Shuttle not flying but already in Rally too long (e.g. a
			# failed serve never launched): re-serve instead of stalling.
			_rally_stuck_timer += delta
			if _rally_stuck_timer > STUCK_RALLY_TIMEOUT:
				push_warning("[MatchManager] Rally stuck (shuttle not flying) — re-serving.")
				start_point()

	# End-of-point wait.
	if phase == Phase.POINT_END or phase == Phase.GAME_END:
		_point_end_timer -= delta
		if _point_end_timer <= 0.0:
			if _pending_match_won:
				_finish_match(_pending_point_winner)
			else:
				start_point()

# --- Landing / points -------------------------------------------------

func _on_shuttle_landed(landing: Vector3, _in_singles: bool, _in_doubles: bool) -> void:
	if phase != Phase.RALLY and phase != Phase.SERVING:
		return
	if rules == null or shuttle == null:
		return

	var last_hitter := shuttle.last_hitter
	var was_serve := shuttle.last_was_serve
	var result: Dictionary = rules.evaluate_landing(landing, last_hitter, was_serve, shuttle.net_hits)

	if result["is_let"]:
		message.emit("Let — giao lại")
		start_point()
		return

	var winner: int = result["winner"]
	var detail := _fault_text(result["fault"])
	_end_point(winner, detail)

func _end_point(winner: int, detail: String) -> void:
	# Settle the last shot: scoring = good, fault = error.
	if _pending_shot_hitter >= 0:
		var won_by_hitter := winner == _pending_shot_hitter
		if _pending_is_serve:
			if won_by_hitter:
				stats.serves_good += 1
			else:
				stats.errors += 1
		else:
			if won_by_hitter:
				stats.shots_good += 1
			else:
				stats.errors += 1
		_pending_shot_hitter = -1
		_pending_is_serve = false

	stats.longest_rally = maxi(stats.longest_rally, _current_rally_hits)

	var game_no: int = rules.score.current_game
	var awarded: Dictionary = rules.award_point(winner)
	var game_won: bool = awarded["game_won"]
	var match_won: bool = awarded["match_won"]

	_pending_point_winner = winner
	_pending_match_won = match_won

	score_changed.emit(rules.score)

	var who := "Bạn" if winner == 0 else "Đối thủ"
	message.emit(("Điểm cho %s! %s" % [who, detail]).strip_edges())

	if game_won and not match_won:
		message.emit("%s thắng ván %d!" % [who, game_no])
		phase = Phase.GAME_END
		_point_end_timer = GAME_END_DELAY
	else:
		phase = Phase.POINT_END
		_point_end_timer = POINT_END_DELAY

func _finish_match(winner: int) -> void:
	phase = Phase.MATCH_END
	if players.size() >= 2 and players[0] != null and players[1] != null:
		(players[winner] as PlayerController).play_celebrate()
		(players[1 - winner] as PlayerController).play_disappointed()
	message.emit("Bạn thắng trận!" if winner == 0 else "Đối thủ thắng trận!")
	_grant_match_rewards(winner)
	match_end.emit(winner)

## End-of-match economy rewards (runs BEFORE match_end so the HUD
## does not double-count the tournament result). Practice matches
## earn nothing. Tournament matches do not get Quick Match rewards
## (their prize money comes from the tournament result). No active
## career -> no rewards (money lives in the Career).
func _grant_match_rewards(winner: int) -> void:
	if is_practice:
		return
	if game_mode == null:
		return
	game_mode.grant_match_rewards(winner, stats, rules)

# --- Input callbacks -------------------------------------------------

func _on_shot_requested(shot_type: int, dir: Vector2) -> void:
	if phase != Phase.RALLY:
		return
	if players.size() < 2 or players[0] == null or shuttle == null:
		return
	if ShotData.ShotLibrary.is_serve(shot_type):
		return  # serves only use the Serve button

	var target := _compute_shot_target(shot_type, dir)
	(players[0] as PlayerController).try_hit(shot_type, target)
	# try_hit true = a swing was executed (including a whiff); a real
	# exchange is counted when last_hitter changes in _process, smashes
	# are counted in on_shot_executed when the shuttle launches.

func _on_serve_requested() -> void:
	request_user_serve()

func _on_pause_requested() -> void:
	pause(phase != Phase.PAUSED)

## Swipe direction (world XZ, Vector2.y = depth z) -> landing point
## on the opponent's half. Default aim is the open space opposite
## the opponent; the swipe pushes the target along the swipe
## direction, clamped inside the singles court.
func _compute_shot_target(shot_type: int, dir: Vector2) -> Vector3:
	var opp_pos := Vector3.ZERO
	if players.size() >= 2 and players[1] != null:
		opp_pos = (players[1] as PlayerController).global_position

	var x := -opp_pos.x  # open space opposite the opponent
	var z := 5.2  # deep by default
	x += dir.x * 2.2
	z += dir.y * 2.2

	if ShotData.ShotLibrary.is_net_shot(shot_type):
		z = minf(z, 2.6)  # net skills stay near the net
	elif shot_type == ShotData.ShotType.CLEAR or shot_type == ShotData.ShotType.LIFT:
		z = maxf(z, 4.2)  # clear / lift go deep

	var hw := CourtDimensions.SINGLES_HALF_WIDTH - 0.35
	x = clampf(x, -hw, hw)
	z = clampf(z, 0.6, CourtDimensions.COURT_HALF_LENGTH - 0.35)
	return Vector3(x, 0, z)

# --- Helpers ---------------------------------------------------------

func _resolve_pending_shot_as_good() -> void:
	if _pending_shot_hitter < 0:
		return
	if _pending_is_serve:
		stats.serves_good += 1
	else:
		stats.shots_good += 1

func _subscribe_shuttle() -> void:
	if _shuttle_subscribed or shuttle == null:
		return
	shuttle.landed.connect(_on_shuttle_landed)
	_shuttle_subscribed = true

func _unsubscribe_shuttle() -> void:
	if not _shuttle_subscribed or shuttle == null:
		return
	shuttle.landed.disconnect(_on_shuttle_landed)
	_shuttle_subscribed = false

## Count smashes via ShotController.shot_executed — only when the
## shuttle really launched (after the fallback), never a whiff or a
## solver failure like the old try_hit / notify_shot_hit counting.
## Subscribed per player so we know whose smash it is: stats counts
## both sides (display), user_smashes counts only the user (missions).
func _subscribe_shot_executed() -> void:
	if _shots_subscribed or players.size() < 2:
		return
	for p in players:
		if p != null and (p as PlayerController).shot_ctrl != null:
			var idx := (p as PlayerController).player_index
			(p as PlayerController).shot_ctrl.shot_executed.connect(_on_shot_executed_for.bind(idx))
	_shots_subscribed = true

func _on_shot_executed_for(shot_type: int, _quality: int, hitter: int) -> void:
	if shot_type != ShotData.ShotType.SMASH and shot_type != ShotData.ShotType.JUMP_SMASH:
		return
	stats.smashes += 1
	if hitter == 0:
		stats.user_smashes += 1

func _fault_text(fault: int) -> String:
	match fault:
		MatchRules.FaultReason.OUT:
			return "Cầu ra ngoài."
		MatchRules.FaultReason.INTO_NET:
			return "Cầu vướng lưới."
		MatchRules.FaultReason.SHORT_SERVE:
			return "Giao cầu chưa qua vạch ngắn."
		MatchRules.FaultReason.SERVE_OUT:
			return "Giao cầu ra ngoài ô."
		MatchRules.FaultReason.NO_CROSS:
			return "Cầu không qua lưới."
		MatchRules.FaultReason.DOUBLE_HIT:
			return "Đánh cầu hai lần."
		_:
			return ""
