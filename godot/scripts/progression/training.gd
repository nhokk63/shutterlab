class_name TrainingManager
extends RefCounted
## 18 drills with MEASURABLE goals: times on target / shots into a zone /
## completion time. Each drill: name, group, description, goal, scoring
## unit. Bests are saved through the Career save file.
##
## start_drill() only returns a DrillSession = the SCORING config (which
## drill, how many targets, time limit, current best). It does NOT run a
## match or spawn an opponent — playing a drill (Practice with drill
## integration) is a later step, using DrillSession as input.
##
## Convert points to stats (PROGRESSION_SPEC §4): 10 training points =
## +1 chosen stat, cap +10 total across all stats. The bonus is applied
## to the base stats at match start (Career.build_user_stats), BEFORE
## equipment.
##
## Port of ShuttleLab.Systems.TrainingManager (Unity C#). Unity stored
## stat bonuses in PlayerPrefs ("shuttlelab_tp_bonus_*"); the Godot port
## keeps them in the single save file instead (same behaviour, one file).

enum DrillUnit { REPS, SECONDS, ACCURACY }

enum TrainableStat { POWER, SPEED, STAMINA, ACCURACY, DEFENSE }

const POINTS_PER_BONUS := 10  # 10 training points = +1 stat
const MAX_TOTAL_BONUS := 10  # max +10 stats total

const CATEGORIES: Array[String] = [
	"Footwork", "Clear", "Drop", "Smash",
	"Net", "Serve", "Defence", "Stamina",
]

static func stat_key(stat: int) -> String:
	match stat:
		TrainableStat.POWER:
			return "power"
		TrainableStat.SPEED:
			return "speed"
		TrainableStat.STAMINA:
			return "stamina"
		TrainableStat.ACCURACY:
			return "accuracy"
		TrainableStat.DEFENSE:
			return "defense"
		_:
			return "power"

## Drill definitions. Values are EXACT from TrainingManager.cs.
## Keys: id, name, category, description, target_text, target_count,
## time_limit_seconds, unit.
static func build_drills() -> Array:
	var d: Array = []
	# Footwork (2)
	d.append({"id": "footwork_corners", "name": "Chạy 4 góc cơ bản", "category": "Footwork",
		"description": "Di chuyển chạm đủ 4 góc sân rồi về vị trí giữa, lặp lại.",
		"target_text": "Chạm đủ 4 góc 5 lượt trong 60 giây", "target_count": 5, "time_limit_seconds": 60.0, "unit": DrillUnit.SECONDS})
	d.append({"id": "footwork_split", "name": "Split-step phản xạ", "category": "Footwork",
		"description": "Bật split-step ngay khi đối thủ chạm cầu, rồi lao đúng hướng.",
		"target_text": "Phản ứng đúng hướng 10 lần liên tiếp", "target_count": 10, "time_limit_seconds": 0.0, "unit": DrillUnit.REPS})
	# Clear (2)
	d.append({"id": "clear_deep", "name": "Clear sâu cuối sân", "category": "Clear",
		"description": "Đánh clear cao, sâu vào vùng cuối sân đối thủ.",
		"target_text": "10 cú clear vào vùng đích cuối sân", "target_count": 10, "time_limit_seconds": 0.0, "unit": DrillUnit.REPS})
	d.append({"id": "clear_cross", "name": "Clear chéo sân", "category": "Clear",
		"description": "Clear chéo từ góc này sang góc đối diện cuối sân.",
		"target_text": "8 cú clear chéo vào vùng đích", "target_count": 8, "time_limit_seconds": 0.0, "unit": DrillUnit.REPS})
	# Drop (2)
	d.append({"id": "drop_tight", "name": "Drop sát lưới", "category": "Drop",
		"description": "Bỏ nhỏ rơi sát lưới, cầu vừa qua lưới là rơi.",
		"target_text": "10 cú drop rơi trong 0,5m sau lưới", "target_count": 10, "time_limit_seconds": 0.0, "unit": DrillUnit.REPS})
	d.append({"id": "drop_slice", "name": "Drop cắt chéo", "category": "Drop",
		"description": "Cắt cầu chéo sang góc đối diện gần lưới.",
		"target_text": "8 cú drop chéo vào vùng đích", "target_count": 8, "time_limit_seconds": 0.0, "unit": DrillUnit.REPS})
	# Smash (3)
	d.append({"id": "smash_straight", "name": "Smash dọc biên", "category": "Smash",
		"description": "Đập dọc biên, cầu cắm xuống gần đường biên.",
		"target_text": "10 cú smash dọc biên vào vùng đích", "target_count": 10, "time_limit_seconds": 0.0, "unit": DrillUnit.REPS})
	d.append({"id": "smash_cross", "name": "Smash chéo sân", "category": "Smash",
		"description": "Đập chéo sang góc đối diện.",
		"target_text": "8 cú smash chéo vào vùng đích", "target_count": 8, "time_limit_seconds": 0.0, "unit": DrillUnit.REPS})
	d.append({"id": "smash_jump", "name": "Nhảy smash", "category": "Smash",
		"description": "Bật nhảy đập cầu ở điểm cao nhất.",
		"target_text": "10 cú nhảy smash trúng đích", "target_count": 10, "time_limit_seconds": 0.0, "unit": DrillUnit.REPS})
	# Net (3)
	d.append({"id": "net_tumble", "name": "Bỏ nhỏ xoay lưới", "category": "Net",
		"description": "Bỏ nhỏ để cầu xoay sát lưới rồi rơi.",
		"target_text": "10 cú bỏ nhỏ xoay lưới thành công", "target_count": 10, "time_limit_seconds": 0.0, "unit": DrillUnit.REPS})
	d.append({"id": "net_kill", "name": "Chặn lưới ghi điểm", "category": "Net",
		"description": "Chặn cầu cao gần lưới, đập xuống sân đối thủ.",
		"target_text": "10 cú chặn lưới ghi điểm", "target_count": 10, "time_limit_seconds": 0.0, "unit": DrillUnit.REPS})
	d.append({"id": "net_lift", "name": "Hất cầu cứu lưới", "category": "Net",
		"description": "Hất cầu từ sát lưới lên cao sâu cuối sân.",
		"target_text": "10 cú hất cầu vào vùng đích", "target_count": 10, "time_limit_seconds": 0.0, "unit": DrillUnit.REPS})
	# Serve (3)
	d.append({"id": "serve_low", "name": "Giao cầu thấp", "category": "Serve",
		"description": "Giao thấp, sát lưới, vào đúng ô giao chéo.",
		"target_text": "10 cú giao thấp vào ô đích", "target_count": 10, "time_limit_seconds": 0.0, "unit": DrillUnit.REPS})
	d.append({"id": "serve_high", "name": "Giao cầu cao sâu", "category": "Serve",
		"description": "Giao cao, sâu về cuối sân đối thủ.",
		"target_text": "10 cú giao cao vào vùng đích", "target_count": 10, "time_limit_seconds": 0.0, "unit": DrillUnit.REPS})
	d.append({"id": "serve_flick", "name": "Giao cầu búng", "category": "Serve",
		"description": "Búng nhanh về cuối sân khi đối thủ đứng sát lưới.",
		"target_text": "8 cú giao búng vào vùng đích", "target_count": 8, "time_limit_seconds": 0.0, "unit": DrillUnit.REPS})
	# Defence (2)
	d.append({"id": "defence_block", "name": "Chặn smash", "category": "Defence",
		"description": "Chặn smash của đối thủ, đưa cầu qua lưới an toàn.",
		"target_text": "Chặn thành công 10 cú smash liên tiếp", "target_count": 10, "time_limit_seconds": 0.0, "unit": DrillUnit.REPS})
	d.append({"id": "defence_drive", "name": "Phản đòn drive", "category": "Defence",
		"description": "Đỡ drive rồi phản drive nhanh về cuối sân.",
		"target_text": "10 cú phản drive vào vùng đích", "target_count": 10, "time_limit_seconds": 0.0, "unit": DrillUnit.REPS})
	# Stamina (1)
	d.append({"id": "stamina_rally", "name": "Rally bền 30 cú", "category": "Stamina",
		"description": "Giữ rally liên tục 30 cú không lỗi, không nghỉ.",
		"target_text": "Hoàn thành rally 30 cú không lỗi", "target_count": 30, "time_limit_seconds": 0.0, "unit": DrillUnit.REPS})
	return d

# Career reference (duck-typed: Career — set by Career, used for training
# points and saving; kept untyped to avoid a parse-time class cycle).
var career = null

var _drills: Array = []
var _bonuses: Dictionary = {}  # stat key -> int
var _bests: Dictionary = {}  # drill id -> float

func _init(career_ref = null) -> void:
	career = career_ref
	_drills = build_drills()

func drill_count() -> int:
	return _drills.size()  # 18

func list_drills() -> Array:
	return _drills.duplicate()

func list_by_category(category: String) -> Array:
	var list: Array = []
	for d in _drills:
		if String(d["category"]) == category:
			list.append(d)
	return list

func get_drill(drill_id: String) -> Dictionary:
	if drill_id.is_empty():
		return {}
	for d in _drills:
		if String(d["id"]) == drill_id:
			return d
	return {}

## Start a drill: returns the scoring config. Empty dict when the id is wrong.
## (Does not run gameplay — Practice with drill integration uses this.)
func start_drill(drill_id: String) -> Dictionary:
	var drill := get_drill(drill_id)
	if drill.is_empty():
		return {}
	var best := get_training_best(drill_id)
	var scoring_text := ""
	match int(drill["unit"]):
		DrillUnit.ACCURACY:
			scoring_text = "Đạt %d%% trúng đích. Kỷ lục: %d%%." % [int(drill["target_count"]), int(best)]
		DrillUnit.SECONDS:
			if best > 0.0:
				scoring_text = "Hoàn thành trong %ds. Kỷ lục: %.1fs." % [int(drill["target_count"]), best]
			else:
				scoring_text = "Hoàn thành trong %ds. Kỷ lục: chưa có." % int(drill["target_count"])
		_:
			scoring_text = "Làm đúng %d lần. Kỷ lục: %d lần." % [int(drill["target_count"]), int(best)]
	return {
		"drill": drill,
		"target_count": int(drill["target_count"]),
		"time_limit_seconds": float(drill["time_limit_seconds"]),
		"best_score": best,
		"scoring_text": scoring_text,
	}

## Record a drill score. True when it is a new best. Seconds drills:
## a SMALLER time is the best (reversed comparison — not the
## higher-is-better rule of the other drills).
func record_best(drill_id: String, score: float) -> bool:
	var drill := get_drill(drill_id)
	if drill.is_empty():
		return false
	if int(drill["unit"]) == DrillUnit.SECONDS:
		var best := get_training_best(drill_id)
		if score <= 0.0:
			return false
		if best <= 0.0 or score < best:
			_bests[drill_id] = score
			if career != null:
				career.save()
			return true
		return false
	if score > get_training_best(drill_id):
		_bests[drill_id] = score
		if career != null:
			career.save()
		return true
	return false

func get_training_best(drill_id: String) -> float:
	if drill_id.is_empty():
		return 0.0
	return float(_bests.get(drill_id, 0.0))

# --- Convert training points to stats --------------------------------------

## Trade 10 training points for +1 chosen stat (max +10 total across all
## stats). False when not enough points, at the cap, or career not active.
## No UI — the UI calls this method later.
func convert_points(stat: int) -> bool:
	if career == null or not career.active:
		return false
	if total_bonus() >= MAX_TOTAL_BONUS:
		return false
	if not career.spend_training_points(POINTS_PER_BONUS):
		return false
	var key := stat_key(stat)
	_bonuses[key] = int(_bonuses.get(key, 0)) + 1
	career.save()
	return true

## Bonus already bought for one stat (0 when none).
func get_stat_bonus(stat: int) -> int:
	return int(_bonuses.get(stat_key(stat), 0))

## Total bonus bought across all stats (cap MAX_TOTAL_BONUS).
func total_bonus() -> int:
	var total := 0
	for stat in TrainableStat.values():
		total += get_stat_bonus(int(stat))
	return total

## All bonuses as a stat-key -> int dict (used when building match stats).
func stat_bonus_dict() -> Dictionary:
	var out: Dictionary = {}
	for stat in TrainableStat.values():
		out[stat_key(int(stat))] = get_stat_bonus(int(stat))
	return out

## Clear all bought stat bonuses — used when starting a new career, so
## old bonuses don't carry into the new career.
func clear_all_bonuses() -> void:
	_bonuses.clear()
	if career != null:
		career.save()

# --- Save / load ----------------------------------------------------------

func load_from_data(data: Dictionary) -> void:
	_bonuses.clear()
	var bonuses: Dictionary = data.get("training_bonuses", {})
	for key in bonuses:
		_bonuses[String(key)] = int(bonuses[key])
	_bests.clear()
	var bests_list: Array = data.get("training_bests", [])
	for entry in bests_list:
		if entry is Dictionary and String(entry.get("drill_id", "")) != "":
			_bests[String(entry["drill_id"])] = float(entry.get("best_score", 0.0))

func write_to_data(data: Dictionary) -> void:
	data["training_bonuses"] = _bonuses.duplicate()
	var bests_list: Array = []
	for drill_id in _bests:
		bests_list.append({"drill_id": String(drill_id), "best_score": float(_bests[drill_id])})
	data["training_bests"] = bests_list
