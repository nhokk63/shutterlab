class_name CharacterRoster
extends RefCounted
## Roster of 6 fictional unlockable characters (PROGRESSION_SPEC.md §2):
## each has a stat spread + shirt/shorts/shoe/skin/hair colours. Unlock
## with coins (Career) or by rank / tournament titles. NO real people's
## names / real brands.
##
## Storage: in the Career save file (unlocked_character_ids +
## selected_character_id). Unity kept these in PlayerPrefs; the Godot port
## unifies them into the single save file (same behaviour, one file).
##
## Unlock conditions (exactly as in CharacterRoster.cs):
##  - Minh Quân: free (id 0, always unlocked).
##  - Bảo Long: 1000 coins.  - Thanh Thảo: rank <= 256.
##  - Đức Anh: 2500 coins (left-handed).  - Hải Yến: 1 title (any tournament).
##  - Quốc Huy: 4000 coins.

## Roster entries. Values are EXACT from CharacterRoster.cs.
## Keys: id, name, country, left_handed, power, accuracy, speed,
## stamina, defense, shirt_color, shorts_color, shoe_color, skin_color,
## hair_color, unlock_cost, unlock_rank, unlock_titles, unlock_text.
static func build_entries() -> Array:
	var e: Array = []
	e.append({"id": 0, "name": "Minh Quân", "country": "VIE", "left_handed": false,
		"power": 60, "accuracy": 62, "speed": 60, "stamina": 60, "defense": 58,
		"shirt_color": Color(0.75, 0.15, 0.15), "shorts_color": Color(0.08, 0.08, 0.10),
		"shoe_color": Color.WHITE, "skin_color": Color(0.85, 0.65, 0.50),
		"hair_color": Color(0.12, 0.09, 0.07),
		"unlock_cost": 0, "unlock_rank": 0, "unlock_titles": 0, "unlock_text": "Đã mở"})
	e.append({"id": 1, "name": "Bảo Long", "country": "VIE", "left_handed": false,
		"power": 78, "accuracy": 55, "speed": 58, "stamina": 62, "defense": 52,
		"shirt_color": Color(0.85, 0.45, 0.10), "shorts_color": Color(0.08, 0.08, 0.10),
		"shoe_color": Color.WHITE, "skin_color": Color(0.80, 0.60, 0.45),
		"hair_color": Color(0.25, 0.15, 0.08),
		"unlock_cost": 1000, "unlock_rank": 0, "unlock_titles": 0, "unlock_text": "1000 xu"})
	e.append({"id": 2, "name": "Thanh Thảo", "country": "VIE", "left_handed": false,
		"power": 52, "accuracy": 70, "speed": 80, "stamina": 65, "defense": 60,
		"shirt_color": Color(0.15, 0.60, 0.30), "shorts_color": Color(0.90, 0.90, 0.92),
		"shoe_color": Color.WHITE, "skin_color": Color(0.88, 0.68, 0.52),
		"hair_color": Color(0.10, 0.08, 0.06),
		"unlock_cost": 0, "unlock_rank": 256, "unlock_titles": 0, "unlock_text": "hạng 256 trở lên"})
	e.append({"id": 3, "name": "Đức Anh", "country": "VIE", "left_handed": true,
		"power": 55, "accuracy": 66, "speed": 60, "stamina": 72, "defense": 82,
		"shirt_color": Color(0.85, 0.75, 0.15), "shorts_color": Color(0.10, 0.15, 0.35),
		"shoe_color": Color.WHITE, "skin_color": Color(0.82, 0.62, 0.46),
		"hair_color": Color(0.12, 0.09, 0.07),
		"unlock_cost": 2500, "unlock_rank": 0, "unlock_titles": 0, "unlock_text": "2500 xu"})
	e.append({"id": 4, "name": "Hải Yến", "country": "VIE", "left_handed": false,
		"power": 58, "accuracy": 84, "speed": 66, "stamina": 60, "defense": 64,
		"shirt_color": Color(0.45, 0.25, 0.65), "shorts_color": Color(0.08, 0.08, 0.10),
		"shoe_color": Color.WHITE, "skin_color": Color(0.86, 0.66, 0.50),
		"hair_color": Color(0.30, 0.18, 0.10),
		"unlock_cost": 0, "unlock_rank": 0, "unlock_titles": 1, "unlock_text": "vô địch 1 giải đấu"})
	e.append({"id": 5, "name": "Quốc Huy", "country": "VIE", "left_handed": false,
		"power": 76, "accuracy": 74, "speed": 74, "stamina": 72, "defense": 72,
		"shirt_color": Color(0.92, 0.92, 0.95), "shorts_color": Color(0.75, 0.60, 0.20),
		"shoe_color": Color.WHITE, "skin_color": Color(0.84, 0.64, 0.48),
		"hair_color": Color(0.12, 0.09, 0.07),
		"unlock_cost": 4000, "unlock_rank": 0, "unlock_titles": 0, "unlock_text": "4000 xu"})
	return e

## Entry by id; unknown id -> the default character (id 0).
static func get_entry(id: int) -> Dictionary:
	var entries := build_entries()
	if id < 0 or id >= entries.size():
		return entries[0]
	return entries[id]

static func count() -> int:
	return build_entries().size()

# Career reference (duck-typed: Career — set by Career, used for unlock
# conditions and saving; kept untyped to avoid a parse-time class cycle).
var career = null

var _unlocked: Dictionary = {0: true}  # character id -> true
var _selected_id: int = 0

func _init(career_ref = null) -> void:
	career = career_ref

func selected_id() -> int:
	return _selected_id

func set_selected_id(id: int) -> void:
	_selected_id = clampi(id, 0, count() - 1)
	if career != null:
		career.save()

## Character unlocked yet (id 0 always unlocked).
func is_unlocked(id: int) -> bool:
	if id == 0:
		return true
	if id < 0 or id >= count():
		return false
	return _unlocked.has(id)

## Mark unlocked (no condition check — call after try_unlock).
func unlock(id: int) -> void:
	if id <= 0 or id >= count():
		return
	if not _unlocked.has(id):
		_unlocked[id] = true
		if career != null:
			career.save()

## Is the unlock condition met (money NOT spent yet)? career null or
## career not active -> not met (except free characters).
func is_unlock_condition_met(id: int) -> bool:
	if id < 0 or id >= count():
		return false
	var entry := get_entry(id)
	if int(entry["unlock_cost"]) <= 0 and int(entry["unlock_rank"]) <= 0 and int(entry["unlock_titles"]) <= 0:
		return true
	if career == null or not career.active:
		return false
	if int(entry["unlock_cost"]) > 0:
		return career.money >= int(entry["unlock_cost"])
	if int(entry["unlock_rank"]) > 0:
		return career.ranking() <= int(entry["unlock_rank"])
	if int(entry["unlock_titles"]) > 0:
		return career.titles >= int(entry["unlock_titles"])
	return false

## Unlock a character: check the condition, spend coins if needed (via
## Career.spend_money — saves itself), then mark unlocked.
## False when the condition isn't met / not enough coins.
func try_unlock(id: int) -> bool:
	if id < 0 or id >= count():
		return false
	if is_unlocked(id):
		return true
	if not is_unlock_condition_met(id):
		return false
	var entry := get_entry(id)
	if int(entry["unlock_cost"]) > 0:
		if career == null:
			return false
		if not career.spend_money(int(entry["unlock_cost"])):
			return false
	unlock(id)
	return true

# --- Save / load ----------------------------------------------------------

func load_from_data(data: Dictionary) -> void:
	_unlocked = {0: true}
	var ids: Array = data.get("unlocked_character_ids", [0])
	for raw_id in ids:
		var id := int(raw_id)
		if id >= 0 and id < count():
			_unlocked[id] = true
	_selected_id = clampi(int(data.get("selected_character_id", 0)), 0, count() - 1)

func write_to_data(data: Dictionary) -> void:
	data["unlocked_character_ids"] = _unlocked.keys()
	data["selected_character_id"] = _selected_id
