class_name GraphicsQuality
extends RefCounted
## Graphics settings — Godot port of Systems/GraphicsQuality.cs
## (spec: GRAPHICS_SETTINGS_SPEC.md, user request 2026-11-10 07:03).
##
## Presets (FPS / render scale / MSAA / shadows / bloom / crowd):
##   Tiết kiệm  30 fps, scale 0.60, MSAA off, no shadows, no bloom, crowd 0
##   Cân bằng   60 fps, scale 0.75, MSAA 2x,  shadows 25 m, no bloom, crowd 800
##   Cao        60 fps, scale 0.85, MSAA 2x,  shadows 30 m, bloom (light), crowd 1200
##   Tối đa    120 fps, scale 1.00, MSAA 4x,  shadows 30 m, bloom, crowd 1500
## FPS and render scale can also be changed independently of the preset.
##
## Godot mapping (Unity -> Godot):
##   - URP renderScale      -> Viewport.scaling_3d_scale (FSR 1.0 when
##     < 1.0 on Forward+/Mobile, bilinear on the Compatibility renderer)
##   - URP msaaSampleCount  -> Viewport.msaa_3d
##   - targetFrameRate 120  -> Engine.max_fps = 120 + vsync disabled
##     (vSyncCount = 0 in Unity; on a 165 Hz screen vsync would lock to
##     165 Hz instead of 120)
##   - shadowDistance       -> stored; Godot spot shadows render within
##     the light's range, so there is no separate distance to set
##   - Bloom flag           -> ArenaBuilder environment glow (v0.2
##     post-processing); stored even without an arena, same as Unity
##     v0.1.0 where the flag was stored before the Volume existed
##   - PlayerPrefs keys     -> ConfigFile user://settings.cfg, section
##     "graphics", same key names (shuttlelab_gfx_*) as Unity
##
## Usage (from the bootstrap, once the main scene exists):
##     GraphicsQuality.apply_saved(get_viewport(), arena)
## `arena` may be null (settings still apply to the viewport/engine).

enum Tier { LOW, BALANCED, HIGH, ULTRA }

const TIER_NAMES := ["Tiết kiệm", "Cân bằng", "Cao", "Tối đa"]
const FPS_OPTIONS := [30, 60, 90, 120]
const SETTINGS_PATH := "user://settings.cfg"
const SECTION := "graphics"
const KEY_TIER := "shuttlelab_gfx_tier"
const KEY_FPS := "shuttlelab_gfx_fps"
const KEY_SCALE := "shuttlelab_gfx_scale"
const KEY_MSAA := "shuttlelab_gfx_msaa"
const KEY_SHADOWS := "shuttlelab_gfx_shadows"
const KEY_SHADOW_DISTANCE := "shuttlelab_gfx_shadowdist"
const KEY_BLOOM := "shuttlelab_gfx_bloom"

static func tier_fps(tier: int) -> int:
	match tier:
		Tier.LOW:
			return 30
		Tier.ULTRA:
			return 120
		_:
			return 60

static func tier_scale(tier: int) -> float:
	match tier:
		Tier.LOW:
			return 0.6
		Tier.BALANCED:
			return 0.75
		Tier.HIGH:
			return 0.85
		_:
			return 1.0

static func tier_msaa(tier: int) -> int:
	match tier:
		Tier.LOW:
			return 1
		Tier.ULTRA:
			return 4
		_:
			return 2

static func tier_shadow_distance(tier: int) -> float:
	match tier:
		Tier.LOW:
			return 0.0
		Tier.BALANCED:
			return 25.0
		_:
			return 30.0

## Crowd size per tier (spec table). Low turns the crowd off entirely.
static func tier_crowd_count(tier: int) -> int:
	match tier:
		Tier.LOW:
			return 0
		Tier.BALANCED:
			return 800
		Tier.HIGH:
			return 1200
		_:
			return 1500

static func shadows_on_for_tier(tier: int) -> bool:
	return tier != Tier.LOW

static func bloom_on_for_tier(tier: int) -> bool:
	return tier == Tier.ULTRA

## Hardware auto-detect (RAM + CPU cores + GPU family), same rules as Unity.
static func auto_detect() -> int:
	var mem := OS.get_memory_info()
	var ram_mb := int(mem.get("physical", 0)) / 1048576
	var cores := OS.get_processor_count()
	var gpu := RenderingServer.get_video_adapter_name().to_lower()
	var flagship_gpu := gpu.contains("adreno 8") or gpu.contains("adreno 7") \
		or gpu.contains("mali-g7") or gpu.contains("mali-g8") \
		or gpu.contains("apple a1") or gpu.contains("apple m")
	if ram_mb >= 8000 and cores >= 8 and flagship_gpu:
		return Tier.ULTRA
	if ram_mb >= 6000:
		return Tier.HIGH
	if ram_mb >= 4000:
		return Tier.BALANCED
	return Tier.LOW

## Human-readable auto-detect reason for the settings UI
## (spec: show the preset in use + why, e.g. "Tự động: máy bạn → Tối đa").
static func auto_detect_reason() -> String:
	var tier := auto_detect()
	var mem := OS.get_memory_info()
	var ram_mb := int(mem.get("physical", 0)) / 1048576
	var gpu := RenderingServer.get_video_adapter_name()
	return "Tự động: %d GB RAM, %d nhân, %s → %s" % [
		ram_mb / 1024, OS.get_processor_count(), gpu, TIER_NAMES[tier]]

static func current_tier() -> int:
	var cfg := _load()
	return clampi(int(cfg.get_value(SECTION, KEY_TIER, auto_detect())), 0, 3)

static func current_fps() -> int:
	var cfg := _load()
	return int(cfg.get_value(SECTION, KEY_FPS, tier_fps(current_tier())))

static func current_scale() -> float:
	var cfg := _load()
	return float(cfg.get_value(SECTION, KEY_SCALE, tier_scale(current_tier())))

static func current_msaa() -> int:
	var cfg := _load()
	return int(cfg.get_value(SECTION, KEY_MSAA, tier_msaa(current_tier())))

static func shadows_on() -> bool:
	var cfg := _load()
	return int(cfg.get_value(SECTION, KEY_SHADOWS, 1 if current_tier() != Tier.LOW else 0)) != 0

static func bloom_on() -> bool:
	var cfg := _load()
	return int(cfg.get_value(SECTION, KEY_BLOOM, 1 if current_tier() == Tier.ULTRA else 0)) != 0

## Apply a whole preset: FPS + scale + MSAA + shadows + bloom, saved at once.
static func apply(viewport: Viewport, tier: int, arena: ArenaBuilder = null) -> void:
	var cfg := _load()
	cfg.set_value(SECTION, KEY_TIER, tier)
	cfg.set_value(SECTION, KEY_FPS, tier_fps(tier))
	cfg.set_value(SECTION, KEY_SCALE, tier_scale(tier))
	cfg.set_value(SECTION, KEY_MSAA, tier_msaa(tier))
	cfg.set_value(SECTION, KEY_SHADOWS, 0 if tier == Tier.LOW else 1)
	cfg.set_value(SECTION, KEY_BLOOM, 1 if tier == Tier.ULTRA else 0)
	cfg.set_value(SECTION, KEY_SHADOW_DISTANCE, tier_shadow_distance(tier))
	_save(cfg)
	apply_fps(tier_fps(tier))
	_apply_viewport(viewport, cfg, arena)

static func apply_fps(fps: int) -> void:
	fps = clampi(fps, 30, 120)
	var cfg := _load()
	cfg.set_value(SECTION, KEY_FPS, fps)
	_save(cfg)
	Engine.max_fps = fps
	# vSync off so max_fps locks to the number, not the screen refresh
	# (on a 165 Hz screen vsync would lock to 165 Hz, not 120).
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)

static func apply_scale(viewport: Viewport, scale: float, arena: ArenaBuilder = null) -> void:
	scale = clampf(scale, 0.5, 1.0)
	var cfg := _load()
	cfg.set_value(SECTION, KEY_SCALE, scale)
	_save(cfg)
	_apply_viewport(viewport, cfg, arena)

## Apply saved settings at startup. First launch (no key yet) auto-detects.
static func apply_saved(viewport: Viewport, arena: ArenaBuilder = null) -> void:
	var cfg := _load()
	if not cfg.has_section_key(SECTION, KEY_TIER):
		apply(viewport, auto_detect(), arena)
		return
	apply_fps(int(cfg.get_value(SECTION, KEY_FPS, 60)))
	_apply_viewport(viewport, cfg, arena)

static func _apply_viewport(viewport: Viewport, cfg: ConfigFile, arena: ArenaBuilder) -> void:
	# Arena settings do not depend on the viewport, so apply them first
	# (a null viewport must not silently skip the shadow/glow toggles).
	if arena != null:
		arena.set_shadows_enabled(int(cfg.get_value(SECTION, KEY_SHADOWS, 1)) != 0)
		arena.set_glow_enabled(int(cfg.get_value(SECTION, KEY_BLOOM, 0)) != 0)
	if viewport == null:
		return
	var scale := clampf(float(cfg.get_value(SECTION, KEY_SCALE, 1.0)), 0.5, 1.0)
	viewport.scaling_3d_scale = scale
	# FSR (1.0) upscale when rendering below native; the Compatibility
	# renderer has no FSR path, so it stays bilinear there.
	if scale < 1.0 and RenderingServer.get_current_rendering_method() != "gl_compatibility":
		viewport.scaling_3d_mode = Viewport.SCALING_3D_MODE_FSR
	else:
		viewport.scaling_3d_mode = Viewport.SCALING_3D_MODE_BILINEAR
	match int(cfg.get_value(SECTION, KEY_MSAA, 4)):
		1:
			viewport.msaa_3d = Viewport.MSAA_DISABLED
		2:
			viewport.msaa_3d = Viewport.MSAA_2X
		4:
			viewport.msaa_3d = Viewport.MSAA_4X
		_:
			viewport.msaa_3d = Viewport.MSAA_8X

static func _load() -> ConfigFile:
	var cfg := ConfigFile.new()
	cfg.load(SETTINGS_PATH)
	return cfg

static func _save(cfg: ConfigFile) -> void:
	cfg.save(SETTINGS_PATH)
