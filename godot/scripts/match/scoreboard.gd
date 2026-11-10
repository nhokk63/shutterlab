class_name Scoreboard
extends RefCounted
## Plain class (not a Node): holds a copy of the ScoreState for the HUD
## to read. MatchManager emits score_changed -> the HUD calls
## Scoreboard.update(state). All strings are Vietnamese, per the
## project's UI convention.
## Port of ShuttleLab.Match.Scoreboard (Unity C#).

var score: MatchRules.ScoreState = MatchRules.ScoreState.new()

func update(p_score: MatchRules.ScoreState) -> void:
	score = p_score.clone() if p_score != null else MatchRules.ScoreState.new()

## Current game score, e.g. "21-15".
func format_score() -> String:
	return "%d-%d" % [score.points[0], score.points[1]]

## Games won, e.g. "1-0".
func format_games() -> String:
	return "%d-%d" % [score.games[0], score.games[1]]

## Who is serving.
func server_indicator() -> String:
	return "Giao cầu: Bạn" if score.server == 0 else "Giao cầu: Đối thủ"

## Both players at >= 20 points (deuce — must win by 2).
func is_deuce() -> bool:
	return score.points[0] >= 20 and score.points[0] == score.points[1]

## Is the player on match point — i.e. winning this point wins
## the game AND that game decides the match?
func is_match_point(player: int) -> bool:
	if player < 0 or player > 1:
		return false
	var other := 1 - player
	var p := score.points[player] + 1
	var q := score.points[other]
	var wins_game := (p >= MatchRules.POINTS_TO_WIN_GAME and p - q >= MatchRules.WIN_BY) or p == MatchRules.MAX_POINTS
	if not wins_game:
		return false
	return score.games[player] + 1 >= MatchRules.GAMES_TO_WIN_MATCH
