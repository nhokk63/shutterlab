class_name GameSettings
extends RefCounted
## GameSettings — port of the settings half of Unity GameStateManager
## (player name, difficulty, volumes, camera preset, left-hand mode)
## plus GraphicsQuality (tier / FPS / render scale / MSAA / shadows).
## Persisted with ConfigFile at user://settings.cfg (Unity: PlayerPrefs
## "shuttlelab_*"). No autoload: whoever builds the UI creates one
## instance and passes it to MainMenu / SettingsPanel / AudioManager.

const SAVE_PATH := "user://settings.cfg"

const DIFFICULTY_NAMES: Array[String] = [
	"Beginner", "Amateur", "Intermediate", "Advanced", "Pro", "Legend",
]
const CAMERA_PRESET_NAMES: Array[String] = [
	"Sau lưng", "Trên cao", "Truyền hình", "Theo cầu", "Bên hông", "Chiến thuật",
]
const TIER_NAMES: Array[String] = ["Tiết kiệm", "Cân bằng", "Cao", "Tối đa"]
const FPS_OPTIONS: Array[int] = [30, 60, 90, 120]

# Tier -> default fps / render scale / MSAA / shadow distance.
const TIER_FPS: Array[int] = [30, 60, 60, 120]
const TIER_SCALE: Array[float] = [0.6, 0.75, 0.85, 1.0]
const TIER_MSAA: Array[int] = [1, 2, 2, 4]
const TIER_SHADOW_DIST: Array[float] = [0.0, 25.0, 30.0, 30.0]

var player_name: String = "Bạn"
var difficulty: int = 2
var camera_preset: int = 0
var music_volume: float = 0.8
var sfx_volume: float = 0.8
var left_hand_mode: bool = false

var gfx_tier: int = -1  # -1 = never saved: auto-detect on first apply
var gfx_fps: int = 60
var gfx_scale: float = 1.0
var gfx_msaa: int = 4
var gfx_shadows: bool = true
var gfx_bloom: bool = false


static func difficulty_name(level: int) -> String:
	return DIFFICULTY_NAMES[clampi(level, 0, 5)]


func load() -> void:
	var cfg := ConfigFile.new()
	if cfg.load(SAVE_PATH) != OK:
		return
	player_name = str(cfg.get_value("player", "name", "Bạn"))
	difficulty = clampi(int(cfg.get_value("player", "difficulty", 2)), 0, 5)
	camera_preset = clampi(int(cfg.get_value("player", "camera_preset", 0)), 0, 5)
	music_volume = clampf(float(cfg.get_value("audio", "music_volume", 0.8)), 0.0, 1.0)
	sfx_volume = clampf(float(cfg.get_value("audio", "sfx_volume", 0.8)), 0.0, 1.0)
	left_hand_mode = bool(cfg.get_value("input", "left_hand", false))
	gfx_tier = clampi(int(cfg.get_value("graphics", "tier", -1)), -1, 3)
	gfx_fps = clampi(int(cfg.get_value("graphics", "fps", 60)), 30, 120)
	gfx_scale = clampf(float(cfg.get_value("graphics", "scale", 1.0)), 0.5, 1.0)
	gfx_msaa = int(cfg.get_value("graphics", "msaa", 4))
	gfx_shadows = bool(cfg.get_value("graphics", "shadows", true))
	gfx_bloom = bool(cfg.get_value("graphics", "bloom", false))


func save() -> void:
	var cfg := ConfigFile.new()
	cfg.set_value("player", "name", player_name)
	cfg.set_value("player", "difficulty", difficulty)
	cfg.set_value("player", "camera_preset", camera_preset)
	cfg.set_value("audio", "music_volume", music_volume)
	cfg.set_value("audio", "sfx_volume", sfx_volume)
	cfg.set_value("input", "left_hand", left_hand_mode)
	cfg.set_value("graphics", "tier", gfx_tier)
	cfg.set_value("graphics", "fps", gfx_fps)
	cfg.set_value("graphics", "scale", gfx_scale)
	cfg.set_value("graphics", "msaa", gfx_msaa)
	cfg.set_value("graphics", "shadows", gfx_shadows)
	cfg.set_value("graphics", "bloom", gfx_bloom)
	cfg.save(SAVE_PATH)


## Guess a tier from the hardware (Unity GraphicsQuality.AutoDetect).
## Flagship GPU + 8 GB RAM + 8 cores -> Ultra; 6 GB -> High; 4 GB ->
## Balanced; otherwise Low. Unknown hardware -> Balanced.
static func auto_detect_tier() -> int:
	var cores := OS.get_processor_count()
	var flagship := false
	var gpu := RenderingServer.get_video_adapter_name().to_lower()
	for token in ["adreno 8", "adreno 7", "mali-g7", "mali-g8", "apple a1", "apple m"]:
		if token in gpu:
			flagship = true
			break
	var ram_mb := 0
	if OS.has_method("get_memory_info"):
		var info: Dictionary = OS.get_memory_info()
		if info.has("physical"):
			ram_mb = int(info["physical"]) / (1024 * 1024)
	if ram_mb >= 8000 and cores >= 8 and flagship:
		return 3
	if ram_mb >= 6000:
		return 2
	if ram_mb >= 4000:
		return 1
	if ram_mb > 0:
		return 0
	return 1


func apply_tier(tier: int) -> void:
	gfx_tier = clampi(tier, 0, 3)
	gfx_fps = TIER_FPS[gfx_tier]
	gfx_scale = TIER_SCALE[gfx_tier]
	gfx_msaa = TIER_MSAA[gfx_tier]
	gfx_shadows = gfx_tier != 0
	gfx_bloom = gfx_tier == 3
	save()
	apply()


func apply_fps(fps: int) -> void:
	gfx_fps = clampi(fps, 30, 120)
	save()
	Engine.max_fps = gfx_fps


func apply_scale(scale: float) -> void:
	gfx_scale = clampf(scale, 0.5, 1.0)
	save()
	_apply_render()


## Apply the saved settings at startup. First launch auto-detects.
func apply_saved() -> void:
	if gfx_tier < 0:
		apply_tier(auto_detect_tier())
		return
	Engine.max_fps = gfx_fps
	_apply_render()


func apply() -> void:
	Engine.max_fps = gfx_fps
	_apply_render()


func _apply_render() -> void:
	# Viewport render scale + MSAA are per-viewport in Godot (Unity: URP
	# renderScale / msaaSampleCount). The main viewport is the root.
	var vp := Engine.get_main_loop() as SceneTree
	if vp == null:
		return
	var root: Viewport = vp.root
	if root == null:
		return
	root.scaling_3d_scale = gfx_scale
	root.msaa_3d = Viewport.MSAA_4X if gfx_msaa >= 4 else (
		Viewport.MSAA_2X if gfx_msaa >= 2 else Viewport.MSAA_DISABLED)
	# Shadows: Godot has no global shadow-distance switch; the arena
	# subagent reads gfx_shadows / tier to toggle its lights instead.
