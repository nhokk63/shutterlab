class_name GameState
extends RefCounted
## The app's flow state + persistent settings (difficulty, player
## name, camera preset, volumes, left-hand mode). Settings persist
## through a ConfigFile at user://shuttlelab_settings.cfg.
## Port of ShuttleLab.Core.GameStateManager (Unity C#), minus scene
## loading: scene transitions are the parent scene's job in Godot
## (this class only holds the mode + settings; GameBootstrap emits
## the requests). The graphics-quality settings live in the Systems
## port (GraphicsQuality) and are not duplicated here.

const SETTINGS_PATH := "user://shuttlelab_settings.cfg"

enum AppMode { MENU, QUICK_MATCH, PRACTICE, CAREER, TOURNAMENT }

var current_mode: int = AppMode.MENU

## AI difficulty 0..5 (default 2 = Intermediate).
var difficulty: int = 2
var player_name: String = "Bạn"

## Camera preset 0..5 (see CameraManager.CamPreset).
var camera_preset: int = 0

var music_volume: float = 0.8
var sfx_volume: float = 0.8
var left_hand_mode: bool = false

func set_difficulty(level: int) -> void:
	difficulty = clampi(level, 0, 5)
	save_settings()

func set_player_name(p_name: String) -> void:
	player_name = p_name if p_name != "" else "Bạn"
	save_settings()

func set_camera_preset(preset: int) -> void:
	camera_preset = clampi(preset, 0, 5)
	save_settings()

func set_left_hand_mode(left: bool) -> void:
	left_hand_mode = left
	save_settings()

func load_settings() -> void:
	var cfg := ConfigFile.new()
	if cfg.load(SETTINGS_PATH) != OK:
		return
	difficulty = clampi(int(cfg.get_value("settings", "difficulty", 2)), 0, 5)
	player_name = String(cfg.get_value("settings", "player_name", "Bạn"))
	if player_name == "":
		player_name = "Bạn"
	camera_preset = clampi(int(cfg.get_value("settings", "camera_preset", 0)), 0, 5)
	music_volume = clampf(float(cfg.get_value("settings", "music_volume", 0.8)), 0.0, 1.0)
	sfx_volume = clampf(float(cfg.get_value("settings", "sfx_volume", 0.8)), 0.0, 1.0)
	left_hand_mode = bool(cfg.get_value("settings", "left_hand", false))

func save_settings() -> void:
	var cfg := ConfigFile.new()
	cfg.set_value("settings", "difficulty", difficulty)
	cfg.set_value("settings", "player_name", player_name)
	cfg.set_value("settings", "camera_preset", camera_preset)
	cfg.set_value("settings", "music_volume", music_volume)
	cfg.set_value("settings", "sfx_volume", sfx_volume)
	cfg.set_value("settings", "left_hand", left_hand_mode)
	cfg.save(SETTINGS_PATH)
