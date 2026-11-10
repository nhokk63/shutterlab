class_name TournamentManager
extends RefCounted
## Single-elimination brackets (8 / 16 / 32 players), seeded by rank,
## plus the weekly special event. Port of ShuttleLab.Systems.
## TournamentManager (Unity C#).
##  - AI-vs-AI matches: SIMULATED with a seeded RandomNumberGenerator,
##    win formula written out below (higher seed = stronger). This is a
##    simulation, NOT a real match — the UI must say "simulated result"
##    if it shows AI matches.
##  - The user's matches: NOT simulated. The user plays for real; the
##    real result goes in via report_player_match(won).
##
## The bracket lives in memory only (not saved) — leaving the Career
## scene mid-tournament loses the bracket; it is rebuilt in tournament
## week. No 3rd-place match.
##
## NOTE: Godot's RandomNumberGenerator with a given seed does NOT produce
## the same stream as C# System.Random with the same seed — the bracket
## logic and formulas are identical, only the random draws differ.

enum TournamentTier { SUPER100, SUPER300, SUPER500, SUPER750, SUPER1000 }

enum TournamentPlacement { EARLY_EXIT, QUARTERFINALIST, SEMIFINALIST, FINALIST, CHAMPION }

enum TournamentRound { ROUND_OF_32, ROUND_OF_16, QUARTERFINAL, SEMIFINAL, FINAL, CHAMPION, ELIMINATED }

## Static info of one tier (fictional name, points, money).
## Keys: tier, name, win_points, prize_money.
const TIERS: Array = [
	{"tier": TournamentTier.SUPER100, "name": "Cúp Sao Mai", "win_points": 100, "prize_money": 200},
	{"tier": TournamentTier.SUPER300, "name": "Cúp Sông Hồng", "win_points": 300, "prize_money": 500},
	{"tier": TournamentTier.SUPER500, "name": "Cúp Hoa Sen", "win_points": 500, "prize_money": 1000},
	{"tier": TournamentTier.SUPER750, "name": "Cúp Rồng Vàng", "win_points": 750, "prize_money": 2000},
	{"tier": TournamentTier.SUPER1000, "name": "Cúp Vô Địch ShuttleLab", "win_points": 1000, "prize_money": 5000},
]

static func tier_info(tier: int) -> Dictionary:
	return TIERS[clampi(tier, 0, TIERS.size() - 1)]

static func tier_info_for_index(index: int) -> Dictionary:
	return TIERS[clampi(index, 0, TIERS.size() - 1)]

static func tier_count() -> int:
	return TIERS.size()

# Fictional AI names (NOT real players). Enough for a 32 bracket.
const FICTIONAL_NAMES: Array[String] = [
	"Minh Quân", "Hải Đăng", "An Nhiên", "Tuấn Anh", "Phương Linh",
	"Đức Huy", "Thu Hằng", "Quốc Bảo", "Lan Anh", "Văn Thắng",
	"Ngọc Trâm", "Hoàng Nam", "Diệu Linh", "Thanh Sơn", "Kim Ngân",
	"Bảo Long", "Hồng Nhung", "Mạnh Cường", "Thùy Dương", "Đình Phúc",
	"Mai Phương", "Gia Hưng", "Thanh Thảo", "Nhật Minh", "Quỳnh Anh",
	"Thế Vinh", "Bích Ngọc", "Hữu Phước", "Tuyết Mai", "Công Danh",
	"Xuân Trường",
]

# Fictional weekly-event names, rotating by week (no real BWF names).
const WEEKLY_EVENT_NAMES: Array[String] = [
	"Cúp Ánh Sao",
	"Giải Mở Rộng Mùa Thu",
	"Cúp Tốc Độ",
	"Giải Thăng Hạng",
	"Cúp Đêm Hội",
]

var bracket_size: int = 0
var is_active: bool = false
var current_round: int = TournamentRound.ROUND_OF_32
var player: Dictionary = {}
var player_matches_won: int = 0
var player_matches_lost: int = 0

var is_player_eliminated: bool:
	get:
		return current_round == TournamentRound.ELIMINATED

var is_player_champion: bool:
	get:
		return current_round == TournamentRound.CHAMPION

## The user's placement so far (for Career). Follows the furthest round
## the user reached, not the current round (losing in the semifinal is
## still Semifinalist, not EarlyExit).
func player_placement() -> int:
	if is_player_champion:
		return TournamentPlacement.CHAMPION
	match _player_best_round:
		TournamentRound.FINAL:
			return TournamentPlacement.FINALIST
		TournamentRound.SEMIFINAL:
			return TournamentPlacement.SEMIFINALIST
		TournamentRound.QUARTERFINAL:
			return TournamentPlacement.QUARTERFINALIST
		_:
			return TournamentPlacement.EARLY_EXIT

var _current_round_matches: Array = []
var _alive: Array = []
var _player_best_round: int = TournamentRound.ROUND_OF_32
var _rng: RandomNumberGenerator = RandomNumberGenerator.new()

func _init() -> void:
	is_active = false
	current_round = TournamentRound.ROUND_OF_32

## Create a new bracket. size only takes 8/16/32 (anything else snaps to
## the nearest). player_seed 1..size (1 = strongest). A fixed rng_seed
## replays the same AI results with the same seed.
func create_bracket(size: int, player_name: String, player_seed: int, rng_seed: int) -> void:
	if size <= 8:
		bracket_size = 8
	elif size <= 16:
		bracket_size = 16
	else:
		bracket_size = 32

	_rng.seed = rng_seed
	_alive = []
	player_matches_won = 0
	player_matches_lost = 0

	player_seed = clampi(player_seed, 1, bracket_size)
	player = {
		"name": player_name if not player_name.is_empty() else "Bạn",
		"seed": player_seed, "is_player": true, "ranking": player_seed,
	}

	# Seeds: the user keeps their seed, AI fill the other seeds.
	var names: Array = FICTIONAL_NAMES.duplicate()
	_shuffle(names)
	var name_idx := 0
	for seed in range(1, bracket_size + 1):
		if seed == player_seed:
			_alive.append(player)
		else:
			var n: String = names[name_idx % names.size()]
			name_idx += 1
			_alive.append({"name": n, "seed": seed, "is_player": false, "ranking": seed})

	current_round = _first_round_for_size(bracket_size)
	_player_best_round = current_round
	_build_round_matches()
	is_active = true

static func _first_round_for_size(size: int) -> int:
	match size:
		8:
			return TournamentRound.QUARTERFINAL
		16:
			return TournamentRound.ROUND_OF_16
		_:
			return TournamentRound.ROUND_OF_32

func _shuffle(list: Array) -> void:
	for i in range(list.size() - 1, 0, -1):
		var j := _rng.randi_range(0, i)
		var tmp = list[i]
		list[i] = list[j]
		list[j] = tmp

func _build_round_matches() -> void:
	_current_round_matches = []
	# Pair high seed vs low seed (1 vs N, 2 vs N-1, ...) — standard bracket.
	var ordered: Array = _alive.duplicate()
	ordered.sort_custom(func(a: Dictionary, b: Dictionary) -> bool: return int(a["seed"]) < int(b["seed"]))
	var n := ordered.size()
	for i in range(n / 2):
		_current_round_matches.append({"a": ordered[i], "b": ordered[n - 1 - i], "winner": null})

## Current round name for the UI (QF/SF/F...).
func current_round_name() -> String:
	match current_round:
		TournamentRound.ROUND_OF_32:
			return "Vòng 1/16"
		TournamentRound.ROUND_OF_16:
			return "Vòng 1/8"
		TournamentRound.QUARTERFINAL:
			return "Tứ kết"
		TournamentRound.SEMIFINAL:
			return "Bán kết"
		TournamentRound.FINAL:
			return "Chung kết"
		TournamentRound.CHAMPION:
			return "Vô địch"
		_:
			return "Đã bị loại"

## The user's next opponent (empty dict when the user is done).
func next_opponent() -> Dictionary:
	if not is_active or is_player_eliminated or is_player_champion:
		return {}
	for m in _current_round_matches:
		if _is_player_match(m) and m["winner"] == null:
			return m["b"] if bool(m["a"]["is_player"]) else m["a"]
	return {}

## The user's current match (empty dict when none).
func player_match() -> Dictionary:
	if not is_active:
		return {}
	for m in _current_round_matches:
		if _is_player_match(m):
			return m
	return {}

static func _is_player_match(m: Dictionary) -> bool:
	return bool(m["a"]["is_player"]) or bool(m["b"]["is_player"])

## Simulate ALL AI-vs-AI matches of the current round (never touches the
## user's match). Formula: strength = (BracketSize - Seed + 1);
## P(A wins) = strength A / (strength A + strength B). A fixed seed
## replays the same results. After simulating, if the user's match is
## still unplayed the round is NOT closed — report_player_match must be
## called first, then the round advances.
func simulate_round() -> void:
	if not is_active:
		return
	for m in _current_round_matches:
		if m["winner"] != null or _is_player_match(m):
			continue
		var strength_a: float = bracket_size - int(m["a"]["seed"]) + 1
		var strength_b: float = bracket_size - int(m["b"]["seed"]) + 1
		var roll := _rng.randf()
		m["winner"] = m["a"] if roll < strength_a / (strength_a + strength_b) else m["b"]

## Report the result of the user's REAL match (played via MatchManager).
## won=true: user advances (champion when this is the final).
## won=false: user is eliminated. After reporting, if every match of the
## round is done the bracket advances automatically.
## Returns true only when a NEW result is recorded (match unplayed);
## false when the tournament isn't active or the match is already
## recorded — so the HUD only pays money/rank/titles once, and the
## "Rematch" button can't be farmed. When the tournament closes
## (champion or eliminated) is_active=false so later Quick Matches
## aren't mistaken for tournament matches.
func report_player_match(won: bool) -> bool:
	if not is_active:
		return false
	var pm := player_match()
	if pm.is_empty() or pm["winner"] != null:
		return false

	if won:
		pm["winner"] = player
		player_matches_won += 1
		if current_round == TournamentRound.FINAL:
			current_round = TournamentRound.CHAMPION
			is_active = false
			return true
	else:
		pm["winner"] = pm["b"] if bool(pm["a"]["is_player"]) else pm["a"]
		player_matches_lost += 1
		_player_best_round = current_round  # lost in this round
		current_round = TournamentRound.ELIMINATED
		is_active = false
		return true

	_player_best_round = current_round  # won this round -> reached it
	_try_advance_round()
	return true

func _try_advance_round() -> void:
	for m in _current_round_matches:
		if m["winner"] == null:
			return  # a match is still open (usually an AI match)
	# Every match done -> winners go to the next round.
	_alive = []
	for m in _current_round_matches:
		_alive.append(m["winner"])

	match current_round:
		TournamentRound.ROUND_OF_32:
			current_round = TournamentRound.ROUND_OF_16
		TournamentRound.ROUND_OF_16:
			current_round = TournamentRound.QUARTERFINAL
		TournamentRound.QUARTERFINAL:
			current_round = TournamentRound.SEMIFINAL
		TournamentRound.SEMIFINAL:
			current_round = TournamentRound.FINAL
		_:
			return
	_build_round_matches()

## Bracket lines for the UI. AI matches are marked as simulations.
func get_bracket_lines() -> Array:
	var lines: Array = []
	if not is_active:
		return lines
	lines.append("%s — %d tay vợt" % [current_round_name(), bracket_size])
	for m in _current_round_matches:
		var tag := " (trận của bạn)" if _is_player_match(m) else " (mô phỏng AI)"
		var result := " -> thắng: %s" % String(m["winner"]["name"]) if m["winner"] != null else " -> chưa đấu"
		lines.append("%s (hạt giống %d) vs %s (hạt giống %d)%s%s" % [
			String(m["a"]["name"]), int(m["a"]["seed"]),
			String(m["b"]["name"]), int(m["b"]["seed"]), result, tag])
	return lines

# --- Weekly special event (PROGRESSION_SPEC §3, data only) ----------------

## This week's special event: fictional name + tier + week seed.
static func get_weekly_event() -> Dictionary:
	return get_weekly_event_for_seed(WeeklyMissions.current_week_number())

## Same logic as get_weekly_event but takes the seed directly (for tests).
## Seed = ISO week number (same standard as WeeklyMissions):
## name rotates every week, tier rotates every 5 weeks.
static func get_weekly_event_for_seed(seed: int) -> Dictionary:
	var s := maxi(seed, 0)
	return {
		"name": WEEKLY_EVENT_NAMES[s % WEEKLY_EVENT_NAMES.size()],
		"tier": (s / WEEKLY_EVENT_NAMES.size()) % 5,
		"week_seed": s,
	}
