class_name PlayerStamina
extends Node
## Stamina pool (0-100) with intensity modes.
## Port of ShuttleLab.Players.PlayerStamina (Unity C#).
## Factors degrade smoothly once stamina drops below 30; the exhausted
## signal fires once when stamina crosses below 10 (re-arms above 15).

## Fired once when stamina drops below 10 (re-arms above 15).
signal exhausted

## How hard the player is pushing. Affects drain, power and regen.
enum IntensityMode { AGGRESSIVE, DEFAULT, EFFICIENT }

## Low-stamina threshold: factors start degrading below this.
const LOW_THRESHOLD := 30.0

## Exhaustion threshold: the exhausted signal fires below this.
const EXHAUSTED_THRESHOLD := 10.0

var mode: int = IntensityMode.DEFAULT

var max_stamina: float = 88.0
var current: float = 88.0

var _exhausted_fired: bool = false

func normalized() -> float:
	return current / max_stamina if max_stamina > 0.0 else 0.0

func is_exhausted() -> bool:
	return current < EXHAUSTED_THRESHOLD

func is_low() -> bool:
	return current < LOW_THRESHOLD

## Initialise the pool from the player's stats.
func init(stats: PlayerStats) -> void:
	max_stamina = stats.max_stamina() if stats != null else 88.0
	current = max_stamina
	_exhausted_fired = false

## Spend stamina. The cost is scaled by the given intensity mode
## (Aggressive x1.4, Efficient x0.7). Clamped at 0.
func drain(cost: float, intensity: int) -> void:
	if cost <= 0.0:
		return
	var multiplier := 1.0
	if intensity == IntensityMode.AGGRESSIVE:
		multiplier = 1.4
	elif intensity == IntensityMode.EFFICIENT:
		multiplier = 0.7
	current = maxf(0.0, current - cost * multiplier)
	if current < EXHAUSTED_THRESHOLD and not _exhausted_fired:
		_exhausted_fired = true
		exhausted.emit()

## Regenerate over time. In-rally regen is slow (2.5/s); between
## rallies it is faster (6/s). Efficient mode regenerates 1.5x faster.
## Called every frame by PlayerController.
func regen(dt: float, in_rally: bool) -> void:
	if dt <= 0.0:
		return
	var rate := 2.5 if in_rally else 6.0
	if mode == IntensityMode.EFFICIENT:
		rate *= 1.5
	current = minf(max_stamina, current + rate * dt)
	if current > 15.0:
		_exhausted_fired = false

## Top the pool up (e.g. a small breather between points).
func restore(amount: float) -> void:
	if amount <= 0.0:
		return
	current = minf(max_stamina, current + amount)
	if current > 15.0:
		_exhausted_fired = false

## Movement speed multiplier. 1.0 at/above 30 stamina, sliding down
## to 0.70 at 0 stamina.
func speed_factor() -> float:
	return 1.0 if current >= LOW_THRESHOLD else lerpf(0.70, 1.0, current / LOW_THRESHOLD)

## Shot power multiplier: low-stamina penalty (0.80 at 0 stamina)
## times the intensity-mode modifier (Aggressive x1.08, Efficient x0.94).
func power_factor() -> float:
	var base_factor := 1.0 if current >= LOW_THRESHOLD else lerpf(0.80, 1.0, current / LOW_THRESHOLD)
	var mode_factor := 1.0
	if mode == IntensityMode.AGGRESSIVE:
		mode_factor = 1.08
	elif mode == IntensityMode.EFFICIENT:
		mode_factor = 0.94
	return base_factor * mode_factor

## Dispersion multiplier for shot accuracy. 1.0 at/above 30 stamina,
## rising to 1.6x at 0 stamina (tired players spray the shuttle).
## Multiply the base dispersion by this.
func accuracy_factor() -> float:
	return 1.0 if current >= LOW_THRESHOLD else lerpf(1.6, 1.0, current / LOW_THRESHOLD)
