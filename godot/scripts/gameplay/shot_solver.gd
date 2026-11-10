class_name ShotSolver
## Solves the launch velocity for a shot: find v0 so that the drag-affected
## simulated trajectory lands on the target. Shooting method: ballistic
## initial guess, then simulate with ShuttleAerodynamics and correct v0.
## The flight itself is always simulated live by Shuttlecock —
## the solver only picks the initial velocity, never the path.
## Port of ShuttleLab.Gameplay.ShotSolver (Unity C#).
##
## solve() returns {"ok": bool, "launch_velocity": Vector3}.
## (GDScript has no out parameters; "type" is renamed to "shot_type".)

const MAX_ITERATIONS := 12
const CONVERGE_DISTANCE := 0.15
const SIM_DT := 1.0 / 240.0

## Solve for a launch velocity from start to target for the given shot
## type and quality. Returns ok=false when the shot is physically
## unreachable (e.g. a drop from the back court) — the caller must
## fall back to another shot (usually Lift/Clear).
static func solve(start: Vector3, target: Vector3, shot_type: int,
		quality: int, wind: Vector3) -> Dictionary:
	var fail := {"ok": false, "launch_velocity": Vector3.ZERO}
	var profile: ShotData.ShotProfile = ShotData.ShotLibrary.get_profile(shot_type)
	if profile == null:
		return fail

	# Apply quality: dispersion + power loss. Poor quality may push the
	# effective target out of the court — that is the risk, by design.
	var aim := target
	var dispersion := ShotData.ShotTiming.dispersion(quality)
	if dispersion > 0.01:
		# Deterministic dispersion from a hash of the inputs (no RNG in
		# the solver path, so replays/tests are reproducible).
		var h := sin(start.x * 12.9898 + start.z * 78.233 + target.x * 37.719) * 43758.5453
		var frac := h - floorf(h)
		var angle := frac * TAU
		aim += Vector3(cos(angle), 0.0, sin(angle)) * dispersion

	var flight_time := profile.flight_time
	var to_target := aim - start

	# Ballistic initial guess (no drag).
	var v0 := Vector3(
		to_target.x / flight_time,
		(to_target.y + 0.5 * ShuttleAerodynamics.GRAVITY * flight_time * flight_time) / flight_time,
		to_target.z / flight_time)

	# Enforce a minimum arc for high shots: raise the apex by lifting vy.
	var want_apex := maxf(profile.apex_height, start.y + 0.5)
	var apex_vy := sqrt(2.0 * ShuttleAerodynamics.GRAVITY * maxf(0.1, want_apex - start.y))
	if shot_type == ShotData.ShotType.CLEAR or shot_type == ShotData.ShotType.LIFT or shot_type == ShotData.ShotType.SERVE_HIGH:
		v0.y = maxf(v0.y, apex_vy * 0.85)

	# Shooting iterations with drag. Power factor is applied INSIDE the
	# loop so convergence is checked on the velocity actually returned
	# (the old code converged pre-power, then scaled down and landed short).
	for i in MAX_ITERATIONS:
		var v_eff := v0 * ShotData.ShotTiming.power_factor(quality)
		var sim: Dictionary = ShuttleAerodynamics.simulate(start, v_eff, wind, 5.0, SIM_DT)
		if not sim["landed"]:
			return fail

		var sim_landing: Vector3 = sim["landing"]
		var sim_time: float = sim["flight_time"]
		var error := aim - sim_landing
		error.y = 0.0
		var err_dist := error.length()
		if err_dist < CONVERGE_DISTANCE:
			break

		# Drag-aware correction: under quadratic drag, range is
		# logarithmic in launch speed (range ~ ln(1+k*vh*t)/k), so the
		# linear correction must be scaled by (1+k*vh*t) to converge
		# on long shots (a Clear needs ~45 m/s from a 12.7 m/s guess).
		var k := 0.5 * ShuttleAerodynamics.RHO * ShuttleAerodynamics.CD \
			* ShuttleAerodynamics.AREA / ShuttleAerodynamics.MASS
		var vh := Vector2(v0.x, v0.z).length()
		var drag_scale := 1.0 + k * vh * maxf(0.2, sim_time)
		v0.x += error.x / maxf(0.2, sim_time) * drag_scale
		v0.z += error.z / maxf(0.2, sim_time) * drag_scale
		var time_err := flight_time - sim_time
		v0.y += time_err * 2.0

		if v0.length() > 75.0:
			return fail  # unreachable at sane speeds
		if i == MAX_ITERATIONS - 1:
			return fail  # never accept an unverified velocity

	# Final verification: re-simulate the velocity we are about to return.
	var v_final := v0 * ShotData.ShotTiming.power_factor(quality)
	var ok_final: Dictionary = ShuttleAerodynamics.simulate(start, v_final, wind, 5.0, SIM_DT)
	if not ok_final["landed"]:
		return fail
	var final_landing: Vector3 = ok_final["landing"]
	var final_error := aim - final_landing
	final_error.y = 0.0
	if final_error.length() > CONVERGE_DISTANCE:
		return fail
	v0 = v_final

	# Sanity: must go roughly towards the target and over the net.
	var dir := aim - start
	dir.y = 0.0
	if dir.length_squared() > 0.01:
		var hv := v0
		hv.y = 0.0
		if hv.normalized().dot(dir.normalized()) < 0.5:
			return fail

	return {"ok": true, "launch_velocity": v0}

## Solve a serve into the diagonal service box.
static func solve_serve(start: Vector3, box_centre: Vector3, serve_type: int,
		quality: int, wind: Vector3) -> Dictionary:
	return solve(start, box_centre, serve_type, quality, wind)

## Pick a fallback shot when the requested one is unreachable.
## High shuttle -> Clear; low shuttle -> Lift; mid -> Drive.
static func fallback_shot(shuttle_height: float) -> int:
	if shuttle_height > 1.9:
		return ShotData.ShotType.CLEAR
	if shuttle_height < 1.0:
		return ShotData.ShotType.LIFT
	return ShotData.ShotType.DRIVE
