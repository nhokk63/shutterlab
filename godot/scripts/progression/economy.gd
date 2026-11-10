class_name Economy
extends RefCounted
## Coin economy: end-of-match rewards + the Quick Match career bootstrap.
## Port of MatchManager.GrantMatchRewards + GameBootstrap.EnsureCareerSession
## (Unity C#).
##
## Quick Match rewards: win +50 coins, loss +10 coins. Tournament matches
## get NO Quick Match reward (they already pay tournament prize money via
## Career.record_tournament_result). Practice matches get nothing.
## Weekly missions count for every real match (Quick Match + tournament).
## No active career -> no reward at all (money only lives in Career).

const QUICK_MATCH_WIN_REWARD := 50
const QUICK_MATCH_LOSS_REWARD := 10

## End-of-match economy (runs BEFORE the match-end signal, so the HUD
## can't double-count tournament results later). Practice matches don't
## count. Tournament matches don't get the Quick Match reward (they
## already have tournament prize money). Weekly missions count for every
## real match (Quick Match + tournament).
## won: user won the match; is_practice: practice match; is_tournament_match:
## bracket active OR the loaded mode is Tournament; user_smashes: USER's
## smashes in the match (AI smashes don't count toward the player's
## weekly mission); games_lost: games the user lost.
static func grant_match_rewards(career: Career, won: bool, is_practice: bool,
		is_tournament_match: bool, user_smashes: int, games_lost: int) -> void:
	if is_practice:
		return
	if career == null or not career.active:
		return
	if not is_tournament_match:
		career.add_money(QUICK_MATCH_WIN_REWARD if won else QUICK_MATCH_LOSS_REWARD)
	career.missions.record_match(won, user_smashes, games_lost)

## Make sure a Career is active so end-of-match coins have somewhere to
## go (Quick Match doesn't go through the Career screen, so the session
## is created/loaded here). No career in the save -> start a new career
## (500 starting coins). Returns the active career (or null when the
## passed career is null and nothing could be loaded).
static func ensure_career_session(career: Career) -> Career:
	if career == null:
		career = Career.load_or_create()
	if not career.active:
		career.start_new_career()
	return career
