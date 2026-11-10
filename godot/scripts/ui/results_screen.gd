extends Control
class_name ResultsScreen
## ResultsScreen — port of Unity UI/ResultsScreen.cs.
## End-of-match panel (child of the HUD canvas, hidden until shown).
## All displayed data comes 100% from show() parameters — nothing is
## invented. Stats arrive as a Dictionary so the screen does not depend
## on how the match subagent ports MatchStats:
##   { match_time: float (s), rallies: int, shots_hit: int,
##     shots_good: int, serves_good: int, smashes: int, errors: int,
##     longest_rally: int }
## coins_earned is the Quick Match / tournament reward for this match
## (Unity had no coins line; the full-port spec adds it).

signal rematch_requested
signal menu_requested

var _title_label: Label
var _score_label: Label
var _stats_label: Label
var _built := false


func _ready() -> void:
	_build()
	visible = false


## winner: 0 = user wins, 1 = AI wins.
## (Named show_results, not show, so it does not shadow CanvasItem.show().)
func show_results(winner: int, score: MatchRules.ScoreState, stats: Dictionary,
		player_name: String, ai_name: String, coins_earned: int = 0) -> void:
	_build()
	visible = true
	var p_name := player_name if player_name != "" else "Bạn"
	var a_name := ai_name if ai_name != "" else "Đối thủ"
	if _title_label != null:
		_title_label.text = "%s thắng trận!" % p_name if winner == 0 else "%s thắng trận!" % a_name
		_title_label.add_theme_color_override("font_color",
			UITheme.ACCENT_TEXT if winner == 0 else Color(1.0, 0.55, 0.45))
	if _score_label != null:
		if score != null:
			_score_label.text = "Tỉ số ván: %d-%d  •  Điểm ván cuối: %d-%d" % [
				score.games[0], score.games[1], score.points[0], score.points[1]]
		else:
			_score_label.text = ""
	if _stats_label != null:
		_stats_label.text = _format_stats(stats, coins_earned)


func hide_results() -> void:
	visible = false


func _format_stats(stats: Dictionary, coins_earned: int) -> String:
	if stats.is_empty():
		return "Không có thống kê trận đấu."
	var match_time := float(stats.get("match_time", 0.0))
	var minutes := int(match_time) / 60
	var seconds := int(match_time) % 60
	var shots_hit := int(stats.get("shots_hit", 0))
	var shots_good := int(stats.get("shots_good", 0))
	var accuracy := 0.0
	if shots_hit > 0:
		accuracy = float(shots_good) / float(shots_hit) * 100.0
	var lines := "Thời gian: %02d:%02d\n" % [minutes, seconds]
	lines += "Tổng cú đánh qua lại: %d\n" % int(stats.get("rallies", 0))
	lines += "Cú đánh đã thực hiện: %d\n" % shots_hit
	lines += "Cú đánh tốt: %d (%d%%)\n" % [shots_good, int(accuracy)]
	lines += "Giao cầu tốt: %d\n" % int(stats.get("serves_good", 0))
	lines += "Smash: %d\n" % int(stats.get("smashes", 0))
	lines += "Lỗi: %d\n" % int(stats.get("errors", 0))
	lines += "Rally dài nhất: %d cú đánh" % int(stats.get("longest_rally", 0))
	if coins_earned != 0:
		lines += "\nXu nhận được: %+d xu" % coins_earned
	return lines


func _on_rematch_button() -> void:
	rematch_requested.emit()


func _on_menu_button() -> void:
	menu_requested.emit()


func _build() -> void:
	if _built:
		return
	_built = true
	UITheme.place_full(self)
	mouse_filter = Control.MOUSE_FILTER_STOP

	var bg := UITheme.panel(self, "ResultsBackground", UITheme.DIM_OVERLAY_STRONG,
		Vector2.ZERO, Vector2.ONE, Vector2(0, 0), Vector2(0, 0))
	var center := Vector2(0.5, 0.5)
	var card := UITheme.panel(bg, "ResultsCard", UITheme.PANEL_SOLID,
		center, center, Vector2(0, 0), Vector2(560, 520))
	var top := Vector2(0.5, 1.0)
	var bottom := Vector2(0.5, 0.0)
	_title_label = UITheme.label(card, "TitleText", "Kết quả",
		36, UITheme.ACCENT_TEXT, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(0, -50), Vector2(520, 50))
	_score_label = UITheme.label(card, "ScoreText", "",
		24, UITheme.TEXT_WHITE, HORIZONTAL_ALIGNMENT_CENTER, VERTICAL_ALIGNMENT_CENTER,
		top, top, Vector2(0, -105), Vector2(520, 34))
	_stats_label = UITheme.label(card, "StatsText", "",
		22, UITheme.TEXT_DIM, HORIZONTAL_ALIGNMENT_LEFT, VERTICAL_ALIGNMENT_TOP,
		top, top, Vector2(0, -280), Vector2(480, 300))
	var rematch_btn := UITheme.button(card, "RematchButton", "Đấu lại", 24,
		bottom, bottom, Vector2(-140, 45), Vector2(230, 56))
	rematch_btn.pressed.connect(_on_rematch_button)
	var menu_btn := UITheme.button(card, "MenuButton", "Về menu", 24,
		bottom, bottom, Vector2(140, 45), Vector2(230, 56))
	menu_btn.pressed.connect(_on_menu_button)
