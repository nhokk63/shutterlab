class_name PlayerAnimation
extends Node
## Procedural animation driver: clip table + pose interpolation on the
## PlayerVisual joints. Port of ShuttleLab.Players.PlayerAnimationController
## (Unity C#) — timings are exact (clip durations and contact times taken
## from AnimClipLibrary); the poses are simplified approximations of the
## Unity keyframes (same joints, same intent: windup / contact / follow),
## because the full 17-joint keyframe tables live in the Unity file.
## A one-shot clip fires on_contact exactly at ContactTime and on_end
## when the swing finishes, then returns to the Ready loop.
##
## Clip table (duration s / contact s):
##   Idle 2.00 loop | Ready 1.20 loop | Run 0.50 loop (procedural)
##   Smash 0.70/0.30 | Clear 0.75/0.32 | Drop 0.65/0.26
##   NetShot 0.50/0.18 | Serve 0.80/0.38 | Block 0.40/0.15
##   Celebrate 1.40 | Disappointed 1.40

const BLEND_DURATION := 0.12

var _visual: PlayerVisual
var _current: String = ""
var _clip_time: float = 0.0
var _speed: float = 1.0
var _on_contact: Callable
var _on_end: Callable
var _contact_fired: bool = false
var _run_phase: float = 0.0
var _run_speed: float = 1.0

func bind(visual: PlayerVisual) -> void:
	_visual = visual
	play_loop("Ready")

func current_clip_name() -> String:
	return _current

func is_playing_shot() -> bool:
	var c := _clip_def(_current)
	return c.get("contact", -1.0) >= 0.0 and _clip_time < float(c.get("duration", 0.0))

func get_clip_duration(clip_name: String) -> float:
	return float(_clip_def(clip_name).get("duration", 0.5))

## Play a one-shot clip. on_contact fires once at ContactTime (shot
## clips), on_end when the swing finishes.
func play_clip(clip_name: String, speed: float, on_contact: Callable, on_end: Callable) -> void:
	if _clip_def(clip_name).is_empty():
		if on_contact.is_valid():
			on_contact.call()
		if on_end.is_valid():
			on_end.call()
		return
	_current = clip_name
	_clip_time = 0.0
	_speed = speed if speed > 0.01 else 1.0
	_on_contact = on_contact
	_on_end = on_end
	_contact_fired = false

## Play a looping clip: "Idle", "Ready" or "Run".
func play_loop(clip_name: String) -> void:
	if _clip_def(clip_name).is_empty():
		return
	_current = clip_name
	_clip_time = 0.0
	_speed = 1.0
	_on_contact = Callable()
	_on_end = Callable()
	_contact_fired = true

func set_run_speed(f: float) -> void:
	_run_speed = clampf(f, 0.0, 1.5)

func _process(delta: float) -> void:
	if _visual == null or _current == "":
		return
	_clip_time += delta * _speed
	var def := _clip_def(_current)
	var duration := float(def.get("duration", 0.5))
	var contact := float(def.get("contact", -1.0))
	var loop: bool = def.get("loop", false)

	# Contact callback, exactly at ContactTime.
	if contact >= 0.0 and not _contact_fired and _clip_time >= contact:
		_contact_fired = true
		if _on_contact.is_valid():
			_on_contact.call()

	if loop and duration > 0.0:
		_clip_time = fmod(_clip_time, duration)
	elif not loop and _clip_time >= duration:
		_clip_time = duration
		var end_cb := _on_end
		_on_end = Callable()
		play_loop("Ready")
		if end_cb.is_valid():
			end_cb.call()

	_apply_pose()
	_update_locomotion()

## Tell the visual which locomotion clip to play (Idle / Walk / Run).
## The procedural rig ignores this (it animates through set_pose);
## the Ara rig switches the GLB's own clips so the legs move with the
## body instead of the character gliding across the court.
func _update_locomotion() -> void:
	if _visual == null:
		return
	var clip := "Idle"
	if _current == "Run":
		clip = "Walk" if _run_speed < 0.45 else "Run"
	_visual.set_locomotion(clip)

# ---------------------------------------------------------------- poses

func _apply_pose() -> void:
	if _current == "Run":
		_run_phase += 0.016 * _run_speed * 10.0
		_visual.set_pose(_run_pose(_run_phase))
		return
	var def := _clip_def(_current)
	var keys: Array = def.get("keys", [])
	if keys.is_empty():
		_visual.set_pose(_ready_pose())
		return
	var t := _clip_time
	var prev: Array = keys[0]
	var next: Array = keys[keys.size() - 1]
	for k in keys:
		if float(k[0]) <= t:
			prev = k
		if float(k[0]) >= t:
			next = k
			break
	var span := float(next[0]) - float(prev[0])
	var f := 0.0 if span <= 0.001 else clampf((t - float(prev[0])) / span, 0.0, 1.0)
	_visual.set_pose(_lerp_pose(prev[1], next[1], f))

## Procedural run: legs/arms swing in opposition, chest leans forward.
func _run_pose(phase: float) -> Dictionary:
	var p := _ready_pose()
	var s := sin(phase)
	var c := cos(phase)
	p["HipL"] = Vector3(deg_to_rad(-25 + s * 28), 0, 0)
	p["HipR"] = Vector3(deg_to_rad(-25 - s * 28), 0, 0)
	p["KneeL"] = Vector3(deg_to_rad(35 + maxf(0.0, -s) * 45), 0, 0)
	p["KneeR"] = Vector3(deg_to_rad(35 + maxf(0.0, s) * 45), 0, 0)
	p["ShoulderL"] = Vector3(deg_to_rad(-20 - s * 25), 0, deg_to_rad(20))
	p["ShoulderR"] = Vector3(deg_to_rad(-45 + s * 25), 0, deg_to_rad(-25))
	p["Chest"] = Vector3(deg_to_rad(16 + c * 2), 0, 0)
	return p

func _lerp_pose(a: Dictionary, b: Dictionary, f: float) -> Dictionary:
	var out := {}
	var keys := {}
	for k in a:
		keys[k] = true
	for k in b:
		keys[k] = true
	for k in keys:
		var va: Vector3 = a.get(k, Vector3.ZERO)
		var vb: Vector3 = b.get(k, Vector3.ZERO)
		out[k] = va.lerp(vb, f)
	return out

# --- Pose library (degrees -> radians at build time) -------------------

func _ready_pose() -> Dictionary:
	return _pose({})

## Ready pose in DEGREES (single source; _pose converts to radians).
func _ready_pose_deg() -> Dictionary:
	return {
		"Chest": Vector3(12, 0, 0),
		"HipL": Vector3(-25, 0, 0), "HipR": Vector3(-25, 0, 0),
		"KneeL": Vector3(35, 0, 0), "KneeR": Vector3(35, 0, 0),
		"AnkleL": Vector3(-10, 0, 0), "AnkleR": Vector3(-10, 0, 0),
		"ShoulderR": Vector3(-45, 0, -25), "ElbowR": Vector3(-55, 0, 0),
		"ShoulderL": Vector3(-20, 0, 20), "ElbowL": Vector3(-40, 0, 0),
	}

func _pose(overrides_deg: Dictionary) -> Dictionary:
	var out := {}
	for k in _ready_pose_deg():
		out[k] = _ready_pose_deg()[k]
	for k in overrides_deg:
		out[k] = overrides_deg[k]
	# Convert degrees -> radians.
	for k in out:
		var v: Vector3 = out[k]
		out[k] = Vector3(deg_to_rad(v.x), deg_to_rad(v.y), deg_to_rad(v.z))
	return out

## Clip definitions: duration / contact time / loop / keyframes
## (each keyframe is [time, pose]). Timings match Unity's AnimClipLibrary.
func _clip_def(clip_name: String) -> Dictionary:
	match clip_name:
		"Idle":
			return {"duration": 2.0, "contact": -1.0, "loop": true, "keys": [
				[0.0, _pose({"Chest": Vector3(2, 0, 0), "ShoulderL": Vector3(0, 0, 8), "ShoulderR": Vector3(0, 0, -8), "ElbowL": Vector3(-15, 0, 0), "ElbowR": Vector3(-15, 0, 0)})],
				[1.0, _pose({"Chest": Vector3(4, 0, 0), "Spine": Vector3(1, 0, 0), "Head": Vector3(-2, 0, 0), "ShoulderL": Vector3(0, 0, 9), "ShoulderR": Vector3(0, 0, -9), "ElbowL": Vector3(-16, 0, 0), "ElbowR": Vector3(-16, 0, 0)})],
				[2.0, _pose({"Chest": Vector3(2, 0, 0), "ShoulderL": Vector3(0, 0, 8), "ShoulderR": Vector3(0, 0, -8), "ElbowL": Vector3(-15, 0, 0), "ElbowR": Vector3(-15, 0, 0)})],
			]}
		"Ready":
			return {"duration": 1.2, "contact": -1.0, "loop": true, "keys": [
				[0.0, _ready_pose()],
				[0.6, _pose({"Chest": Vector3(13, 0, 0), "HipL": Vector3(-27, 0, 0), "HipR": Vector3(-27, 0, 0), "KneeL": Vector3(38, 0, 0), "KneeR": Vector3(38, 0, 0), "ShoulderR": Vector3(-47, 0, -25), "ElbowR": Vector3(-56, 0, 0), "ShoulderL": Vector3(-22, 0, 20), "ElbowL": Vector3(-42, 0, 0)})],
				[1.2, _ready_pose()],
			]}
		"Run":
			return {"duration": 0.5, "contact": -1.0, "loop": true, "keys": []}
		"Smash":
			return {"duration": 0.70, "contact": 0.30, "loop": false, "keys": [
				[0.0, _ready_pose()],
				[0.18, _pose({"Chest": Vector3(-10, 0, -15), "ShoulderR": Vector3(-120, 0, -40), "ElbowR": Vector3(-20, 0, 0), "HipR": Vector3(-40, 0, 0), "KneeR": Vector3(50, 0, 0)})],
				[0.30, _pose({"Chest": Vector3(15, 0, 10), "ShoulderR": Vector3(-170, 0, -10), "ElbowR": Vector3(-5, 0, 0), "HipR": Vector3(-55, 0, 0), "KneeR": Vector3(60, 0, 0)})],
				[0.50, _pose({"Chest": Vector3(20, 0, 20), "ShoulderR": Vector3(-60, 0, 40), "ElbowR": Vector3(-30, 0, 0)})],
				[0.70, _ready_pose()],
			]}
		"Clear":
			return {"duration": 0.75, "contact": 0.32, "loop": false, "keys": [
				[0.0, _ready_pose()],
				[0.20, _pose({"Chest": Vector3(-8, 0, -12), "ShoulderR": Vector3(-110, 0, -35), "ElbowR": Vector3(-25, 0, 0), "HipR": Vector3(-38, 0, 0), "KneeR": Vector3(48, 0, 0)})],
				[0.32, _pose({"Chest": Vector3(12, 0, 8), "ShoulderR": Vector3(-160, 0, -12), "ElbowR": Vector3(-8, 0, 0), "HipR": Vector3(-50, 0, 0), "KneeR": Vector3(55, 0, 0)})],
				[0.55, _pose({"Chest": Vector3(18, 0, 15), "ShoulderR": Vector3(-70, 0, 30), "ElbowR": Vector3(-30, 0, 0)})],
				[0.75, _ready_pose()],
			]}
		"Drop":
			return {"duration": 0.65, "contact": 0.26, "loop": false, "keys": [
				[0.0, _ready_pose()],
				[0.16, _pose({"Chest": Vector3(-5, 0, -8), "ShoulderR": Vector3(-100, 0, -25), "ElbowR": Vector3(-35, 0, 0)})],
				[0.26, _pose({"Chest": Vector3(8, 0, 5), "ShoulderR": Vector3(-130, 0, -15), "ElbowR": Vector3(-15, 0, 0)})],
				[0.45, _pose({"Chest": Vector3(12, 0, 10), "ShoulderR": Vector3(-70, 0, 20), "ElbowR": Vector3(-35, 0, 0)})],
				[0.65, _ready_pose()],
			]}
		"NetShot":
			return {"duration": 0.50, "contact": 0.18, "loop": false, "keys": [
				[0.0, _ready_pose()],
				[0.10, _pose({"Chest": Vector3(22, 0, 0), "ShoulderR": Vector3(-35, 0, -10), "ElbowR": Vector3(-25, 0, 0), "HipL": Vector3(-45, 0, 0), "KneeL": Vector3(60, 0, 0)})],
				[0.18, _pose({"Chest": Vector3(28, 0, 0), "ShoulderR": Vector3(-50, 0, -5), "ElbowR": Vector3(-15, 0, 0), "HipL": Vector3(-50, 0, 0), "KneeL": Vector3(65, 0, 0)})],
				[0.35, _pose({"Chest": Vector3(22, 0, 0), "ShoulderR": Vector3(-35, 0, -10), "ElbowR": Vector3(-25, 0, 0), "HipL": Vector3(-45, 0, 0), "KneeL": Vector3(60, 0, 0)})],
				[0.50, _ready_pose()],
			]}
		"Serve":
			return {"duration": 0.80, "contact": 0.38, "loop": false, "keys": [
				[0.0, _ready_pose()],
				[0.22, _pose({"ShoulderR": Vector3(-20, 0, -15), "ElbowR": Vector3(-70, 0, 0)})],
				[0.38, _pose({"ShoulderR": Vector3(-70, 0, -10), "ElbowR": Vector3(-20, 0, 0)})],
				[0.55, _pose({"ShoulderR": Vector3(-50, 0, 0), "ElbowR": Vector3(-40, 0, 0)})],
				[0.80, _ready_pose()],
			]}
		"Block":
			return {"duration": 0.40, "contact": 0.15, "loop": false, "keys": [
				[0.0, _ready_pose()],
				[0.15, _pose({"ShoulderR": Vector3(-80, 0, 0), "ElbowR": Vector3(-10, 0, 0)})],
				[0.40, _ready_pose()],
			]}
		"Celebrate":
			return {"duration": 1.40, "contact": -1.0, "loop": false, "keys": [
				[0.0, _ready_pose()],
				[0.35, _pose({"Chest": Vector3(-8, 0, 0), "ShoulderL": Vector3(-160, 0, 20), "ShoulderR": Vector3(-160, 0, -20), "ElbowL": Vector3(-10, 0, 0), "ElbowR": Vector3(-10, 0, 0)})],
				[0.70, _pose({"Chest": Vector3(-8, 0, 0), "ShoulderL": Vector3(-160, 0, 20), "ShoulderR": Vector3(-160, 0, -20), "ElbowL": Vector3(-10, 0, 0), "ElbowR": Vector3(-10, 0, 0)})],
				[1.40, _ready_pose()],
			]}
		"Disappointed":
			return {"duration": 1.40, "contact": -1.0, "loop": false, "keys": [
				[0.0, _pose({"Chest": Vector3(25, 0, 0), "Head": Vector3(15, 0, 0), "ShoulderL": Vector3(-10, 0, 10), "ShoulderR": Vector3(-10, 0, -10)})],
				[0.70, _pose({"Chest": Vector3(30, 0, 0), "Head": Vector3(20, 0, 0), "ShoulderL": Vector3(-5, 0, 5), "ShoulderR": Vector3(-5, 0, -5)})],
				[1.40, _pose({"Chest": Vector3(25, 0, 0), "Head": Vector3(15, 0, 0), "ShoulderL": Vector3(-10, 0, 10), "ShoulderR": Vector3(-10, 0, -10)})],
			]}
		_:
			return {}
