class_name AIDifficulty
extends RefCounted
## Static tuning data for the 6 AI difficulty levels (0..5).
## Pure data, no Node, no scene dependencies. All balancing for
## the AI lives here (ARCHITECTURE.md: "No hard-coded data").
## Port of ShuttleLab.AI.AIDifficulty (Unity C#).
##
## LEVEL NAMES — agreed (2026-11-10): Beginner / Amateur /
## Intermediate / Advanced / Pro / Legend. PlayerStats uses the
## same 6 names; do not change one side without the other.

## One difficulty level's tuning. Immutable value type — copy it to
## derive variants (e.g. PracticeVersion) instead of mutating.
class DifficultyConfig:
	var level: int
	var level_name: String

	## Seconds the AI waits after the opponent's hit before reacting.
	var reaction_delay: float

	## Multiplier on PlayerStats.move_speed().
	var move_speed_factor: float

	## Target dispersion (m) added to the AI's shots.
	var error_radius: float

	## 0..1: how often the AI picks attacking shots over safe ones.
	var aggression: float

	## 0..1: chance to smash a high ball when a smash is available.
	var smash_frequency: float

	## Prediction noise (m) added to the AI's landing prediction.
	var prediction_error: float

	func _init(p_level: int, p_name: String, p_reaction_delay: float,
			p_move_speed_factor: float, p_error_radius: float, p_aggression: float,
			p_smash_frequency: float, p_prediction_error: float) -> void:
		level = p_level
		level_name = p_name
		reaction_delay = p_reaction_delay
		move_speed_factor = p_move_speed_factor
		error_radius = p_error_radius
		aggression = p_aggression
		smash_frequency = p_smash_frequency
		prediction_error = p_prediction_error

	## Practice-mode variant: the AI returns everything it can reach
	## and does not make intentional errors — smaller dispersion, less
	## aggression, fewer smashes. Applied by OpponentAI when
	## MatchManager.is_practice is true.
	func practice_version() -> DifficultyConfig:
		return DifficultyConfig.new(level, level_name, reaction_delay,
			move_speed_factor, error_radius * 0.5, aggression * 0.6,
			smash_frequency * 0.7, prediction_error * 0.5)

static var _levels: Array = []

static func _ensure_levels() -> void:
	if not _levels.is_empty():
		return
	#            level  name          react  speed  error  aggr  smash  predErr
	_levels.append(DifficultyConfig.new(0, "Beginner",     0.55, 0.70, 1.20, 0.20, 0.05, 0.90))
	_levels.append(DifficultyConfig.new(1, "Amateur",      0.45, 0.78, 0.90, 0.35, 0.15, 0.65))
	_levels.append(DifficultyConfig.new(2, "Intermediate", 0.35, 0.85, 0.60, 0.50, 0.30, 0.45))
	_levels.append(DifficultyConfig.new(3, "Advanced",     0.25, 0.92, 0.40, 0.65, 0.50, 0.30))
	_levels.append(DifficultyConfig.new(4, "Pro",          0.17, 0.99, 0.25, 0.80, 0.70, 0.18))
	_levels.append(DifficultyConfig.new(5, "Legend",       0.12, 1.05, 0.15, 0.90, 0.85, 0.08))

## Config for a level; out-of-range levels are clamped to 0..5.
static func for_level(level: int) -> DifficultyConfig:
	_ensure_levels()
	return _levels[clampi(level, 0, _levels.size() - 1)]

## Display name of a level; out-of-range levels are clamped.
static func name_for(level: int) -> String:
	return for_level(level).level_name
