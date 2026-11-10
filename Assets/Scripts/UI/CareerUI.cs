// CareerUI.cs — ShuttleLab (Unity 6)
//
// Màn hình Sự nghiệp (scene Career): 3 tab — Tổng quan / Luyện tập /
// Cửa hàng. Tự dựng UI bằng code (UIBuilder) như MainMenu.
//
//  - Tổng quan: tuần, hạng, điểm ranking, tiền ảo, giải sắp tới, nút
//    "Sang tuần" (CareerManager.AdvanceWeek), nút "Thi đấu giải"
//    (GameStateManager.LoadMatch Tournament) khi đang là tuần giải và
//    còn đối thủ, bracket hiện tại (TournamentManager), dòng trạng thái
//    online lấy từ OfflineNetworkService (trung thực: chưa khả dụng).
//  - Luyện tập: chọn nhóm -> danh sách 18 bài (TrainingManager),
//    xem mô tả + mục tiêu + kỷ lục đã lưu; nút "Vào tập" DISABLE kèm
//    lý do trung thực (chơi bài tập cần Practice tích hợp drill — chưa có).
//  - Cửa hàng: chọn khe (Vợt/Giày/Áo) -> mua bằng tiền Career / đeo
//    (EquipmentManager.Buy/Equip), hiển thị đã sở hữu / đang đeo.
//
// GIỚI HẠN TRUNG THỰC v0.1.0 (đọc trước khi nối thêm):
//  1. Bracket giải chỉ sống trong bộ nhớ (TournamentManager không lưu
//     vào SaveManager). Thoát scene Career giữa giải thì bracket mất;
//     vào lại cùng tuần giải sẽ tạo lại bracket với CÙNG seed (kết quả
//     mô phỏng AI giống hệt, trận của user đánh lại từ đầu).
//  2. Kết quả trận giải CHƯA tự cập nhật bracket: cần GameBootstrap nối
//     MatchManager.OnMatchEnd -> TournamentManager.ReportPlayerMatch.
//     Nút "Thi đấu giải" vẫn mở trận thật (AppMode.Tournament), nhưng
//     thắng/thua chưa tự ghi vào bracket — UI ghi rõ điều này.
//  3. Điểm tập luyện (TrainingPoints) chỉ hiển thị tích lũy, chưa đổi
//     thành tăng chỉ số (cần luồng chơi bài tập thật để chấm).
//
// GameObject & wiring cho SceneBuilder:
//  - Scene Career: GameObject "CareerUI" (root) + gắn component này.
//    Component tự dựng Canvas + toàn bộ UI bằng code trong Awake.
//  - GameStateManager là singleton DontDestroyOnLoad; nếu vào thẳng
//    scene Career mà chưa có thì CareerUI tự tạo một cái (như MainMenu).
//
// Phụ thuộc: ShuttleLab.Core (GameStateManager), ShuttleLab.Systems
// (CareerManager, TournamentManager, TrainingManager, EquipmentManager,
// SaveManager), ShuttleLab.Networking (OfflineNetworkService).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ShuttleLab.Core;
using ShuttleLab.Networking;
using ShuttleLab.Systems;

namespace ShuttleLab.UI
{
    public class CareerUI : MonoBehaviour
    {
        private CareerManager _career;
        private TournamentManager _tournament;
        private EquipmentManager _equipment;

        private Transform _root;
        private bool _built;

        // Tab 0 = Tổng quan, 1 = Luyện tập, 2 = Cửa hàng.
        private int _tab;
        private GameObject _overviewPanel;
        private GameObject _trainingPanel;
        private GameObject _shopPanel;
        private Button[] _tabButtons = new Button[3];

        // Tổng quan.
        private Text _infoText;
        private Text _tournamentText;
        private Text _bracketText;
        private Text _onlineText;
        private Button _startCareerButton;
        private Button _advanceWeekButton;
        private Button _playTournamentButton;
        private Text _tournamentNoteText;

        // Luyện tập.
        private string _selectedCategory = "Footwork";
        private string _selectedDrillId;
        private Transform _drillListRoot;
        private Text _drillDetailText;
        private Button _startDrillButton;
        private Button[] _categoryButtons;

        // Cửa hàng.
        private EquipmentSlot _selectedSlot = EquipmentSlot.Racket;
        private string _selectedItemId;
        private Transform _shopListRoot;
        private ScrollRect _shopScroll;
        private Text _shopDetailText;
        private Text _shopMoneyText;
        private Button _buyButton;
        private Button _equipButton;
        private Text _shopMessageText;

        private void Awake()
        {
            if (GameStateManager.Instance == null)
            {
                var go = new GameObject("GameStateManager");
                go.AddComponent<GameStateManager>();
            }
            // Như MainMenu: đảm bảo có AudioManager để nút có tiếng click.
            if (!ShuttleLab.Systems.AudioManager.HasInstance)
            {
                var audioGO = new GameObject("AudioManager");
                audioGO.AddComponent<ShuttleLab.Systems.AudioManager>();
            }

            _career = new CareerManager();
            _career.LoadFromSave(SaveManager.Current);
            _equipment = new EquipmentManager();
            _equipment.LoadFromSave(SaveManager.Current);
            // Khôi phục bracket tuần giải đang chơi (nếu vừa đánh xong trận
            // giải quay lại); tuần mới / sự nghiệp mới tạo bracket mới ở
            // OnAdvanceWeekButton / OnStartCareerButton.
            _tournament = TournamentSession.Instance?.Tournament ?? new TournamentManager();

            BuildIfNeeded();
            Refresh();
        }

        // --- Nút tab ----------------------------------------------------------

        public void OnTabButton(int tab)
        {
            _tab = Mathf.Clamp(tab, 0, 2);
            Refresh();
        }

        public void OnBackToMenuButton()
        {
            SaveManager.SyncFromGameState();
            SaveManager.Save();
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.LoadMenu();
        }

        // --- Tổng quan ---------------------------------------------------------

        public void OnStartCareerButton()
        {
            _career.StartNewCareer();
            // Sự nghiệp mới = làm lại từ đầu: shop về 3 món khởi đầu,
            // điểm cộng chỉ số từ điểm tập cũng xóa.
            _equipment.ResetToStarters();
            ShuttleLab.Systems.TrainingManager.ClearAllBonuses();
            _selectedItemId = null;
            _tournament = new TournamentManager();
            Refresh();
        }

        public void OnAdvanceWeekButton()
        {
            bool isTournamentWeek = _career.AdvanceWeek();
            if (isTournamentWeek)
            {
                // Tạo bracket 16 người cho tuần giải. Seed theo hạng:
                // hạng 1 -> seed 1, hạng 512 -> seed 16. Seed RNG theo tuần
                // nên vào lại cùng tuần sẽ tạo lại bracket giống hệt.
                int seed = 1 + (_career.Ranking - 1) * 15 / 511;
                seed = Mathf.Clamp(seed, 1, 16);
                string name = GameStateManager.Instance != null
                    ? GameStateManager.Instance.PlayerName : "Bạn";
                _tournament = new TournamentManager();
                _tournament.CreateBracket(16, name, seed, _career.Week * 7919 + 13);
                _tournament.SimulateRound(); // chỉ mô phỏng trận AI, không chạm trận user
            }
            Refresh();
        }

        public void OnPlayTournamentButton()
        {
            if (!_career.IsTournamentWeek) return;
            if (_tournament == null || !_tournament.IsActive) return;
            if (_tournament.NextOpponent() == null) return;
            SaveManager.SyncFromGameState();
            SaveManager.Save();
            // Giữ bracket + sự nghiệp qua đổi scene để HUD ghi kết quả
            // trận giải vào đúng chỗ khi trận kết thúc.
            if (TournamentSession.Instance == null)
            {
                var sessionGO = new GameObject("TournamentSession");
                sessionGO.AddComponent<TournamentSession>();
            }
            TournamentSession.Instance.Tournament = _tournament;
            TournamentSession.Instance.Career = _career;
            if (GameStateManager.Instance != null)
            {
                int difficulty = GameStateManager.Instance.Difficulty;
                GameStateManager.Instance.LoadMatch(GameStateManager.AppMode.Tournament, difficulty);
            }
        }

        // --- Luyện tập ----------------------------------------------------------

        public void OnCategoryButton(string category)
        {
            _selectedCategory = category;
            _selectedDrillId = null;
            Refresh();
        }

        public void OnDrillButton(string drillId)
        {
            _selectedDrillId = drillId;
            Refresh();
        }

        // --- Cửa hàng ------------------------------------------------------------

        public void OnSlotButton(EquipmentSlot slot)
        {
            _selectedSlot = slot;
            _selectedItemId = null;
            Refresh();
            if (_shopScroll != null) _shopScroll.verticalNormalizedPosition = 1f;
        }

        public void OnShopItemButton(string itemId)
        {
            _selectedItemId = itemId;
            Refresh();
        }

        public void OnBuyButton()
        {
            var item = _equipment.GetItem(_selectedItemId);
            if (item == null) return;
            if (_equipment.Buy(item, _career))
            {
                if (_shopMessageText != null)
                    _shopMessageText.text = $"Đã mua {item.Name}.";
            }
            else
            {
                if (_shopMessageText != null)
                    _shopMessageText.text = _equipment.LastError;
            }
            Refresh();
        }

        public void OnEquipButton()
        {
            if (_equipment.Equip(_selectedItemId))
            {
                if (_shopMessageText != null)
                    _shopMessageText.text = "Đã đeo.";
            }
            else
            {
                if (_shopMessageText != null)
                    _shopMessageText.text = _equipment.LastError;
            }
            Refresh();
        }

        // --- Hiển thị ------------------------------------------------------------

        private void Refresh()
        {
            if (!_built) return;

            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] != null)
                    UIBuilder.SetButtonSelected(_tabButtons[i], i == _tab);
            }
            if (_overviewPanel != null) _overviewPanel.SetActive(_tab == 0);
            if (_trainingPanel != null) _trainingPanel.SetActive(_tab == 1);
            if (_shopPanel != null) _shopPanel.SetActive(_tab == 2);

            if (_tab == 0) RefreshOverview();
            else if (_tab == 1) RefreshTraining();
            else RefreshShop();
        }

        private void RefreshOverview()
        {
            if (_infoText != null)
                _infoText.text = _career.SummaryLine()
                    + $" • Thắng {_career.Wins} - Thua {_career.Losses}"
                    + $" • Điểm tập {_career.TrainingPoints}";

            bool active = _career.Active;
            if (_startCareerButton != null)
                _startCareerButton.gameObject.SetActive(!active);
            if (_advanceWeekButton != null)
                _advanceWeekButton.gameObject.SetActive(active);
            if (_playTournamentButton != null)
                _playTournamentButton.gameObject.SetActive(active);

            // Dòng 2: giải đặc biệt tuần + tiến trình nhiệm vụ tuần.
            var weeklyEvent = TournamentManager.GetWeeklyEvent();
            string weeklyLine = $"\nGiải tuần: {weeklyEvent.Name} ({weeklyEvent.Tier})"
                + $" • NV: thắng {WeeklyMissions.WinsProgress}/3"
                + $" • smash {WeeklyMissions.SmashesProgress}/10"
                + (WeeklyMissions.CleanWinDone ? " • thắng sạch: xong" : " • thắng sạch: chưa");

            if (_tournamentText != null)
            {
                if (!active)
                {
                    _tournamentText.text = "Bắt đầu sự nghiệp để chơi theo tuần: "
                        + "3 tuần tập luyện, 1 tuần thi đấu, thăng hạng qua các cấp giải."
                        + weeklyLine;
                }
                else if (_career.IsTournamentWeek)
                {
                    var info = _career.NextTournamentInfo;
                    _tournamentText.text = $"TUẦN GIẢI — {info.EventName} ({info.Tier})"
                        + $" • Vô địch: +{info.WinPoints} điểm, {info.PrizeMoney} xu"
                        + $" • Còn {_career.WeeksUntilTournament} tuần tới giải sau."
                        + weeklyLine;
                }
                else
                {
                    var info = _career.NextTournamentInfo;
                    _tournamentText.text = $"Tuần tập luyện — giải tới: {info.EventName} ({info.Tier})"
                        + $" sau {_career.WeeksUntilTournament} tuần nữa."
                        + weeklyLine;
                }
            }

            bool canPlay = active && _career.IsTournamentWeek
                && _tournament != null && _tournament.IsActive
                && _tournament.NextOpponent() != null;
            if (_playTournamentButton != null)
                _playTournamentButton.interactable = canPlay;
            if (_tournamentNoteText != null)
            {
                if (!active)
                    _tournamentNoteText.text = "";
                else if (!_career.IsTournamentWeek)
                    _tournamentNoteText.text = "Nút thi đấu chỉ mở vào tuần giải.";
                else if (_tournament == null || !_tournament.IsActive)
                    _tournamentNoteText.text = "Bracket chưa tạo — bấm \"Sang tuần\" để vào tuần giải.";
                else if (_tournament.NextOpponent() == null)
                    _tournamentNoteText.text = _tournament.IsPlayerChampion
                        ? "Bạn đã vô địch giải này! Sang tuần để tiếp tục."
                        : "Bạn đã bị loại khỏi giải này. Sang tuần để tiếp tục.";
                else
                    _tournamentNoteText.text = "Kết quả trận giải chưa tự cập nhật bracket "
                        + "(cần GameBootstrap nối MatchManager -> TournamentManager).";
            }

            if (_bracketText != null)
            {
                if (_tournament != null && _tournament.IsActive)
                {
                    var lines = _tournament.GetBracketLines();
                    _bracketText.text = string.Join("\n", lines);
                }
                else
                {
                    _bracketText.text = "Chưa có bracket giải đấu.";
                }
            }

            if (_onlineText != null)
            {
                var result = OfflineNetworkService.Shared.GetLeaderboard(10);
                _onlineText.text = "Bảng xếp hạng online: " + result.Message;
            }
        }

        private void RefreshTraining()
        {
            // Nút nhóm.
            for (int i = 0; i < _categoryButtons.Length && i < TrainingManager.Categories.Length; i++)
            {
                if (_categoryButtons[i] != null)
                    UIBuilder.SetButtonSelected(_categoryButtons[i],
                        TrainingManager.Categories[i] == _selectedCategory);
            }

            // Danh sách bài của nhóm đang chọn (xây lại mỗi lần refresh).
            if (_drillListRoot != null)
            {
                foreach (Transform child in _drillListRoot)
                    Destroy(child.gameObject);
                var drills = TrainingManager.ListByCategory(_selectedCategory);
                for (int i = 0; i < drills.Count; i++)
                {
                    var d = drills[i];
                    string id = d.Id; // copy cho closure
                    var b = UIBuilder.CreateButton(_drillListRoot, $"DrillButton{i}",
                        d.Name, 20, () => OnDrillButton(id),
                        new Vector2(0f, 1f), new Vector2(0f, 1f),
                        new Vector2(300f, -30f - i * 52f), new Vector2(580f, 46f));
                    UIBuilder.SetButtonSelected(b, d.Id == _selectedDrillId);
                }
            }

            if (_drillDetailText != null)
            {
                var def = TrainingManager.GetDrill(_selectedDrillId);
                if (def == null)
                {
                    _drillDetailText.text = "Chọn một bài tập để xem mục tiêu và kỷ lục.";
                }
                else
                {
                    float best = SaveManager.GetTrainingBest(def.Id);
                    string bestText = best > 0
                        ? $"Kỷ lục của bạn: {best:0.##} {(def.Unit == DrillUnit.Seconds ? "giây" : def.Unit == DrillUnit.Accuracy ? "%" : "lần")}"
                        : "Chưa có kỷ lục.";
                    _drillDetailText.text = $"{def.Name} ({def.Category})\n{def.Description}\n"
                        + $"Mục tiêu: {def.TargetText}\n{bestText}";
                }
            }

            if (_startDrillButton != null)
            {
                // Chưa có luồng chơi bài tập thật — disable kèm lý do trung thực.
                _startDrillButton.interactable = false;
            }
        }

        private void RefreshShop()
        {
            if (_shopMoneyText != null)
                _shopMoneyText.text = $"Tiền của bạn: {_career.Money} xu";

            if (_shopListRoot != null)
            {
                foreach (Transform child in _shopListRoot)
                    Destroy(child.gameObject);
                var items = _equipment.ListBySlot(_selectedSlot);
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    string id = item.Id; // copy cho closure
                    string state = _equipment.IsEquipped(id) ? " [ĐANG ĐEO]"
                        : _equipment.IsOwned(id) ? " [Đã có]" : $" — {item.Price} xu";
                    var b = UIBuilder.CreateButton(_shopListRoot, $"ShopItem{i}",
                        item.Name + state, 20, () => OnShopItemButton(id),
                        new Vector2(0f, 1f), new Vector2(0f, 1f),
                        new Vector2(300f, -30f - i * 52f), new Vector2(580f, 46f));
                    var label = b.GetComponentInChildren<Text>();
                    if (label != null) label.color = item.RarityColor();
                    UIBuilder.SetButtonSelected(b, item.Id == _selectedItemId);
                }
                // Nội dung cao theo số món để ScrollRect cuộn hết danh sách.
                var contentRt = (RectTransform)_shopListRoot;
                contentRt.sizeDelta = new Vector2(0f, 40f + items.Count * 52f);
            }

            if (_shopDetailText != null)
            {
                var item = _equipment.GetItem(_selectedItemId);
                if (item == null)
                {
                    _shopDetailText.text = "Chọn một món để xem chỉ số và giá.";
                }
                else
                {
                    string owned = _equipment.IsOwned(item.Id) ? "Đã sở hữu" : "Chưa sở hữu";
                    string equipped = _equipment.IsEquipped(item.Id) ? " • Đang đeo" : "";
                    _shopDetailText.text = $"[{item.RarityName()}] {item.Name}\n{item.Description}\n"
                        + $"Chỉ số: {item.ModText()}\nGiá: {item.Price} xu • {owned}{equipped}";
                }
            }

            var selected = _equipment.GetItem(_selectedItemId);
            if (_buyButton != null)
            {
                _buyButton.gameObject.SetActive(selected != null && !_equipment.IsOwned(selected.Id));
                _buyButton.interactable = selected != null && _career.Money >= selected.Price;
            }
            if (_equipButton != null)
            {
                _equipButton.gameObject.SetActive(selected != null && _equipment.IsOwned(selected.Id)
                    && !_equipment.IsEquipped(selected.Id));
            }
            if (_shopMessageText != null && selected == null)
                _shopMessageText.text = "";
        }

        // --- Dựng UI bằng code ---------------------------------------------------

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            var canvas = GetComponentInChildren<Canvas>();
            if (canvas == null)
            {
                canvas = UIBuilder.CreateCanvas("CareerCanvas");
                canvas.transform.SetParent(transform, false);
            }
            _root = canvas.transform;

            UIBuilder.CreatePanel(_root, "CareerBackground",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.03f, 0.10f, 0.06f, 1f));

            UIBuilder.CreateText(_root, "TitleText", "SỰ NGHIỆP",
                40, UIBuilder.AccentText, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -45f), new Vector2(700f, 50f));

            // Hàng tab.
            string[] tabNames = { "Tổng quan", "Luyện tập", "Cửa hàng" };
            for (int i = 0; i < 3; i++)
            {
                int tab = i; // copy cho closure
                _tabButtons[i] = UIBuilder.CreateButton(_root, $"TabButton{i}",
                    tabNames[i], 22, () => OnTabButton(tab),
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(-320f + i * 320f, -110f), new Vector2(300f, 52f));
            }

            // Nút về menu (góc dưới).
            UIBuilder.CreateButton(_root, "BackButton", "Về menu", 22, OnBackToMenuButton,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 30f), new Vector2(260f, 52f));

            BuildOverviewPanel();
            BuildTrainingPanel();
            BuildShopPanel();
        }

        private void BuildOverviewPanel()
        {
            _overviewPanel = new GameObject("OverviewPanel", typeof(RectTransform));
            _overviewPanel.transform.SetParent(_root, false);
            var rt = _overviewPanel.GetComponent<RectTransform>();
            UIBuilder.Place(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Transform p = _overviewPanel.transform;
            _infoText = UIBuilder.CreateText(p, "InfoText", "",
                22, UIBuilder.TextWhite, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -175f), new Vector2(1100f, 30f));

            _startCareerButton = UIBuilder.CreateButton(p, "StartCareerButton",
                "Bắt đầu sự nghiệp", 24, OnStartCareerButton,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-320f, -225f), new Vector2(320f, 56f));
            _advanceWeekButton = UIBuilder.CreateButton(p, "AdvanceWeekButton",
                "Sang tuần", 24, OnAdvanceWeekButton,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-320f, -225f), new Vector2(320f, 56f));
            _playTournamentButton = UIBuilder.CreateButton(p, "PlayTournamentButton",
                "Thi đấu giải", 24, OnPlayTournamentButton,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(120f, -225f), new Vector2(320f, 56f));

            _tournamentText = UIBuilder.CreateText(p, "TournamentText", "",
                20, UIBuilder.TextDim, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -285f), new Vector2(1100f, 56f));
            _tournamentNoteText = UIBuilder.CreateText(p, "TournamentNoteText", "",
                18, UIBuilder.AccentText, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -340f), new Vector2(1100f, 26f));
            _bracketText = UIBuilder.CreateText(p, "BracketText", "",
                18, UIBuilder.TextWhite, TextAnchor.UpperLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -455f), new Vector2(1100f, 200f));
            _onlineText = UIBuilder.CreateText(p, "OnlineText", "",
                16, UIBuilder.TextDim, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 100f), new Vector2(1100f, 24f));
        }

        private void BuildTrainingPanel()
        {
            _trainingPanel = new GameObject("TrainingPanel", typeof(RectTransform));
            _trainingPanel.transform.SetParent(_root, false);
            var rt = _trainingPanel.GetComponent<RectTransform>();
            UIBuilder.Place(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Transform p = _trainingPanel.transform;
            _categoryButtons = new Button[TrainingManager.Categories.Length];
            for (int i = 0; i < TrainingManager.Categories.Length; i++)
            {
                string cat = TrainingManager.Categories[i]; // copy cho closure
                float x = -490f + (i % 4) * 330f;
                float y = -185f - (i / 4) * 58f;
                _categoryButtons[i] = UIBuilder.CreateButton(p, $"CategoryButton{i}",
                    cat, 20, () => OnCategoryButton(cat),
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(x, y), new Vector2(300f, 48f));
            }

            _drillListRoot = new GameObject("DrillList", typeof(RectTransform)).transform;
            _drillListRoot.SetParent(p, false);
            UIBuilder.Place((RectTransform)_drillListRoot,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-320f, -310f), new Vector2(600f, 180f));

            _drillDetailText = UIBuilder.CreateText(p, "DrillDetailText", "",
                20, UIBuilder.TextWhite, TextAnchor.UpperLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(320f, -400f), new Vector2(480f, 150f));

            _startDrillButton = UIBuilder.CreateButton(p, "StartDrillButton",
                "Vào tập", 22, null,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(320f, -560f), new Vector2(300f, 52f));
            _startDrillButton.interactable = false;
            UIBuilder.CreateText(p, "DrillNoteText",
                "Bài tập sẽ chơi được khi Practice tích hợp drill (bản sau).",
                16, UIBuilder.TextDim, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(320f, -600f), new Vector2(480f, 24f));
        }

        private void BuildShopPanel()
        {
            _shopPanel = new GameObject("ShopPanel", typeof(RectTransform));
            _shopPanel.transform.SetParent(_root, false);
            var rt = _shopPanel.GetComponent<RectTransform>();
            UIBuilder.Place(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Transform p = _shopPanel.transform;
            _shopMoneyText = UIBuilder.CreateText(p, "ShopMoneyText", "",
                22, UIBuilder.AccentText, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -175f), new Vector2(600f, 30f));

            string[] slotNames = { "Vợt", "Giày", "Áo" };
            EquipmentSlot[] slots = { EquipmentSlot.Racket, EquipmentSlot.Shoes, EquipmentSlot.Shirt };
            for (int i = 0; i < 3; i++)
            {
                EquipmentSlot slot = slots[i]; // copy cho closure
                UIBuilder.CreateButton(p, $"SlotButton{i}", slotNames[i], 20,
                    () => OnSlotButton(slot),
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(-320f + i * 320f, -225f), new Vector2(300f, 48f));
            }

            // Danh sách cuộn được: 25 vợt x 52px = 1300px, vượt xa khung
            // 300px — không có ScrollRect thì các món Hiếm/Huyền thoại nằm
            // ngoài màn hình, không mua được. Viewport cắt phần tràn bằng
            // RectMask2D; nội dung cao theo số món (đặt lại ở RefreshShop).
            var viewport = new GameObject("ShopViewport", typeof(RectTransform));
            viewport.transform.SetParent(p, false);
            UIBuilder.Place((RectTransform)viewport.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-320f, -300f), new Vector2(600f, 300f));
            viewport.AddComponent<RectMask2D>();
            _shopScroll = viewport.AddComponent<ScrollRect>();
            _shopScroll.horizontal = false;
            _shopScroll.vertical = true;
            _shopScroll.movementType = ScrollRect.MovementType.Clamped;
            _shopScroll.inertia = false;

            _shopListRoot = new GameObject("ShopList", typeof(RectTransform)).transform;
            _shopListRoot.SetParent(viewport.transform, false);
            var contentRt = (RectTransform)_shopListRoot;
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 300f);
            _shopScroll.content = contentRt;

            _shopDetailText = UIBuilder.CreateText(p, "ShopDetailText", "",
                20, UIBuilder.TextWhite, TextAnchor.UpperLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(320f, -400f), new Vector2(480f, 150f));

            _buyButton = UIBuilder.CreateButton(p, "BuyButton", "Mua", 22, OnBuyButton,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(220f, -560f), new Vector2(220f, 52f));
            _equipButton = UIBuilder.CreateButton(p, "EquipButton", "Đeo", 22, OnEquipButton,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(460f, -560f), new Vector2(220f, 52f));
            _shopMessageText = UIBuilder.CreateText(p, "ShopMessageText", "",
                18, UIBuilder.AccentText, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(320f, -600f), new Vector2(480f, 26f));
        }
    }
}
