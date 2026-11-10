class_name MatchRules
extends RefCounted
## Pure BWF rules logic — no scene dependencies, fully unit-testable.
## Rally scoring, 21 points, win by 2, cap at 30, best of 3 games.
## Port of ShuttleLab.Match.MatchRules (Unity C#).
##
## evaluate_landing() returns {"winner": int, "fault": int, "is_let": bool}
## (winner 0/1, or -1 for a let). award_point() returns
## {"game_won": bool, "match_won": bool}.

const POINTS_TO_WIN_GAME := 21
const MAX_POINTS := 30
const GAMES_TO_WIN_MATCH := 2
const WIN_BY := 2

enum FaultReason {
	NONE,
	OUT,  # landed outside the singles court
	INTO_NET,  # hit the net and fell back on the hitter's side
	SHORT_SERVE,  # serve did not reach the service box
	SERVE_OUT,  # serve landed outside the service box
	NO_CROSS,  # shuttle never crossed the net
	DOUBLE_HIT,  # (reserved) hit twice
}

static func fault_name(fault: int) -> String:
	var keys := FaultReason.keys()
	if fault >= 0 and fault < keys.size():
		return String(keys[fault])
	return "?"

class ScoreState extends RefCounted:
	var points: Array[int] = [0, 0]  # points in the current game
	var games: Array[int] = [0, 0]  # games won
	var current_game: int = 1
	var server: int = 0  # player index serving
	var receiver: int = 1
	var won_game: Array[bool] = [false, false]

	func clone() -> ScoreState:
		var c := ScoreState.new()
		c.points = points.duplicate()
		c.games = games.duplicate()
		c.current_game = current_game
		c.server = server
		c.receiver = receiver
		c.won_game = won_game.duplicate()
		return c

var score: ScoreState = ScoreState.new()

func reset() -> void:
	score = ScoreState.new()

## Server's score even -> right court, odd -> left court.
func server_on_right_court() -> bool:
	return score.points[score.server] % 2 == 0

## X sign of the service court the server stands on.
func server_court_x_sign() -> int:
	var server_side := -1 if score.server == 0 else 1
	return CourtDimensions.service_court_x_sign(server_side, server_on_right_court())

## X sign of the service box the serve must land in.
func target_box_x_sign() -> int:
	# Diagonal box: opposite X sign from the server's court.
	return -server_court_x_sign()

## Side sign (z) of the receiver.
func receiver_side_sign() -> int:
	return -1 if score.receiver == 0 else 1

## Evaluate a landing. Returns the point winner (0/1), or -1 for a let
## (replay the rally). fault explains the decision.
## NOTE: a landing ALWAYS ends the rally — Shuttlecock sets State = Dead
## the moment the shuttle touches the ground, so nobody can play it
## afterwards. A good serve or a shot landing in-bounds therefore scores
## for the last hitter (serve ace / winner by placement); there is no
## "rally continues" outcome here.
func evaluate_landing(landing: Vector3, last_hitter: int, was_serve: bool,
		net_hits: int) -> Dictionary:
	var fault := FaultReason.NONE
	var is_let := false
	if last_hitter < 0 or last_hitter > 1:
		return {"winner": 0, "fault": fault, "is_let": is_let}
	var opponent := 1 - last_hitter

	var in_singles := CourtDimensions.is_in_singles(landing)
	var hitter_side := -1 if last_hitter == 0 else 1

	# Did the shuttle cross to the opponent's side?
	var crossed := CourtDimensions.sign_of(landing.z) != hitter_side

	if was_serve:
		# Serve must land in the diagonal service box.
		var box_x := target_box_x_sign()
		var recv_side := receiver_side_sign()
		var in_box := CourtDimensions.is_in_service_box(landing, recv_side, box_x, false)

		if net_hits > 0 and in_box:
			# Net-cord serve that lands in: let, replay.
			is_let = true
			return {"winner": -1, "fault": fault, "is_let": is_let}
		if not crossed:
			fault = FaultReason.NO_CROSS
			return {"winner": opponent, "fault": fault, "is_let": is_let}
		if not in_box:
			# Short (did not reach the short service line) vs out.
			var az := absf(landing.z)
			fault = FaultReason.SHORT_SERVE if az < CourtDimensions.SHORT_SERVICE_DISTANCE else FaultReason.SERVE_OUT
			return {"winner": opponent, "fault": fault, "is_let": is_let}
		# Serve good: receiver failed to play it before it landed.
		# Serve ace — point to the server.
		return {"winner": last_hitter, "fault": fault, "is_let": is_let}

	# Rally landing.
	if not crossed:
		# Fell back on the hitter's own side (usually into the net).
		fault = FaultReason.INTO_NET if net_hits > 0 else FaultReason.NO_CROSS
		return {"winner": opponent, "fault": fault, "is_let": is_let}
	if not in_singles:
		fault = FaultReason.OUT
		return {"winner": opponent, "fault": fault, "is_let": is_let}
	# In-bounds: winner by placement — point to the last hitter.
	return {"winner": last_hitter, "fault": fault, "is_let": is_let}

## Award a point to the winner. Returns {"game_won": bool, "match_won": bool}.
func award_point(winner: int) -> Dictionary:
	var game_won := false
	var match_won := false

	score.points[winner] += 1
	var loser := 1 - winner

	# Service goes to the point winner (rally scoring).
	score.server = winner
	score.receiver = loser

	var p := score.points[winner]
	var q := score.points[loser]
	if (p >= POINTS_TO_WIN_GAME and p - q >= WIN_BY) or p == MAX_POINTS:
		score.games[winner] += 1
		score.won_game[winner] = score.games[winner] >= GAMES_TO_WIN_MATCH
		game_won = true
		if score.games[winner] >= GAMES_TO_WIN_MATCH:
			match_won = true
			return {"game_won": game_won, "match_won": match_won}
		# Next game: reset points, server = winner of the previous game.
		score.points[0] = 0
		score.points[1] = 0
		score.current_game += 1
		score.server = winner
		score.receiver = loser
	return {"game_won": game_won, "match_won": match_won}

## Score line for the HUD, e.g. "21-15".
func score_line() -> String:
	return "%d-%d" % [score.points[0], score.points[1]]

func games_line() -> String:
	return "%d-%d" % [score.games[0], score.games[1]]
