class_name PlayerStats
extends RefCounted
## The 5 core attributes (1-99) of a badminton player plus identity/kit.
## Pure data + derived values. Port of ShuttleLab.Players.PlayerStats (Unity C#).
## Derived formulas are the v0.1.0 design values from ARCHITECTURE.md.
##
## NOTE: this is the single canonical PlayerStats. The progression port
## (scripts/progression/) previously carried a duplicate copy; it was
## removed and the roster lookup below is the real one (CharacterRoster
## from the Systems/progression port), not the old stub.

var player_name: String = "Player"
var country: String = "VIE"

## True = racket in the left hand.
var left_handed: bool = false

# Core attributes (1-99).
var power: int = 60
var accuracy: int = 60
var speed: int = 60
var stamina: int = 60
var defense: int = 60

# Kit / appearance colours.
var shirt_color: Color = Color(0.10, 0.35, 0.75)  # blue
var shorts_color: Color = Color(0.08, 0.08, 0.10)  # near-black
var shoe_color: Color = Color.WHITE
var skin_color: Color = Color(0.85, 0.65, 0.50)
var hair_color: Color = Color(0.12, 0.09, 0.07)

# --- Derived values -------------------------------------------------------

## Max court speed in m/s. 60 Speed -> 7.3 m/s.
func move_speed() -> float:
	return 5.5 + speed * 0.03

## Multiplier applied to shot power. 60 Power -> 1.03.
func shot_power() -> float:
	return 0.85 + power * 0.003

## Base target dispersion in metres from the player's own accuracy
## (before shot-quality dispersion is added). 60 Accuracy -> 0.23 m. Floor 0.08 m.
func accuracy_dispersion() -> float:
	return maxf(0.08, 0.35 - accuracy * 0.002)

## Stamina pool (0-100 scale). 60 Stamina -> 88.
func max_stamina() -> float:
	return 70.0 + stamina * 0.3

## Deep copy (equipment apply returns a copy, never the base stats).
func clone() -> PlayerStats:
	var c := PlayerStats.new()
	c.player_name = player_name
	c.country = country
	c.left_handed = left_handed
	c.power = power
	c.accuracy = accuracy
	c.speed = speed
	c.stamina = stamina
	c.defense = defense
	c.shirt_color = shirt_color
	c.shorts_color = shorts_color
	c.shoe_color = shoe_color
	c.skin_color = skin_color
	c.hair_color = hair_color
	return c

# --- Factories ------------------------------------------------------------

## A balanced ~60-stat player (the default human player).
static func default_player() -> PlayerStats:
	var s := PlayerStats.new()
	s.player_name = "Player 1"
	s.country = "VIE"
	s.left_handed = false
	s.power = 60
	s.accuracy = 62
	s.speed = 60
	s.stamina = 60
	s.defense = 58
	s.shirt_color = Color(0.10, 0.35, 0.75)
	s.shorts_color = Color(0.08, 0.08, 0.10)
	return s

## Stats of a character in the roster (CharacterRoster). Id 0 = the default
## character (same stats as default_player, but name/kit from the roster);
## unknown id -> the default character.
static func from_character(id: int) -> PlayerStats:
	var entry: Dictionary = CharacterRoster.get_entry(id)
	var s := PlayerStats.new()
	s.player_name = String(entry["name"])
	s.country = String(entry["country"])
	s.left_handed = bool(entry["left_handed"])
	s.power = int(entry["power"])
	s.accuracy = int(entry["accuracy"])
	s.speed = int(entry["speed"])
	s.stamina = int(entry["stamina"])
	s.defense = int(entry["defense"])
	s.shirt_color = entry["shirt_color"]
	s.shorts_color = entry["shorts_color"]
	s.shoe_color = entry["shoe_color"]
	s.skin_color = entry["skin_color"]
	s.hair_color = entry["hair_color"]
	return s

## AI stats for a difficulty level 0..5:
## 0 Beginner ~35, 1 Amateur ~50, 2 Intermediate ~62,
## 3 Advanced ~74, 4 Pro ~84, 5 Legend ~92.
## Out-of-range levels are clamped.
static func from_difficulty(level: int) -> PlayerStats:
	level = clampi(level, 0, 5)
	var bases: Array[int] = [35, 50, 62, 74, 84, 92]
	var b: int = bases[level]
	var s := PlayerStats.new()
	s.player_name = difficulty_name(level)
	s.country = "AI"
	s.left_handed = false
	s.power = clamp_stat(b + 2)
	s.accuracy = clamp_stat(b)
	s.speed = clamp_stat(b + 1)
	s.stamina = clamp_stat(b - 2)
	s.defense = clamp_stat(b)
	s.shirt_color = Color(0.75, 0.15, 0.15)
	s.shorts_color = Color(0.08, 0.08, 0.10)
	return s

# Difficulty names — MUST match AIDifficulty (Beginner/Amateur/
# Intermediate/Advanced/Pro/Legend). Don't change one side only.
static func difficulty_name(level: int) -> String:
	match clampi(level, 0, 5):
		0:
			return "Beginner"
		1:
			return "Amateur"
		2:
			return "Intermediate"
		3:
			return "Advanced"
		4:
			return "Pro"
		_:
			return "Legend"

static func clamp_stat(v: int) -> int:
	return clampi(v, 1, 99)

## Clamp every attribute into 1..99 (call after manual edits).
func clamp_all() -> void:
	power = clamp_stat(power)
	accuracy = clamp_stat(accuracy)
	speed = clamp_stat(speed)
	stamina = clamp_stat(stamina)
	defense = clamp_stat(defense)
