class_name ShuttleAerodynamics
## Shuttlecock aerodynamics. Feather shuttle reference values:
## mass 4.74-5.50 g, 16 feathers. Drag: F = -0.5 * rho * Cd * A * |v| * v.
## Cd is a tunable effective coefficient, not a universal constant — see the
## Physics Calibration Scene / ShotSolver convergence tests.
## Port of ShuttleLab.Gameplay.ShuttleAerodynamics (Unity C#).
##
## GDScript has no ref/out parameters, so step_rk2 returns [pos, vel] and
## simulate / predict_landing return a Dictionary:
##   {"landed": bool, "landing": Vector3, "flight_time": float, "apex": float}

const MASS := 0.005  # kg
const RHO := 1.225  # kg/m^3, sea level
const CD := 0.55  # effective drag coefficient
const AREA := 0.0038  # m^2 effective frontal area
const GRAVITY := 9.81  # m/s^2

static func drag_acceleration(velocity: Vector3, wind: Vector3) -> Vector3:
	var rel := velocity - wind
	var speed := rel.length()
	if speed < 0.0001:
		return Vector3.ZERO
	var k := 0.5 * RHO * CD * AREA / MASS
	return -k * speed * rel

static func acceleration(velocity: Vector3, wind: Vector3) -> Vector3:
	return drag_acceleration(velocity, wind) + Vector3(0.0, -GRAVITY, 0.0)

## One RK2 step. Returns [new_pos, new_vel].
static func step_rk2(pos: Vector3, vel: Vector3, dt: float, wind: Vector3) -> Array:
	var a1 := acceleration(vel, wind)
	var v_mid := vel + a1 * (dt * 0.5)
	var a2 := acceleration(v_mid, wind)
	var new_vel := vel + a2 * dt
	var new_pos := pos + v_mid * dt
	return [new_pos, new_vel]

## Simulate a flight until the shuttle reaches y <= 0 (court plane) or
## max_time elapses. Returns a Dictionary; "landed" is true when a landing
## was found and "landing" is the interpolated court-plane crossing.
## "apex" is the max height reached.
static func simulate(start: Vector3, v0: Vector3, wind: Vector3,
		max_time: float, dt: float) -> Dictionary:
	var pos := start
	var vel := v0
	var apex := start.y
	var t := 0.0
	var landing := start
	var flight_time := 0.0

	if start.y <= 0.0 and v0.y <= 0.0:
		landing = start
		landing.y = 0.0
		return {"landed": true, "landing": landing, "flight_time": 0.0, "apex": apex}

	while t < max_time:
		var prev_pos := pos
		var prev_t := t
		var res: Array = step_rk2(pos, vel, dt, wind)
		pos = res[0]
		vel = res[1]
		t += dt
		if pos.y > apex:
			apex = pos.y

		if pos.y <= 0.0:
			# Interpolate the exact crossing between prev_pos and pos.
			var f := clampf(prev_pos.y / (prev_pos.y - pos.y), 0.0, 1.0)
			landing = prev_pos.lerp(pos, f)
			landing.y = 0.0
			flight_time = prev_t + dt * f
			return {"landed": true, "landing": landing, "flight_time": flight_time, "apex": apex}

	landing = pos
	flight_time = max_time
	return {"landed": false, "landing": landing, "flight_time": flight_time, "apex": apex}

## Predict where the shuttle will land from its current state.
static func predict_landing(pos: Vector3, vel: Vector3, wind: Vector3) -> Dictionary:
	return simulate(pos, vel, wind, 5.0, 1.0 / 240.0)
