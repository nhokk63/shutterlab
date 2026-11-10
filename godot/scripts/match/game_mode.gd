class_name GameMode
extends RefCounted
## Match mode configuration + mode-specific rules.
## Port of the mode flow in ShuttleLab.Core.GameStateManager.AppMode
## + the reward rules in MatchManager.GrantMatchRewards (Unity C#).
##
## MODES (ARCHITECTURE.md):
## - QuickMatch: one match vs the AI at the chosen difficulty.
##   Win +50 coins, loss +10 (only when a Career is active).
## - Practice: the AI returns everything it can reach (PracticeVersion
##   of the difficulty) and makes no intentional errors; no rewards.
## - Career: matches inside a career season; rewards and progression
##   live in CareerManager (stub — full career system is a later port).
## - Tournament: bracket matches; no Quick Match rewards (the prize
##   money comes from the tournament result via TournamentManager).
##
## Career / Tournament are STUBS here: they reuse the Quick Match
## flow with the progression data hooks, exactly as the task allows.
## The full CareerManager / TournamentManager (Systems) port comes
## after the core match is verified in-game.

enum AppMode { MENU, QUICK_MATCH, PRACTICE, CAREER, TOURNAMENT }

const QUICK_MATCH_WIN_REWARD := 50
const QUICK_MATCH_LOSS_REWARD := 10

var mode: int = AppMode.QUICK_MATCH
var difficulty: int = 2
var player_character_id: int = 0
var ai_character_id: int = -1  # -1 = stats from difficulty

## Optional progression contexts (stubs for now).
var career: CareerManager
var tournament: TournamentManager

static func mode_name(m: int) -> String:
	match m:
		AppMode.QUICK_MATCH:
			return "Quick Match"
		AppMode.PRACTICE:
			return "Practice"
		AppMode.CAREER:
			return "Career"
		AppMode.TOURNAMENT:
			return "Tournament"
		_:
			return "Menu"

func is_practice() -> bool:
	return mode == AppMode.PRACTICE

## Is this a tournament match? A bracket is active OR the mode
## the match was loaded in is Tournament (CurrentMode stays put even
## on "Rematch", so a tournament rematch still earns no Quick Match
## reward).
func is_tournament_match() -> bool:
	if tournament != null and tournament.is_active:
		return true
	return mode == AppMode.TOURNAMENT

## End-of-match economy rewards (called by MatchManager BEFORE the
## match_end signal, so the HUD does not double-count the tournament
## result). Practice matches earn nothing. Tournament matches earn
## no Quick Match reward. No active career -> no rewards (money
## lives in the Career).
func grant_match_rewards(winner: int, stats: MatchManager.MatchStats, rules: MatchRules) -> void:
	if mode == AppMode.PRACTICE:
		return
	if career == null or not career.active:
		return
	var won := winner == 0
	if not is_tournament_match():
		career.add_money(QUICK_MATCH_WIN_REWARD if won else QUICK_MATCH_LOSS_REWARD)

	# Games the user lost = games the opponent won; smashes count
	# only the USER's (stats.user_smashes) — AI smashes do not count
	# toward the player's weekly missions.
	var games_lost := rules.score.games[1] if rules != null and rules.score != null else 0
	var smashes := stats.user_smashes if stats != null else 0
	career.record_match(won, smashes, games_lost)
