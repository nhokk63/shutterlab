// SaveManager.cs — ShuttleLab (Unity 6)
//
// Lưu dài hạn bằng JSON có version trong Application.persistentDataPath.
// File: "shuttlelab_save_v1.json", backup: cùng tên + ".bak".
//
// PHẠM VI (theo ARCHITECTURE.md):
//  - SaveManager lo DỮ LIỆU DÀI HẠN: profile, tiến trình Career, trang bị
//    sở hữu/đang dùng, kỷ lục training. KHÔNG thay GameStateManager —
//    setting chạy (độ khó, âm lượng...) vẫn do GameStateManager giữ qua
//    PlayerPrefs; profile trong file save chỉ là bản sao để đồng bộ khi
//    load (ApplyToGameState) / trước khi save (SyncFromGameState).
//
// Ghi atom: ghi file tạm ".tmp" -> copy file cũ thành ".bak" -> đổi tên
// ".tmp" thành file chính. Đọc lỗi/corrupt: thử ".bak", vẫn lỗi thì về
// mặc định (không crash, không mất app). Version: trường Version + stub
// Migrate (v0.1.0 chỉ có v1 nên stub chưa làm gì ngoài nâng số version).
//
// Phụ thuộc: UnityEngine (JsonUtility, Application, Debug). Không phụ
// thuộc module game nào — các manager khác tự LoadFromSave/WriteToSave
// vào SaveData. Không dùng Dictionary trong SaveData (JsonUtility không
// hỗ trợ) — dùng List<...> thay thế.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShuttleLab.Systems
{
    /// <summary>Một món trang bị đang đeo, theo khe (Racket/Shoes/Shirt).</summary>
    [Serializable]
    public class EquippedEntry
    {
        public string Slot = "";
        public string ItemId = "";
    }

    /// <summary>Điểm tốt nhất của một bài tập (đơn vị tùy bài: số lần / %).</summary>
    [Serializable]
    public class TrainingBestEntry
    {
        public string DrillId = "";
        public float BestScore = 0f;
    }

    /// <summary>Tiến trình sự nghiệp lưu được (CareerManager là chủ sở hữu logic).</summary>
    [Serializable]
    public class CareerSaveData
    {
        public bool Active = false;
        public int Week = 0;
        public int RankingPoints = 0;
        public int Money = 0;
        public int Wins = 0;
        public int Losses = 0;
        public int Titles = 0;
        public int CurrentTierIndex = 0;
        public int TrainingPoints = 0;
        public int TournamentsPlayed = 0;
    }

    /// <summary>Toàn bộ dữ liệu lưu của game (version 1).</summary>
    [Serializable]
    public class SaveData
    {
        public int Version = SaveManager.CurrentVersion;

        // Profile (bản sao của setting chạy — xem ghi chú đầu file).
        public string PlayerName = "Bạn";
        public int Difficulty = 2;
        public float MusicVolume = 0.8f;
        public float SfxVolume = 0.8f;
        public int CameraPreset = 0;
        public bool LeftHandMode = false;

        public CareerSaveData Career = new CareerSaveData();
        public List<string> OwnedEquipmentIds = new List<string>();
        public List<EquippedEntry> Equipped = new List<EquippedEntry>();
        public List<TrainingBestEntry> TrainingBests = new List<TrainingBestEntry>();
    }

    /// <summary>
    /// Static class duy nhất chạm tới file save. Không phải MonoBehaviour.
    /// </summary>
    public static class SaveManager
    {
        public const int CurrentVersion = 1;
        public const string FileName = "shuttlelab_save_v1.json";

        public static string SavePath =>
            Path.Combine(Application.persistentDataPath, FileName);

        public static string BackupPath => SavePath + ".bak";

        private static SaveData _current;

        /// <summary>Dữ liệu hiện tại; load lười lần đầu truy cập.</summary>
        public static SaveData Current
        {
            get
            {
                if (_current == null) Load();
                return _current;
            }
        }

        /// <summary>Đọc file save. Lỗi/corrupt -> thử backup -> mặc định.</summary>
        public static SaveData Load()
        {
            _current = TryLoadFile(SavePath);
            if (_current == null)
            {
                Debug.LogWarning("[SaveManager] File save chính lỗi/mất, thử backup.");
                _current = TryLoadFile(BackupPath);
            }
            if (_current == null)
            {
                Debug.LogWarning("[SaveManager] Không có save hợp lệ — dùng mặc định.");
                _current = new SaveData();
            }
            if (_current.Version < CurrentVersion) Migrate(_current);
            if (_current.Version != CurrentVersion) _current.Version = CurrentVersion;
            return _current;
        }

        private static SaveData TryLoadFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path);
                if (string.IsNullOrEmpty(json)) return null;
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null) return null;
                // Bảo vệ khỏi JSON thiếu trường (JsonUtility để null).
                if (data.Career == null) data.Career = new CareerSaveData();
                if (data.OwnedEquipmentIds == null) data.OwnedEquipmentIds = new List<string>();
                if (data.Equipped == null) data.Equipped = new List<EquippedEntry>();
                if (data.TrainingBests == null) data.TrainingBests = new List<TrainingBestEntry>();
                if (string.IsNullOrEmpty(data.PlayerName)) data.PlayerName = "Bạn";
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Không đọc được {path}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Ghi atom: file tạm -> backup file cũ -> đổi tên thành file chính.
        /// Trả false nếu ghi thất bại (log warning, không throw).
        /// </summary>
        public static bool Save()
        {
            return Save(Current);
        }

        public static bool Save(SaveData data)
        {
            if (data == null) return false;
            data.Version = CurrentVersion;
            string tmpPath = SavePath + ".tmp";
            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(tmpPath, json);
                // Giữ bản trước làm backup rồi mới thay file chính.
                if (File.Exists(SavePath)) File.Copy(SavePath, BackupPath, overwrite: true);
                if (File.Exists(SavePath)) File.Delete(SavePath);
                File.Move(tmpPath, SavePath);
                _current = data;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Ghi save thất bại: {e.Message}");
                try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { /* bỏ qua */ }
                return false;
            }
        }

        /// <summary>Xóa save hiện tại, về mặc định và ghi lại.</summary>
        public static void ResetToDefaults()
        {
            _current = new SaveData();
            Save();
        }

        /// <summary>
        /// Nâng dữ liệu cũ lên version hiện tại. STUB v0.1.0: chỉ có v1 nên
        /// chưa có bước migrate thật; khi thêm version mới, viết từng bước
        /// ở đây (v1 -> v2, v2 -> v3...), không xóa trường cũ lặng lẽ.
        /// </summary>
        private static void Migrate(SaveData data)
        {
            Debug.Log($"[SaveManager] Migrate save v{data.Version} -> v{CurrentVersion} (stub).");
            data.Version = CurrentVersion;
        }

        // --- Đồng bộ profile với GameStateManager (setting chạy) -----------

        /// <summary>Đẩy profile từ file save vào GameStateManager (gọi khi load).</summary>
        public static void ApplyToGameState()
        {
            var gs = Core.GameStateManager.Instance;
            if (gs == null) return;
            var d = Current;
            gs.PlayerName = d.PlayerName;
            gs.Difficulty = d.Difficulty;
            gs.MusicVolume = d.MusicVolume;
            gs.SfxVolume = d.SfxVolume;
            gs.CameraPreset = d.CameraPreset;
            gs.LeftHandMode = d.LeftHandMode;
        }

        /// <summary>Kéo setting chạy từ GameStateManager vào SaveData trước khi Save().</summary>
        public static void SyncFromGameState()
        {
            var gs = Core.GameStateManager.Instance;
            if (gs == null) return;
            var d = Current;
            d.PlayerName = gs.PlayerName;
            d.Difficulty = gs.Difficulty;
            d.MusicVolume = gs.MusicVolume;
            d.SfxVolume = gs.SfxVolume;
            d.CameraPreset = gs.CameraPreset;
            d.LeftHandMode = gs.LeftHandMode;
        }

        // --- Kỷ lục training (TrainingManager gọi) --------------------------

        /// <summary>
        /// Lưu điểm tốt nhất của một bài tập. Trả true nếu đây là kỷ lục mới.
        /// </summary>
        public static bool RecordTrainingBest(string drillId, float score)
        {
            if (string.IsNullOrEmpty(drillId)) return false;
            var d = Current;
            var entry = d.TrainingBests.Find(e => e.DrillId == drillId);
            if (entry == null)
            {
                d.TrainingBests.Add(new TrainingBestEntry { DrillId = drillId, BestScore = score });
                Save();
                return true;
            }
            if (score > entry.BestScore)
            {
                entry.BestScore = score;
                Save();
                return true;
            }
            return false;
        }

        public static float GetTrainingBest(string drillId)
        {
            if (string.IsNullOrEmpty(drillId)) return 0f;
            var entry = Current.TrainingBests.Find(e => e.DrillId == drillId);
            return entry != null ? entry.BestScore : 0f;
        }
    }
}
