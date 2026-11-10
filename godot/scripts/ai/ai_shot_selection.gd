class_name AIShotSelection
extends RefCounted
## Picks the AI's shot (type + landing target) for one contact.
## Plain class, no Node. Decisions come only from verifiable
## state — shuttle height/position, the two players' court positions,
## stamina, stats and difficulty — never from a bare random roll.
## Randomness (RandomNumberGenerator, passed in by the caller) is used
## only for target dispersion and for choosing between near-equal options.
##
## SELECTION PRINCIPLES (checked against ShotLibrary profiles):
## - High ball near the net (h >= 1.6 m, above the tape) -> NetKill if the
##   AI is aggressive enough, else NetShot / Push.
## - High ball, mid/back court (h >= 2.0 m) -> Smash / JumpSmash when
##   stamina allows and the difficulty's SmashFrequency roll passes,
##   else HalfSmash / Clear.
## - Deep on the AI's own side (shuttle z > 4.5 m) -> Clear, or Drop if
##   aggressive (attack the open net area).
## - Mid court (h 0.9..2.2 m) -> Drive, or Push.
## - Pressured (low ball h < 0.9 m, or the shuttle already past the AI)
##   -> Lift (safe, deep) or DefensiveDrive.
## - NO SMASH when stamina < 25% (stamina.normalized() < 0.25).
## - Every candidate is filtered through ShotLibrary.can_attempt(type, h);
##   if nothing fits, ShotSolver.fallback_shot(h) decides (Clear / Lift /
##   Drive by height), then Lift as the last resort.
##
## TARGETING: open space — the corner away from the opponent (mirrored X,
## deep Z on the USER side, so Z is NEGATIVE here), plus dispersion from
## difficulty + the AI's own accuracy + low-stamina spray. Targets are
## clamped inside the singles court with a 0.35 m margin, so the AI never
## deliberately aims out — misses come from dispersion, not intent.
## Port of ShuttleLab.AI.AIShotSelection (Unity C#).

var _shuttle: Shuttlecock

# Net tape height: a kill below the tape would hit the net, so the
# NetKill window starts above it (profile min is 1.2 m, we require
# 1.6 m — see choose).
const NET_TAPE_HEIGHT := 1.55
const TARGET_MARGIN := 0.35

func init(shuttle: Shuttlecock) -> void:
	_shuttle = shuttle

## Choose a shot for the current contact. state is the shuttle's
## state; ai_pos / opp_pos are the two players' positions (AI on the
## far side, z > 0; opponent on the near side, z < 0).
## Returns {"type": int, "target": Vector3}.
func choose(state: int, ai_pos: Vector3, opp_pos: Vector3,
		ai_stats: PlayerStats, stamina: PlayerStamina,
		diff: AIDifficulty.DifficultyConfig, rng: RandomNumberGenerator) -> Dictionary:
	if rng == null:
		rng = RandomNumberGenerator.new()
		rng.randomize()

	# Not a live rally shuttle: safe defensive default. The caller
	# (OpponentAI) normally never calls choose in this case.
	if state != Shuttlecock.State.FLYING or _shuttle == null:
		return {"type": ShotData.ShotType.LIFT, "target": _deep_target(opp_pos, rng, diff, ai_stats, stamina)}

	var shuttle_pos := _shuttle.global_position
	var h := shuttle_pos.y
	var ai_z := absf(ai_pos.z)  # AI distance from the net
	var shuttle_z := shuttle_pos.z  # > 0 on the AI side
	var near_net := shuttle_z < 2.2
	var deep := shuttle_z > 4.5 or ai_z > 4.5
	var pressured := h < 0.9 or shuttle_z > ai_pos.z + 1.0  # low, or already past the AI
	var high_ball := h >= 2.0
	var killable := h >= NET_TAPE_HEIGHT + 0.05

	var can_smash := stamina == null or stamina.normalized() >= 0.25

	var shot_type: int

	if pressured:
		# Get out of trouble: lift deep, or a flat defensive drive.
		shot_type = ShotData.ShotType.DEFENSIVE_DRIVE if (h >= 0.5 and rng.randf() < 0.35) else ShotData.ShotType.LIFT
	elif near_net and killable:
		shot_type = ShotData.ShotType.NET_KILL if rng.randf() < diff.aggression else ShotData.ShotType.NET_SHOT
	elif near_net:
		shot_type = ShotData.ShotType.NET_SHOT if rng.randf() < 0.5 else ShotData.ShotType.PUSH
	elif high_ball:
		if can_smash and rng.randf() < diff.smash_frequency:
			shot_type = ShotData.ShotType.JUMP_SMASH if (h >= 2.2 and stamina != null and stamina.normalized() > 0.5 and rng.randf() < 0.4) else ShotData.ShotType.SMASH
		elif rng.randf() < 0.5:
			shot_type = ShotData.ShotType.HALF_SMASH
		else:
			shot_type = ShotData.ShotType.CLEAR
	elif deep:
		# Deep ball: clear it away, or attack with a drop.
		shot_type = ShotData.ShotType.DROP if rng.randf() < diff.aggression * 0.5 else ShotData.ShotType.CLEAR
	else:
		# Mid court: flat drive, or a push towards the net.
		shot_type = ShotData.ShotType.DRIVE if rng.randf() < 0.5 + diff.aggression * 0.3 else ShotData.ShotType.PUSH

	# Enforce the height window of the chosen profile; fall back to
	# the height-based default, then to Lift.
	if not ShotData.ShotLibrary.can_attempt(shot_type, h):
		shot_type = ShotSolver.fallback_shot(h)
		if not ShotData.ShotLibrary.can_attempt(shot_type, h):
			shot_type = ShotData.ShotType.LIFT

	var target := _target_for(shot_type, opp_pos, rng, diff, ai_stats, stamina)
	return {"type": shot_type, "target": target}

# --- Targeting -------------------------------------------------------

func _target_for(shot_type: int, opp_pos: Vector3, rng: RandomNumberGenerator,
		diff: AIDifficulty.DifficultyConfig, ai_stats: PlayerStats,
		stamina: PlayerStamina) -> Vector3:
	match shot_type:
		ShotData.ShotType.DROP, ShotData.ShotType.NET_SHOT, ShotData.ShotType.NET_KILL, ShotData.ShotType.PUSH:
			# Short: just behind the net on the user's side.
			return _short_target(opp_pos, rng, diff, ai_stats, stamina)
		ShotData.ShotType.SMASH, ShotData.ShotType.JUMP_SMASH, ShotData.ShotType.HALF_SMASH:
			# Steep: at the opponent's feet / away from them, mid-deep.
			return _smash_target(opp_pos, rng, diff, ai_stats, stamina)
		_:
			# Clear / Lift / Drive / DefensiveDrive: deep corners.
			return _deep_target(opp_pos, rng, diff, ai_stats, stamina)

## Deep corner away from the opponent (user side: z negative).
func _deep_target(opp_pos: Vector3, rng: RandomNumberGenerator,
		diff: AIDifficulty.DifficultyConfig, ai_stats: PlayerStats,
		stamina: PlayerStamina) -> Vector3:
	var x := -signf(1.0 if opp_pos.x == 0.0 else opp_pos.x) * (1.4 + rng.randf() * 0.8)
	var z := -(5.4 + rng.randf() * 0.9)
	return _clamp_and_disperse(Vector3(x, 0, z), rng, diff, ai_stats, stamina)

## Just behind the net, away from the opponent.
func _short_target(opp_pos: Vector3, rng: RandomNumberGenerator,
		diff: AIDifficulty.DifficultyConfig, ai_stats: PlayerStats,
		stamina: PlayerStamina) -> Vector3:
	var x := -signf(1.0 if opp_pos.x == 0.0 else opp_pos.x) * (0.8 + rng.randf() * 1.2)
	var z := -(0.9 + rng.randf() * 0.7)
	return _clamp_and_disperse(Vector3(x, 0, z), rng, diff, ai_stats, stamina)

## Mid-deep, on the side the opponent is not covering.
func _smash_target(opp_pos: Vector3, rng: RandomNumberGenerator,
		diff: AIDifficulty.DifficultyConfig, ai_stats: PlayerStats,
		stamina: PlayerStamina) -> Vector3:
	var x := -signf(1.0 if opp_pos.x == 0.0 else opp_pos.x) * (1.2 + rng.randf() * 1.0)
	var z := -(3.4 + rng.randf() * 2.0)
	return _clamp_and_disperse(Vector3(x, 0, z), rng, diff, ai_stats, stamina)

func _clamp_and_disperse(target: Vector3, rng: RandomNumberGenerator,
		diff: AIDifficulty.DifficultyConfig, ai_stats: PlayerStats,
		stamina: PlayerStamina) -> Vector3:
	# Dispersion: difficulty error + the AI's own accuracy, widened
	# when stamina is low (tired players spray the shuttle).
	var dispersion := diff.error_radius + (ai_stats.accuracy_dispersion() if ai_stats != null else 0.2)
	if stamina != null:
		dispersion *= stamina.accuracy_factor()

	if dispersion > 0.001:
		var angle := rng.randf() * TAU
		var radius := dispersion * sqrt(rng.randf())
		target += Vector3(cos(angle), 0.0, sin(angle)) * radius

	var hw := CourtDimensions.SINGLES_HALF_WIDTH - TARGET_MARGIN
	var z_max := CourtDimensions.COURT_HALF_LENGTH - TARGET_MARGIN
	target.x = clampf(target.x, -hw, hw)
	target.z = clampf(target.z, -z_max, -0.6)  # user side only
	target.y = 0.0
	return target
