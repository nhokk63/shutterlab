class_name AIPerception
extends RefCounted
## Predicts where the shuttle will land and whether the AI can get there.
## Plain class, no Node. Reads the live shuttle only — it never
## touches the score, the rules, or any player other than through the
## position the caller supplies (observer_position).
##
## Prediction uses the real physics: Shuttlecock.predict_landing ->
## ShuttleAerodynamics.predict_landing -> simulate (RK2 with drag).
## Difficulty is modelled as noise on the predicted landing point, so a
## low-level AI runs to the wrong spot and a high-level AI barely errs.
##
## LIMITS (same as Unity):
## - The prediction ignores the net and the posts (the simulation only
##   integrates flight to the court plane). OpponentAI re-predicts every
##   frame, so the error corrects itself within a few frames.
## - The prediction also ignores the opponent: it is a pure ballistics
##   forecast of the current flight, not a guess about the next shot.
## Port of ShuttleLab.AI.AIPerception (Unity C#).

var _shuttle: Shuttlecock

## Prediction noise (m) applied to the landing point. Set from
## DifficultyConfig.prediction_error (0 = perfect prediction).
var prediction_error: float = 0.0

## The AI player's current position. OpponentAI sets this every
## frame before calling can_reach — perception itself never finds
## players in the scene.
var observer_position: Vector3 = Vector3.ZERO

## Last prediction, if try_predict succeeded this frame.
var last_landing: Vector3 = Vector3.ZERO

## Time (s) until the last predicted landing.
var last_time_to_land: float = 0.0

var has_prediction: bool = false

func init(shuttle: Shuttlecock) -> void:
	_shuttle = shuttle
	has_prediction = false

## Predict the landing point and time-to-land of the current flight.
## Returns {"ok": bool, "landing": Vector3, "time_to_land": float}.
## ok is false when there is no flight to predict (shuttle held,
## dead, or not the opponent's shot). The returned landing carries
## the difficulty noise; the raw physics landing is never exposed.
func try_predict() -> Dictionary:
	var fail := {"ok": false, "landing": Vector3.ZERO, "time_to_land": 0.0}
	has_prediction = false

	if _shuttle == null:
		return fail
	if _shuttle.state != Shuttlecock.State.FLYING:
		return fail
	# Only predict the opponent's shots — our own shot flying away
	# needs no prediction.
	if _shuttle.last_hitter == 1:
		return fail

	var raw: Dictionary = _shuttle.predict_landing()
	if not raw["landed"]:
		return fail

	var raw_landing: Vector3 = raw["landing"]
	var raw_time: float = raw["flight_time"]

	# Difficulty noise: uniform disc of radius prediction_error.
	if prediction_error > 0.001:
		var angle := randf() * TAU
		var radius := prediction_error * sqrt(randf())
		raw_landing += Vector3(cos(angle), 0.0, sin(angle)) * radius

	last_landing = raw_landing
	last_time_to_land = raw_time
	has_prediction = true
	return {"ok": true, "landing": raw_landing, "time_to_land": raw_time}

## Can the observer reach the landing point before the shuttle gets
## there? move_speed is the observer's effective court speed (m/s,
## already including difficulty and stamina factors). A small margin
## is kept: the AI must arrive a fraction before the shuttle, and
## its racket reach counts towards the distance.
func can_reach(landing: Vector3, time_to_land: float, move_speed: float) -> bool:
	if time_to_land <= 0.0 or move_speed <= 0.0:
		return false

	var flat := landing - observer_position
	flat.y = 0.0
	var distance := flat.length()

	var arrive_early := 0.05  # must be there slightly early
	var reach_bonus := 1.15  # racket reach (PlayerController.reach_radius)
	var time_available := maxf(0.0, time_to_land - arrive_early)
	return distance - reach_bonus <= move_speed * time_available
