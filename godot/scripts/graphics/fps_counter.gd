class_name FpsCounter
extends Node
## Real FPS meter (1 s rolling average, like the Unity SettingsPanel
## clock). Add it once to the scene; read `fps` or listen to
## `fps_updated`. The settings panel UI (UI subagent) displays it.

signal fps_updated(fps: float)

var fps: float = 0.0

var _frames := 0
var _elapsed := 0.0

func _process(delta: float) -> void:
	_frames += 1
	_elapsed += delta
	if _elapsed >= 1.0:
		fps = float(_frames) / _elapsed
		_frames = 0
		_elapsed = 0.0
		fps_updated.emit(fps)

func get_fps_text() -> String:
	return "%d FPS" % int(round(fps))
