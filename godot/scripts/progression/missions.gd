class_name WeeklyMissions
extends RefCounted
## Weekly missions: 3 missions rotating on the ISO week, coins paid into
## Career. Port of ShuttleLab.Systems.WeeklyMissions (Unity C#).
##  - Win 3 matches in the week -> +100 coins
##  - 10 smashes (USER's only) in the week -> +200 coins
##  - Win 1 match without losing a game -> +300 coins
##
## Progress is recorded via record_match(won, smashes, games_lost) —
## the match flow calls it at match end (practice matches don't count).
## Rewards are paid IMMEDIATELY when a mission is met (no separate
## "claim" step), once per week per mission.
##
## Storage: merged into the main save file (data["missions"]) — one file,
## same atomic write + backup as the rest of the save. Unity deleted a
## legacy separate file "shuttlelab_missions_v1.json" once; the Godot
## port does the same one-time cleanup if it ever finds one.
##
## Money only lives in Career: record_match does nothing when career is
## null or not active (no CareerManager means nowhere to pay the reward).

const WINS_TARGET := 3
const WINS_REWARD := 100
const SMASHES_TARGET := 10
const SMASHES_REWARD := 200
const CLEAN_WIN_REWARD := 300

const LEGACY_FILE_NAME := "shuttlelab_missions_v1.json"

# Career reference (duck-typed: Career — set by Career, used for money
# and saving; kept untyped to avoid a parse-time class cycle).
var career = null

var _week_key: String = ""
var _wins: int = 0
var _smashes: int = 0
var _clean_win_done: bool = false
var _wins_rewarded: bool = false
var _smashes_rewarded: bool = false
var _clean_win_rewarded: bool = false
var _legacy_cleaned: bool = false

func _init(career_ref = null) -> void:
	career = career_ref

# --- ISO 8601 week ---------------------------------------------------------

static func _is_leap_year(year: int) -> bool:
	return (year % 4 == 0 and year % 100 != 0) or year % 400 == 0

static func _day_of_year(year: int, month: int, day: int) -> int:
	var days_before: Array[int] = [0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334]
	var doy: int = days_before[month - 1] + day
	if month > 2 and _is_leap_year(year):
		doy += 1
	return doy

## {year, week} of the ISO week containing today. The Thursday decides
## the ISO year (ISO 8601): Monday = 0 .. Sunday = 6.
static func iso_parts() -> Dictionary:
	var now := Time.get_datetime_dict_from_system()
	var dow: int = int(now["weekday"])  # 0 = Sunday .. 6 = Saturday
	var monday_based := (dow + 6) % 7
	var today_unix := Time.get_unix_time_from_datetime_dict(now)
	var thursday_unix := today_unix + (3 - monday_based) * 86400
	var th := Time.get_datetime_dict_from_unix_time(thursday_unix)
	var iso_year: int = int(th["year"])
	var doy := _day_of_year(iso_year, int(th["month"]), int(th["day"]))
	var week := (doy - 1) / 7 + 1
	return {"year": iso_year, "week": week}

## Current week key, e.g. "2026-W46".
static func current_week_key() -> String:
	var parts := iso_parts()
	return "%d-W%02d" % [int(parts["year"]), int(parts["week"])]

## Current ISO week number (1..53), same standard as current_week_key.
static func current_week_number() -> int:
	return int(iso_parts()["week"])

# --- Main API --------------------------------------------------------------

## Record one real match (match flow calls at match end).
## won: user won the match; smashes: USER's smashes in the match
## (Stats.UserSmashes); games_lost: games the user lost.
## Does nothing when career is null or not active. Pays rewards the
## moment a mission is met.
func record_match(won: bool, smashes: int, games_lost: int) -> void:
	if career == null or not career.active:
		return
	_ensure_week()
	if won:
		_wins += 1
	if smashes > 0:
		_smashes += smashes
	if won and games_lost <= 0:
		_clean_win_done = true
	_grant_due_rewards()
	if career != null:
		career.save()

## Status text for the UI.
func get_status_text() -> String:
	_ensure_week()
	var wins_state := "hoàn thành — đã nhận +100 xu" if _wins_rewarded else "tiến độ %d/%d" % [_wins, WINS_TARGET]
	var smash_state := "hoàn thành — đã nhận +200 xu" if _smashes_rewarded else "tiến độ %d/%d" % [_smashes, SMASHES_TARGET]
	var clean_state := "hoàn thành — đã nhận +300 xu" if _clean_win_rewarded else "chưa xong"
	return "Nhiệm vụ tuần (%s):\n• Thắng 3 trận: %s\n• Đập 10 cú smash: %s\n• Thắng 1 trận không thua ván: %s" % [
		_week_key, wins_state, smash_state, clean_state]

# --- Progress (read for the UI) -------------------------------------------

func wins_progress() -> int:
	_ensure_week()
	return _wins

func smashes_progress() -> int:
	_ensure_week()
	return _smashes

func clean_win_done() -> bool:
	_ensure_week()
	return _clean_win_done

func wins_rewarded() -> bool:
	_ensure_week()
	return _wins_rewarded

func smashes_rewarded() -> bool:
	_ensure_week()
	return _smashes_rewarded

func clean_win_rewarded() -> bool:
	_ensure_week()
	return _clean_win_rewarded

# --- Internal --------------------------------------------------------------

func _grant_due_rewards() -> void:
	if _wins >= WINS_TARGET and not _wins_rewarded:
		career.add_money(WINS_REWARD)
		_wins_rewarded = true
	if _smashes >= SMASHES_TARGET and not _smashes_rewarded:
		career.add_money(SMASHES_REWARD)
		_smashes_rewarded = true
	if _clean_win_done and not _clean_win_rewarded:
		career.add_money(CLEAN_WIN_REWARD)
		_clean_win_rewarded = true

## New week: reset progress + reward flags, save.
func _ensure_week() -> void:
	if not _legacy_cleaned:
		_legacy_cleaned = true
		var legacy_path := "user://" + LEGACY_FILE_NAME
		if FileAccess.file_exists(legacy_path):
			DirAccess.remove_absolute(legacy_path)
		var legacy_backup := legacy_path + ".bak"
		if FileAccess.file_exists(legacy_backup):
			DirAccess.remove_absolute(legacy_backup)
	var key := current_week_key()
	if _week_key == key:
		return
	_week_key = key
	_wins = 0
	_smashes = 0
	_clean_win_done = false
	_wins_rewarded = false
	_smashes_rewarded = false
	_clean_win_rewarded = false
	if career != null:
		career.save()

# --- Save / load ----------------------------------------------------------

func load_from_data(data: Dictionary) -> void:
	var m: Dictionary = data.get("missions", {})
	_week_key = String(m.get("week_key", ""))
	_wins = int(m.get("wins", 0))
	_smashes = int(m.get("smashes", 0))
	_clean_win_done = bool(m.get("clean_win_done", false))
	_wins_rewarded = bool(m.get("wins_rewarded", false))
	_smashes_rewarded = bool(m.get("smashes_rewarded", false))
	_clean_win_rewarded = bool(m.get("clean_win_rewarded", false))

func write_to_data(data: Dictionary) -> void:
	data["missions"] = {
		"week_key": _week_key,
		"wins": _wins,
		"smashes": _smashes,
		"clean_win_done": _clean_win_done,
		"wins_rewarded": _wins_rewarded,
		"smashes_rewarded": _smashes_rewarded,
		"clean_win_rewarded": _clean_win_rewarded,
	}
