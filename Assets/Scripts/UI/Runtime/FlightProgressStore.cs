using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace WingSinger.UI {
    [Serializable]
    public sealed class FlightRecord {
        public string id, completedAtUtc, levelTitle, levelId;
        public int levelNumber, collected, completedCycles;
        public float seconds, rhythmPercent;
        public bool practice;
        public DateTime LocalDate => DateTime.TryParse(completedAtUtc, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var value) ? value.ToLocalTime().Date : DateTime.MinValue;
    }
    [Serializable]
    public sealed class FlightProgressData {
        public int version = 1, unlockedLevels = 1;
        public float musicVolume = .6f, soundVolume = .8f;
        public bool reduceMotion, highContrast;
        public List<FlightRecord> records = new List<FlightRecord>();
    }
    public sealed class FlightProgressStore {
        readonly string key;
        public FlightProgressData Data { get; private set; }
        public FlightProgressStore(string preferenceKey) {
            key = preferenceKey;
            Read(!string.IsNullOrEmpty(key) ? PlayerPrefs.GetString(key, "") : "");
        }
        public void Read(string json) {
            try { Data = string.IsNullOrEmpty(json) ? new FlightProgressData() : JsonUtility.FromJson<FlightProgressData>(json); }
            catch (ArgumentException) { Data = null; }
            if (Data == null || Data.version != 1) Data = new FlightProgressData();
            if (Data.records == null) Data.records = new List<FlightRecord>();
            Data.records.RemoveAll(r => r == null || r.levelNumber < 1 || r.LocalDate == DateTime.MinValue
                || string.IsNullOrEmpty(r.id) || float.IsNaN(r.seconds) || float.IsInfinity(r.seconds) || r.seconds < 0);
            Data.records = Data.records.GroupBy(r => r.id).Select(group => group.First()).Take(128).ToList();
            foreach (var record in Data.records) {
                record.collected = Math.Max(0, record.collected); record.completedCycles = Math.Max(0, record.completedCycles);
                record.rhythmPercent = float.IsNaN(record.rhythmPercent) || float.IsInfinity(record.rhythmPercent) ? 0 : Mathf.Clamp(record.rhythmPercent, 0, 100);
            }
            Data.unlockedLevels = Math.Max(1, Data.unlockedLevels);
            Data.musicVolume = FiniteVolume(Data.musicVolume, .6f); Data.soundVolume = FiniteVolume(Data.soundVolume, .8f);
        }
        static float FiniteVolume(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value);
        public string Export() => JsonUtility.ToJson(Data);
        public void Save() { if (string.IsNullOrEmpty(key)) return; PlayerPrefs.SetString(key, Export()); PlayerPrefs.Save(); }
        public bool Add(FlightRecord record, int availableLevels) {
            if (record == null || string.IsNullOrEmpty(record.id) || Data.records.Any(r => r.id == record.id)) return false;
            Data.records.Insert(0, record);
            if (Data.records.Count > 128) Data.records.RemoveRange(128, Data.records.Count - 128);
            if (!record.practice) Data.unlockedLevels = Math.Max(Data.unlockedLevels, Math.Min(availableLevels, record.levelNumber + 1));
            Save(); return true;
        }
        public bool Completed(int level) => Data.records.Any(r => !r.practice && r.levelNumber == level);
        public bool Completed(string levelId, int legacyNumber) => Data.records.Any(r => !r.practice &&
            (string.IsNullOrEmpty(r.levelId) ? r.levelNumber == legacyNumber : r.levelId == levelId));
        public FlightRecord[] RecentFlights => Data.records.Where(r => !r.practice).Take(3).ToArray();
        public int TodaySessions => Data.records.Count(r => r.LocalDate == DateTime.Today);
        public static DateTime WeekStart => DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
        public int WeeklyFlights => Data.records.Count(r => !r.practice && r.LocalDate >= WeekStart && r.LocalDate <= DateTime.Today);
        public double WeeklyMinutes => Data.records.Where(r => r.LocalDate >= WeekStart && r.LocalDate <= DateTime.Today).Sum(r => r.seconds) / 60.0;
        public bool HasActivity(DateTime date) => Data.records.Any(r => r.LocalDate == date.Date);
        public int Streak {
            get {
                DateTime date = HasActivity(DateTime.Today) ? DateTime.Today : DateTime.Today.AddDays(-1);
                int streak = 0;
                while (HasActivity(date)) { streak++; date = date.AddDays(-1); }
                return streak;
            }
        }
    }
}
