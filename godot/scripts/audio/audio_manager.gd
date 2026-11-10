class_name AudioManager
extends Node
## AudioManager — port of Unity Systems/AudioManager.cs.
## ALL sounds are synthesised by code (no .wav/.mp3 assets), exactly
## like Unity: sine / seeded noise bursts + exponential envelopes,
## rendered once at startup into AudioStreamWAV clips (Unity:
## AudioClip.Create + SetData). A pool of 4 players lets sounds overlap
## (e.g. smash + bounce).
##
## HONEST ABOUT MUSIC (same as Unity v0.1.0):
##  - v0.1.0 has SFX only: racket hits per shot type, shuttle bounce,
##    referee whistle, UI clicks, score chime, crowd ambience + cheer.
##  - NO background music track. music_volume is stored and will drive
##    music when a track exists in a later version; the slider currently
##    plays nothing, and SettingsPanel says so.
##  - SFX volume is read on every play, so moving the slider is heard
##    immediately without a restart.
##
## Wiring: add as a child of the main scene (or any persistent node) and
## call setup(settings). UI buttons call AudioManager.instance.play_click().

const SAMPLE_RATE := 44100
const POOL_SIZE := 4

static var instance: AudioManager = null

var music_volume: float = 0.8
var sfx_volume: float = 0.8

var _sources: Array[AudioStreamPlayer] = []
var _next_source: int = 0
var _crowd_player: AudioStreamPlayer = null
var _crowd_level: float = 0.5

var _hit_smash: AudioStreamWAV
var _hit_clear: AudioStreamWAV
var _hit_soft: AudioStreamWAV
var _hit_serve: AudioStreamWAV
var _bounce: AudioStreamWAV
var _whistle: AudioStreamWAV
var _click: AudioStreamWAV
var _score: AudioStreamWAV
var _cheer: AudioStreamWAV


func _ready() -> void:
	if instance != null and instance != self:
		queue_free()
		return
	instance = self
	for i in POOL_SIZE:
		var p := AudioStreamPlayer.new()
		add_child(p)
		_sources.append(p)
	_crowd_player = AudioStreamPlayer.new()
	add_child(_crowd_player)
	_build_clips()


func _exit_tree() -> void:
	if instance == self:
		instance = null


func setup(settings: GameSettings) -> void:
	if settings == null:
		return
	music_volume = settings.music_volume
	sfx_volume = settings.sfx_volume


# --- Public API -------------------------------------------------------------

## Racket sound by shot type (hard/soft differ), mirroring Unity PlayHit.
func play_hit(shot_type: int) -> void:
	match shot_type:
		ShotData.ShotType.SMASH, ShotData.ShotType.JUMP_SMASH, ShotData.ShotType.HALF_SMASH:
			_play(_hit_smash, 1.0)
		ShotData.ShotType.SERVE_LOW, ShotData.ShotType.SERVE_HIGH, ShotData.ShotType.SERVE_FLICK:
			_play(_hit_serve, 0.8)
		ShotData.ShotType.DROP, ShotData.ShotType.NET_SHOT, ShotData.ShotType.NET_KILL, \
		ShotData.ShotType.PUSH, ShotData.ShotType.BLOCK:
			_play(_hit_soft, 0.7)
		_:
			_play(_hit_clear, 0.85)


## Shuttle hitting the floor (small, low thud).
func play_bounce() -> void:
	_play(_bounce, 0.5)


## Referee whistle: two blasts (short + long).
func play_whistle() -> void:
	_play(_whistle, 0.9)


## UI click.
func play_click() -> void:
	_play(_click, 0.6)


## Chime when a point is scored (two rising notes).
func play_score() -> void:
	_play(_score, 0.8)


## Crowd cheer burst (e.g. after a smash winner / game point).
func play_cheer() -> void:
	_play(_cheer, 0.9)


## Start the looping crowd ambience (arena background noise).
func play_crowd() -> void:
	if _crowd_player == null:
		return
	if _crowd_player.playing:
		return
	_crowd_player.stream = _crowd_stream
	_crowd_player.volume_db = linear_to_db(maxf(0.001, sfx_volume * _crowd_level * 0.5))
	_crowd_player.play()


func stop_crowd() -> void:
	if _crowd_player == null:
		return
	_crowd_player.stop()


## Crowd excitement 0..1 (rises after big points, falls in quiet rallies).
func set_crowd_level(level: float) -> void:
	_crowd_level = clampf(level, 0.0, 1.0)
	if _crowd_player != null and _crowd_player.playing:
		_crowd_player.volume_db = linear_to_db(maxf(0.001, sfx_volume * _crowd_level * 0.5))


# --- Playback ----------------------------------------------------------------

func _play(stream: AudioStreamWAV, volume_scale: float) -> void:
	if stream == null or _sources.is_empty():
		return
	var vol := sfx_volume * clampf(volume_scale, 0.0, 1.0)
	if vol <= 0.001:
		return  # muted: don't play
	var src := _sources[_next_source]
	_next_source = (_next_source + 1) % _sources.size()
	src.stream = stream
	src.volume_db = linear_to_db(vol)
	src.play()


# --- Clip synthesis ----------------------------------------------------------

var _crowd_stream: AudioStreamWAV


func _build_clips() -> void:
	# Smash: strong noise burst + low 110 Hz thump (0.16 s).
	_hit_smash = _synth(0.16, func(t: float) -> float:
		return _noise_burst(t, 0.10, 0.9, 11) + _sine(t, 110.0) * _exp_decay(t, 28.0) * 0.7)
	# Clear / Drive: medium noise + 260 Hz sine (0.12 s).
	_hit_clear = _synth(0.12, func(t: float) -> float:
		return _noise_burst(t, 0.07, 0.55, 23) + _sine(t, 260.0) * _exp_decay(t, 40.0) * 0.5)
	# Drop / Net: short soft tick (0.07 s).
	_hit_soft = _synth(0.07, func(t: float) -> float:
		return _noise_burst(t, 0.03, 0.4, 37) + _sine(t, 520.0) * _exp_decay(t, 70.0) * 0.35)
	# Serve: pop — sweep 350 Hz -> 180 Hz + short noise (0.10 s).
	_hit_serve = _synth(0.10, func(t: float) -> float:
		var freq := lerpf(350.0, 180.0, t / 0.10)
		return _sine(t, freq) * _exp_decay(t, 45.0) * 0.6 + _noise_burst(t, 0.04, 0.4, 51))
	# Bounce: low 90 Hz thud (0.09 s).
	_bounce = _synth(0.09, func(t: float) -> float:
		return _sine(t, 90.0) * _exp_decay(t, 55.0) * 0.6)
	# Whistle: 2 blasts of 2300 Hz sine with a slight wobble (0.65 s).
	_whistle = _synth(0.65, func(t: float) -> float:
		var on := t < 0.18 or (t > 0.26 and t < 0.61)
		if not on:
			return 0.0
		var vib := 1.0 + 0.04 * sin(TAU * 30.0 * t)
		return _sine(t, 2300.0 * vib) * 0.5 + _noise_burst(t, 0.65, 0.08, 71))
	# Click: 1200 Hz blip (0.045 s).
	_click = _synth(0.045, func(t: float) -> float:
		return _sine(t, 1200.0) * _exp_decay(t, 120.0) * 0.6)
	# Score: 880 Hz (0.12 s) then 1318 Hz (0.28 s).
	_score = _synth(0.40, func(t: float) -> float:
		if t < 0.12:
			return _sine(t, 880.0) * _exp_decay(t, 25.0) * 0.5
		return _sine(t - 0.12, 1318.0) * _exp_decay(t - 0.12, 14.0) * 0.55)
	# Cheer: crowd noise swelling up and fading (1.5 s).
	_cheer = _synth(1.5, func(t: float) -> float:
		var env := 0.0
		if t < 0.3:
			env = t / 0.3
		else:
			env = 1.0 - (t - 0.3) / 1.2
		return _noise_burst(t, 1.5, 0.8, 97) * env)
	_crowd_stream = _synth_crowd_loop(3.0)


## Render a mono clip from a sample function fn(t), t in seconds.
func _synth(duration: float, fn: Callable) -> AudioStreamWAV:
	var count := maxi(1, int(SAMPLE_RATE * duration))
	var data := PackedByteArray()
	data.resize(count * 2)
	for i in count:
		var t := float(i) / SAMPLE_RATE
		var v := clampf(float(fn.call(t)), -1.0, 1.0)
		var iv := int(v * 32767.0)
		data[i * 2] = iv & 0xFF
		data[i * 2 + 1] = (iv >> 8) & 0xFF
	var wav := AudioStreamWAV.new()
	wav.format = AudioStreamWAV.FORMAT_16_BITS
	wav.mix_rate = SAMPLE_RATE
	wav.stereo = false
	wav.data = data
	return wav


## Looping crowd ambience: lowpassed noise with a slow swell, crossfaded
## at the loop point so it repeats seamlessly.
func _synth_crowd_loop(duration: float) -> AudioStreamWAV:
	var count := maxi(1, int(SAMPLE_RATE * duration))
	var raw := PackedFloat32Array()
	raw.resize(count)
	var lp1 := 0.0
	var lp2 := 0.0
	for i in count:
		var t := float(i) / SAMPLE_RATE
		var white := _hash_noise(i, 197) * 0.5
		lp1 += 0.06 * (white - lp1)
		lp2 += 0.06 * (lp1 - lp2)
		var swell := 0.7 + 0.3 * sin(TAU * t / duration)
		raw[i] = lp2 * 2.2 * swell * 0.5
	# Crossfade the last 0.2 s into the start so the loop has no click.
	var fade := int(SAMPLE_RATE * 0.2)
	for i in fade:
		var a := float(i) / fade
		var idx := count - fade + i
		raw[idx] = raw[idx] * (1.0 - a) + raw[i] * a
	var data := PackedByteArray()
	data.resize(count * 2)
	for i in count:
		var v := clampf(raw[i], -1.0, 1.0)
		var iv := int(v * 32767.0)
		data[i * 2] = iv & 0xFF
		data[i * 2 + 1] = (iv >> 8) & 0xFF
	var wav := AudioStreamWAV.new()
	wav.format = AudioStreamWAV.FORMAT_16_BITS
	wav.mix_rate = SAMPLE_RATE
	wav.stereo = false
	wav.data = data
	wav.loop_mode = AudioStreamWAV.LOOP_FORWARD
	wav.loop_begin = 0
	wav.loop_end = count
	return wav


# --- Basic wave blocks (mirrors Unity) --------------------------------------

func _sine(t: float, freq: float) -> float:
	return sin(TAU * freq * t)


func _exp_decay(t: float, rate: float) -> float:
	return exp(-t * rate)


## Deterministic seeded white noise, only inside the first `length`
## seconds, then silent. Same hash as Unity (same sound every play).
func _noise_burst(t: float, length: float, amp: float, seed: int) -> float:
	if t >= length:
		return 0.0
	var n := int(t * SAMPLE_RATE)
	var white := _hash_noise(n, seed)
	var env := 1.0 - t / length  # linear fade-out
	return white * amp * env


## Hash noise in [-1, 1] from (seed, sample index) — Unity's exact hash.
func _hash_noise(n: int, seed: int) -> float:
	var h: int = int(seed * 2654435761) ^ int((n + 1) * 40503)
	h = h & 0xFFFFFFFF
	h ^= h >> 13
	h = int(h * 1274126177) & 0xFFFFFFFF
	h ^= h >> 16
	h = h & 0xFFFFFFFF
	return float(h % 20001) / 10000.0 - 1.0
