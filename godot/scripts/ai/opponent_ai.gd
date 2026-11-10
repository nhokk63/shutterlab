class_name OpponentAI
extends Node
## Drives the AI player (index 1) through PlayerController only.
##
## WHAT THIS DOES:
## - Rally: predicts the landing with AIPerception, waits out its
##   ReactionDelay after the opponent's hit, runs to the predicted spot
##   with PlayerController.move_towards, and hits with
##   AIShotSelection + PlayerController.try_hit. After every successful
##   try_hit it calls MatchManager.notify_shot_hit(1, type) so the AI's
##   smashes are counted (PlayerController has no event carrying ShotType).
## - Practice mode (MatchManager.is_practice): shot selection uses the
##   PracticeVersion of the difficulty (half the error, less aggression)
##   so the AI returns everything it can reach and makes no intentional
##   errors; the rest of the flow is unchanged.
##
## WHAT THIS DELIBERATELY DOES NOT DO:
## - SERVE: MatchManager already serves for the AI (0.8 s timer,
##   ServeLow 70% / ServeHigh 30% via do_serve, with the correct service
##   box from MatchRules). If OpponentAI also called serve, the serve
##   would be attempted twice. So in the Serving phase this component
##   only waits — the serve belongs to MatchManager.
## - Score/rules/phase: never touches rules, score, award_point or phase.
## - The user: never moves player 0, never reads input.
## - Reaching over the net: a hit is only attempted when the shuttle is
##   on the AI's side (z > -0.2 m); a predicted landing on the user's
##   side means the AI walks back home instead of chasing it.
##
## Reaction delay is real: the AI stands still from the moment the
## opponent's hit is observed until ReactionDelay has elapsed. A slow
## Beginner (0.55 s) therefore genuinely cannot reach a fast smash.
## Port of ShuttleLab.AI.OpponentAI (Unity C#).

const AI_INDEX := 1

var _ai: PlayerController
var _match: MatchManager
var _diff: AIDifficulty.DifficultyConfig

var _perception: AIPerception
var _selection: AIShotSelection
var _rng: RandomNumberGenerator

# Reaction / rally-exchange tracking.
var _last_observed_hitter: int = -1
var _react_ready_at: float = 0.0
var _incoming: bool = false

var _last_phase: int = -1
var _time: float = 0.0

func difficulty_level() -> int:
	return _diff.level

func is_ready() -> bool:
	return _ai != null and _match != null

func init(ai_player: PlayerController, match: MatchManager, difficulty: int) -> void:
	_ai = ai_player
	_match = match
	_diff = AIDifficulty.for_level(difficulty)

	_perception = AIPerception.new()
	_perception.init(match.shuttle if match != null else null)
	_perception.prediction_error = _diff.prediction_error

	_selection = AIShotSelection.new()
	_selection.init(match.shuttle if match != null else null)

	# Seeded per difficulty so AI behaviour is reproducible.
	_rng = RandomNumberGenerator.new()
	_rng.seed = 1000 + _diff.level * 77

	_last_observed_hitter = -1
	_react_ready_at = 0.0
	_incoming = false
	_time = 0.0

func _process(delta: float) -> void:
	_time += delta
	if _ai == null or _match == null:
		return

	var phase: int = _match.phase

	# New point: forget the previous exchange.
	if phase != _last_phase:
		_last_phase = phase
		if phase == MatchManager.Phase.SERVING:
			_last_observed_hitter = -1
			_incoming = false
			_react_ready_at = 0.0

	# Serve: owned by MatchManager (see header). Rally: our job.
	if phase != MatchManager.Phase.RALLY:
		return

	var shuttle: Shuttlecock = _match.shuttle
	if shuttle == null:
		return
	if shuttle.state != Shuttlecock.State.FLYING:
		return

	# Our own shot is flying away — nothing to do until the
	# opponent answers it. Record the change so the opponent's
	# next hit starts a FRESH reaction delay (otherwise the
	# reaction timer from the previous exchange would be reused).
	if shuttle.last_hitter == AI_INDEX:
		_last_observed_hitter = AI_INDEX
		_incoming = false
		return

	# Opponent just hit: start the reaction clock, split step.
	if shuttle.last_hitter == 0 and _last_observed_hitter != 0:
		_last_observed_hitter = 0
		_incoming = true
		_react_ready_at = _time + _diff.reaction_delay
		_ai.notify_opponent_hit()

	if not _incoming:
		return
	if _time < _react_ready_at:
		return  # still reacting: stand still

	# Predict, then move.
	var shuttle_pos := shuttle.global_position
	var target: Vector3
	var prediction := _perception.try_predict()
	if prediction["ok"]:
		_perception.observer_position = _ai.global_position
		var landing: Vector3 = prediction["landing"]
		if landing.z < 0.0:
			# Landing on the opponent's side (fault / net cord):
			# do not chase across the net, recover home instead.
			_ai.move_towards(_ai.home_position, _diff.move_speed_factor)
			return
		target = landing
	else:
		# No prediction (rare): chase the shuttle's current spot,
		# clamped to our own half.
		target = shuttle_pos
		target.z = maxf(target.z, 0.05)
		target.y = 0.0

	_ai.move_towards(target, _diff.move_speed_factor)

	# Hit when the shuttle is within reach, on our side of the net,
	# and we are not already mid-swing.
	if _ai.is_swinging:
		return
	if shuttle_pos.z <= -0.2:
		return  # never reach over the net
	if not _ai.can_hit_shuttle(shuttle_pos)["ok"]:
		return

	var sel_diff: AIDifficulty.DifficultyConfig = _diff.practice_version() if _match.is_practice else _diff

	var choice: Dictionary = _selection.choose(
		shuttle.state,
		_ai.global_position,
		_opponent_position(),
		_ai.stats, _ai.stamina, sel_diff, _rng)

	# try_hit true = a swing was executed (including a whiff).
	# The shuttle itself is launched later, from the animation's
	# on_contact callback inside PlayerController.
	# Only swing when close enough: at the edge of reach (0.99–1.15 m)
	# try_hit almost always whiffs (evaluate_quality -> Miss) and the
	# swing lock (~0.5–0.75 s) wastes the chance to return the serve.
	if _ai.global_position.distance_to(shuttle_pos) > 0.95:
		return
	if _ai.try_hit(choice["type"], choice["target"]):
		_match.notify_shot_hit(AI_INDEX, choice["type"])

func _opponent_position() -> Vector3:
	if _match.players != null and _match.players.size() > 0 and _match.players[0] != null:
		return (_match.players[0] as PlayerController).global_position
	return Vector3.ZERO
