class_name ShotData
## Shot types, quality, timing windows and per-shot profiles.
## Port of ShuttleLab.Gameplay.ShotData (Unity C#), which contains
## ShotType, ShotQuality, ShotTiming, ShotProfile and ShotLibrary.
## Enum order matches the Unity enums exactly (Clear = 0, ...).

enum ShotType {
	CLEAR, DROP, SMASH, JUMP_SMASH, HALF_SMASH,
	DRIVE, LIFT, NET_SHOT, NET_KILL, PUSH, BLOCK, DEFENSIVE_DRIVE,
	SERVE_LOW, SERVE_HIGH, SERVE_FLICK,
}

enum ShotQuality { MISS, EARLY, LATE, GOOD, PERFECT }

static func shot_type_name(t: int) -> String:
	var keys := ShotType.keys()
	if t >= 0 and t < keys.size():
		return String(keys[t])
	return "?"

static func shot_quality_name(q: int) -> String:
	var keys := ShotQuality.keys()
	if q >= 0 and q < keys.size():
		return String(keys[q])
	return "?"

## Timing window around the ideal contact moment (seconds). Quality is
## computed from timing + contact position + posture + stamina — never
## from a bare random roll.
class ShotTiming:
	const PERFECT_WINDOW := 0.040
	const GOOD_WINDOW := 0.090
	const LATE_WINDOW := 0.160

	static func from_timing_error(error_seconds: float) -> int:
		var e := absf(error_seconds)
		if e <= PERFECT_WINDOW:
			return ShotData.ShotQuality.PERFECT
		if e <= GOOD_WINDOW:
			return ShotData.ShotQuality.GOOD
		if e <= LATE_WINDOW:
			return ShotData.ShotQuality.LATE if error_seconds > 0.0 else ShotData.ShotQuality.EARLY
		return ShotData.ShotQuality.MISS

	## Target dispersion (metres) caused by quality.
	static func dispersion(q: int) -> float:
		match q:
			ShotData.ShotQuality.PERFECT:
				return 0.08
			ShotData.ShotQuality.GOOD:
				return 0.18
			ShotData.ShotQuality.LATE:
				return 0.55
			ShotData.ShotQuality.EARLY:
				return 0.60
			_:
				return 1.20

	## Power factor caused by quality (1 = full).
	static func power_factor(q: int) -> float:
		match q:
			ShotData.ShotQuality.PERFECT:
				return 1.00
			ShotData.ShotQuality.GOOD:
				return 0.96
			ShotData.ShotQuality.LATE:
				return 0.88
			ShotData.ShotQuality.EARLY:
				return 0.85
			_:
				return 0.60

## Tunable profile per shot type. Values are design decisions for
## v0.1.0, to be refined against the Physics Calibration Scene (CSV logs).
class ShotProfile extends RefCounted:
	var type: int
	var flight_time: float  # target flight time (s)
	var apex_height: float  # target apex above the court (m)
	var min_shuttle_height: float  # min shuttle height to attempt (m)
	var max_shuttle_height: float  # max shuttle height to attempt (m)
	var stamina_cost: float  # 0..100 scale
	var overhead: bool  # hit above the head
	var attacking: bool  # aggressive intent

	func _init(p_type: int, p_flight_time: float, p_apex_height: float,
			p_min_h: float, p_max_h: float, p_stamina: float,
			p_overhead: bool, p_attacking: bool) -> void:
		type = p_type
		flight_time = p_flight_time
		apex_height = p_apex_height
		min_shuttle_height = p_min_h
		max_shuttle_height = p_max_h
		stamina_cost = p_stamina
		overhead = p_overhead
		attacking = p_attacking

class ShotLibrary:
	static var _profiles: Dictionary = {}

	static func _ensure_profiles() -> void:
		if not _profiles.is_empty():
			return
		_profiles[ShotData.ShotType.CLEAR] = ShotData.ShotProfile.new(ShotData.ShotType.CLEAR, 1.15, 7.5, 1.6, 9.0, 6.0, true, false)
		_profiles[ShotData.ShotType.DROP] = ShotData.ShotProfile.new(ShotData.ShotType.DROP, 0.95, 2.6, 1.6, 9.0, 6.0, true, true)
		_profiles[ShotData.ShotType.SMASH] = ShotData.ShotProfile.new(ShotData.ShotType.SMASH, 0.45, 2.2, 2.0, 9.0, 14.0, true, true)
		_profiles[ShotData.ShotType.JUMP_SMASH] = ShotData.ShotProfile.new(ShotData.ShotType.JUMP_SMASH, 0.42, 2.4, 2.2, 9.0, 18.0, true, true)
		_profiles[ShotData.ShotType.HALF_SMASH] = ShotData.ShotProfile.new(ShotData.ShotType.HALF_SMASH, 0.55, 2.4, 1.9, 9.0, 11.0, true, true)
		_profiles[ShotData.ShotType.DRIVE] = ShotData.ShotProfile.new(ShotData.ShotType.DRIVE, 0.90, 3.5, 0.9, 2.2, 7.0, false, true)
		_profiles[ShotData.ShotType.LIFT] = ShotData.ShotProfile.new(ShotData.ShotType.LIFT, 1.05, 6.5, 0.2, 1.6, 7.0, false, false)
		_profiles[ShotData.ShotType.NET_SHOT] = ShotData.ShotProfile.new(ShotData.ShotType.NET_SHOT, 0.90, 3.5, 0.9, 1.7, 4.0, false, false)
		_profiles[ShotData.ShotType.NET_KILL] = ShotData.ShotProfile.new(ShotData.ShotType.NET_KILL, 0.60, 3.0, 1.2, 2.0, 8.0, false, true)
		_profiles[ShotData.ShotType.PUSH] = ShotData.ShotProfile.new(ShotData.ShotType.PUSH, 0.90, 3.5, 0.9, 1.8, 5.0, false, true)
		_profiles[ShotData.ShotType.BLOCK] = ShotData.ShotProfile.new(ShotData.ShotType.BLOCK, 0.90, 3.5, 0.5, 2.0, 4.0, false, false)
		_profiles[ShotData.ShotType.DEFENSIVE_DRIVE] = ShotData.ShotProfile.new(ShotData.ShotType.DEFENSIVE_DRIVE, 0.90, 3.5, 0.5, 2.0, 6.0, false, false)
		_profiles[ShotData.ShotType.SERVE_LOW] = ShotData.ShotProfile.new(ShotData.ShotType.SERVE_LOW, 1.20, 3.5, 0.9, 1.3, 2.0, false, false)
		_profiles[ShotData.ShotType.SERVE_HIGH] = ShotData.ShotProfile.new(ShotData.ShotType.SERVE_HIGH, 1.20, 7.0, 0.9, 1.3, 3.0, false, false)
		_profiles[ShotData.ShotType.SERVE_FLICK] = ShotData.ShotProfile.new(ShotData.ShotType.SERVE_FLICK, 0.90, 4.5, 0.9, 1.3, 3.0, false, true)

	static func get_profile(shot_type: int) -> ShotData.ShotProfile:
		_ensure_profiles()
		return _profiles.get(shot_type, null)

	static func is_overhead(t: int) -> bool:
		return get_profile(t).overhead

	static func is_attacking(t: int) -> bool:
		return get_profile(t).attacking

	static func is_net_shot(t: int) -> bool:
		return t == ShotData.ShotType.NET_SHOT or t == ShotData.ShotType.NET_KILL or t == ShotData.ShotType.PUSH

	static func is_serve(t: int) -> bool:
		return t == ShotData.ShotType.SERVE_LOW or t == ShotData.ShotType.SERVE_HIGH or t == ShotData.ShotType.SERVE_FLICK

	## Can this shot be attempted from the shuttle's current height?
	static func can_attempt(t: int, shuttle_height: float) -> bool:
		var p := get_profile(t)
		return shuttle_height >= p.min_shuttle_height and shuttle_height <= p.max_shuttle_height
