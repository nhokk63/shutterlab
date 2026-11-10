class_name Career
extends RefCounted
## Career state + save owner. Port of CareerManager + SaveManager (Unity C#).
##
## Career by week: 1 week = training (3 weeks) + tournament (week 4).
## Fictional tiers Super 100 -> 300 -> 500 -> 750 -> 1000 with FICTIONAL
## tournament names (Cúp Sao Mai, Cúp Sông Hồng, Cúp Hoa Sen, Cúp Rồng
## Vàng, Cúp Vô Địch ShuttleLab) — NO real BWF tournament names
## (All England, Indonesia Open, Malaysia Open...). Ranking points,
## prize money, promotion on winning a tier.
##
## Rank formula (v0.1.0, written out for later balancing):
##   Rank = max(1, 512 - RankingPoints / 2)
##   (win Super 100: +100 points -> rank ~462; win Super 1000:
##   +1000 points -> rank ~12.)
##
## Save: one JSON file with a version, atomic write + backup, mirroring
## SaveManager: write ".tmp" -> copy the old file to ".bak" -> rename
## ".tmp" to the main file. Corrupt read: try ".bak", still broken ->
## defaults (no crash, no lost app). Version field + a Migrate stub
## (v0.1.0 only has v1, so the stub only bumps the version number).
##
## Simplified v0.1.0:
##  - No seasons (52 weeks then reset) — the week just keeps rising.
##  - Training points convert to +stats via TrainingManager.convert_points
##    (10 points = +1, cap +10 total). Training weeks give +2 points/week.
##  - Money is virtual Career money only — it buys nothing outside the
##    equipment shop (EquipmentManager).
##
## The profile fields (player name, difficulty, volumes, camera preset,
## left-hand mode) mirror Unity's SaveData profile — a copy of the
## running settings, written out before saving.
##
## NOTE: Unity kept character unlocks + training-point bonuses in
## PlayerPrefs; the Godot port unifies them into this single save file
## (same behaviour, one file).

const START_RANK := 512
const START_MONEY := 500
const WEEKS_PER_CYCLE := 4  # 3 training weeks + 1 tournament week
const SAVE_FILE := "shuttlelab_save_v1.json"
const SAVE_VERSION := 1

const WEEKLY_TRAINING_POINTS := 2
const WEEKLY_TRAINING_ALLOWANCE := 50  # coins per training week

## Placement factors for tournament results (points / money).
## Champion 1.00/1.00, Finalist 0.60/0.50, Semifinalist 0.35/0.25,
## Quarterfinalist 0.20/0.125, EarlyExit 0.05/0.00.
static func placement_factors(placement: int) -> Dictionary:
	match placement:
		TournamentManager.TournamentPlacement.CHAMPION:
			return {"points": 1.0, "money": 1.0}
		TournamentManager.TournamentPlacement.FINALIST:
			return {"points": 0.6, "money": 0.5}
		TournamentManager.TournamentPlacement.SEMIFINALIST:
			return {"points": 0.35, "money": 0.25}
		TournamentManager.TournamentPlacement.QUARTERFINALIST:
			return {"points": 0.2, "money": 0.125}
		_:
			return {"points": 0.05, "money": 0.0}

## Unity Mathf.RoundToInt uses banker's rounding (round half to even):
## 62.5 -> 62, 37.5 -> 38, 12.5 -> 12. Godot's round() rounds half away
## from zero, so prize/point math would be off by 1 coin in the .5 cases
## (e.g. Super 300 quarterfinal money 500 * 0.125 = 62.5).
static func round_to_int_bankers(x: float) -> int:
	var f := floorf(x)
	var frac := x - f
	if frac < 0.5:
		return int(f)
	if frac > 0.5:
		return int(f) + 1
	var fi := int(f)
	return fi if fi % 2 == 0 else fi + 1

## The currently loaded career (set by load_or_create).
static var current: Career = null

## Full save v1 dict (profile + career + missions + equipment + training
## + roster sections). Sub-managers sync their live state into it on save.
var data: Dictionary = {}

var active: bool = false
var week: int = 0
var ranking_points: int = 0
var money: int = 0
var wins: int = 0
var losses: int = 0
var titles: int = 0
var current_tier_index: int = 0
var training_points: int = 0
var tournaments_played: int = 0

# Sub-managers (untyped vars holding EquipmentManager / WeeklyMissions /
# TrainingManager / CharacterRoster — kept untyped to avoid a parse-time
# class cycle, since those classes call back into Career).
var equipment = null
var missions = null
var training = null
var roster = null

func _init() -> void:
	data = _default_data()
	equipment = EquipmentManager.new(self)
	missions = WeeklyMissions.new(self)
	training = TrainingManager.new(self)
	roster = CharacterRoster.new(self)

## Load the save file if it exists, else fresh defaults. Sets Career.current.
static func load_or_create() -> Career:
	var c := Career.new()
	c.load()
	current = c
	return c

## Current rank by the formula in the file header.
func ranking() -> int:
	return maxi(1, START_RANK - ranking_points / 2)

func current_tier() -> int:
	return current_tier_index

func current_tier_info() -> Dictionary:
	return TournamentManager.tier_info_for_index(current_tier_index)

## Is this week a tournament week (week divisible by 4)?
func is_tournament_week() -> bool:
	return active and week > 0 and week % WEEKS_PER_CYCLE == 0

## The next tournament: the tournament of the nearest tournament week ahead.
func next_tournament_info() -> Dictionary:
	return current_tier_info()

## Weeks until the tournament week (0 = this is a tournament week).
func weeks_until_tournament() -> int:
	if not active:
		return WEEKS_PER_CYCLE
	return (WEEKS_PER_CYCLE - (week % WEEKS_PER_CYCLE)) % WEEKS_PER_CYCLE

## Start a new career (clears the old progress in memory).
func start_new_career() -> void:
	active = true
	week = 1
	ranking_points = 0
	money = START_MONEY
	wins = 0
	losses = 0
	titles = 0
	current_tier_index = 0
	training_points = 0
	tournaments_played = 0
	equipment.reset_to_starters()
	training.clear_all_bonuses()
	save()

## Advance to the next week. Training week: +2 training points, +50 coin
## allowance. Tournament week: only marked (tournament results go in via
## record_tournament_result). Returns true when the new week is a
## tournament week.
func advance_week() -> bool:
	if not active:
		return false
	week += 1
	if is_tournament_week():
		tournaments_played += 1
	else:
		training_points += WEEKLY_TRAINING_POINTS
		money += WEEKLY_TRAINING_ALLOWANCE
	save()
	return is_tournament_week()

## Record a finished tournament. matches_won/lost counted by the
## TournamentManager. Champion -> promote one tier (max Super 1000)
## and +1 title. Points/money by placement.
func record_tournament_result(placement: int, tier: int, matches_won: int, matches_lost: int) -> void:
	if not active:
		return
	var info := TournamentManager.tier_info(tier)
	wins += maxi(0, matches_won)
	losses += maxi(0, matches_lost)
	var factors := placement_factors(placement)
	ranking_points += round_to_int_bankers(float(info["win_points"]) * float(factors["points"]))
	money += round_to_int_bankers(float(info["prize_money"]) * float(factors["money"]))
	if placement == TournamentManager.TournamentPlacement.CHAMPION:
		titles += 1
		if current_tier_index < TournamentManager.tier_count() - 1:
			current_tier_index += 1
	save()

## Spend virtual coins (buying equipment). False when not enough coins.
func spend_money(amount: int) -> bool:
	if amount < 0:
		amount = 0
	if money < amount:
		return false
	money -= amount
	save()
	return true

func add_money(amount: int) -> void:
	if amount <= 0:
		return
	money += amount
	save()

## Add training points (called when a scored drill is completed).
func add_training_points(points: int) -> void:
	if points <= 0:
		return
	training_points += points
	save()

## Spend training points (trading points for +stats via TrainingManager).
## False when not enough points.
func spend_training_points(points: int) -> bool:
	if points <= 0:
		points = 0
	if training_points < points:
		return false
	training_points -= points
	save()
	return true

func summary_line() -> String:
	if active:
		return "Tuần %d • Hạng %d • %d điểm • %d xu • %d danh hiệu" % [
			week, ranking(), ranking_points, money, titles]
	return "Chưa bắt đầu sự nghiệp"

## Build the user's match stats (port of the GameBootstrap stat chain):
## roster character -> training-point bonuses -> clamp -> equipment.
## The selected character's name is replaced by the saved player name
## when it differs from the default "Bạn".
func build_user_stats() -> PlayerStats:
	var stats := PlayerStats.from_character(roster.selected_id())
	var saved_name := String(data.get("player_name", "Bạn"))
	if not saved_name.is_empty() and saved_name != "Bạn":
		stats.player_name = saved_name
	var bonuses: Dictionary = training.stat_bonus_dict()
	stats.power += int(bonuses.get("power", 0))
	stats.accuracy += int(bonuses.get("accuracy", 0))
	stats.speed += int(bonuses.get("speed", 0))
	stats.stamina += int(bonuses.get("stamina", 0))
	stats.defense += int(bonuses.get("defense", 0))
	stats.clamp_all()
	stats = equipment.apply_to(stats)
	return stats

# --- Save / load ----------------------------------------------------------

static func save_path() -> String:
	return "user://" + SAVE_FILE

static func backup_path() -> String:
	return "user://" + SAVE_FILE + ".bak"

static func _default_data() -> Dictionary:
	return {
		"version": SAVE_VERSION,
		"player_name": "Bạn",
		"difficulty": 2,
		"music_volume": 0.8,
		"sfx_volume": 0.8,
		"camera_preset": 0,
		"left_hand_mode": false,
		"career": {
			"active": false, "week": 0, "ranking_points": 0, "money": 0,
			"wins": 0, "losses": 0, "titles": 0, "current_tier_index": 0,
			"training_points": 0, "tournaments_played": 0,
		},
		"missions": {
			"week_key": "", "wins": 0, "smashes": 0, "clean_win_done": false,
			"wins_rewarded": false, "smashes_rewarded": false, "clean_win_rewarded": false,
		},
		"owned_equipment_ids": [],
		"equipped": [],
		"training_bests": [],
		"training_bonuses": {},
		"unlocked_character_ids": [0],
		"selected_character_id": 0,
	}

## Read the save file. Broken/corrupt -> try backup -> defaults.
## Returns true when a file (main or backup) was loaded.
func load() -> bool:
	var loaded: Variant = _try_load_file(save_path())
	if loaded == null:
		loaded = _try_load_file(backup_path())
	if loaded == null:
		data = _default_data()
		_load_from_data(data)
		return false
	data = loaded
	_ensure_sections()
	if int(data.get("version", 0)) < SAVE_VERSION:
		data["version"] = SAVE_VERSION  # migrate stub: v0.1.0 only has v1
	_load_from_data(data)
	return true

func _try_load_file(path: String) -> Variant:
	if not FileAccess.file_exists(path):
		return null
	var text := FileAccess.get_file_as_string(path)
	if text.is_empty():
		return null
	var parser := JSON.new()
	if parser.parse(text) != OK:
		return null
	var parsed = parser.data
	if not (parsed is Dictionary):
		return null
	return parsed

## Fill any missing section from defaults (protects against JSON that is
## missing fields — e.g. a hand-edited or older save).
func _ensure_sections() -> void:
	var defaults := _default_data()
	for key in defaults:
		if not data.has(key):
			data[key] = defaults[key]
	var career_defaults: Dictionary = defaults["career"]
	var career_data: Dictionary = data.get("career", {})
	for key in career_defaults:
		if not career_data.has(key):
			career_data[key] = career_defaults[key]
	data["career"] = career_data
	var mission_defaults: Dictionary = defaults["missions"]
	var mission_data: Dictionary = data.get("missions", {})
	for key in mission_defaults:
		if not mission_data.has(key):
			mission_data[key] = mission_defaults[key]
	data["missions"] = mission_data

## Atomic write: temp file -> backup the old file -> rename temp to main.
## False when the write failed (logged, no throw).
func save() -> bool:
	_write_to_data(data)
	data["version"] = SAVE_VERSION
	var tmp_path := save_path() + ".tmp"
	var f := FileAccess.open(tmp_path, FileAccess.WRITE)
	if f == null:
		push_warning("[Career] Cannot open save temp file: " + tmp_path)
		return false
	f.store_string(JSON.stringify(data, "\t"))
	f.close()
	if FileAccess.file_exists(save_path()):
		var old_text := FileAccess.get_file_as_string(save_path())
		var b := FileAccess.open(backup_path(), FileAccess.WRITE)
		if b != null:
			b.store_string(old_text)
			b.close()
		DirAccess.remove_absolute(save_path())
	DirAccess.rename_absolute(tmp_path, save_path())
	return true

## Delete the current save, back to defaults, and write them out.
func reset_to_defaults() -> void:
	data = _default_data()
	_load_from_data(data)
	save()

# --- Sync live state <-> save dict ----------------------------------------

func _load_from_data(d: Dictionary) -> void:
	var c: Dictionary = d.get("career", {})
	active = bool(c.get("active", false))
	week = int(c.get("week", 0))
	ranking_points = int(c.get("ranking_points", 0))
	money = int(c.get("money", 0))
	wins = int(c.get("wins", 0))
	losses = int(c.get("losses", 0))
	titles = int(c.get("titles", 0))
	current_tier_index = clampi(int(c.get("current_tier_index", 0)), 0, TournamentManager.tier_count() - 1)
	training_points = int(c.get("training_points", 0))
	tournaments_played = int(c.get("tournaments_played", 0))
	equipment.load_from_data(d)
	missions.load_from_data(d)
	training.load_from_data(d)
	roster.load_from_data(d)

func _write_to_data(d: Dictionary) -> void:
	var c: Dictionary = d.get("career", {})
	c["active"] = active
	c["week"] = week
	c["ranking_points"] = ranking_points
	c["money"] = money
	c["wins"] = wins
	c["losses"] = losses
	c["titles"] = titles
	c["current_tier_index"] = current_tier_index
	c["training_points"] = training_points
	c["tournaments_played"] = tournaments_played
	d["career"] = c
	equipment.write_to_data(d)
	missions.write_to_data(d)
	training.write_to_data(d)
	roster.write_to_data(d)
