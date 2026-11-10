class_name CareerManager
extends RefCounted
## STUB — career season data (money, match history).
## The full CareerManager (weekly loop: train / rest / tournament,
## stats growth, equipment, save) lives in Unity's Systems layer and
## is a later port. This stub carries the data MatchManager and
## GameMode already use (active flag, money, record_match) so the
## match economy works end to end without the full system.
## Port of the API surface used by ShuttleLab.Match.MatchManager
## (Unity C#); internals are placeholders.

var active: bool = false
var money: int = 500  # starting coins (Unity: 500 starting coins)
var matches_played: int = 0
var matches_won: int = 0
var total_user_smashes: int = 0

func add_money(amount: int) -> void:
	money = maxi(0, money + amount)

## Record a finished match (called from GameMode.grant_match_rewards).
## Weekly missions are recorded here in the full system; the stub
## only accumulates the counters.
func record_match(won: bool, user_smashes: int, _games_lost: int) -> void:
	matches_played += 1
	if won:
		matches_won += 1
	total_user_smashes += user_smashes
