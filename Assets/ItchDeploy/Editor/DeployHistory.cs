using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ItchDeploy.Editor
{
    public static class DeployHistory
    {
        [Serializable]
        public class Entry
        {
            public string version;
            public string platform;
            public string channel;
            public string status;

            public string timestampUtc;
            public string buildTarget;
            public long buildSizeBytes;

            public DateTime TimestampUtc
            {
                get
                {
                    return DateTime.TryParse(
                        timestampUtc,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out var value)
                        ? value.ToUniversalTime()
                        : DateTime.MinValue;
                }
            }

            public string FormattedSize
            {
                get
                {
                    if (buildSizeBytes < 1024) return $"{buildSizeBytes} B";
                    if (buildSizeBytes < 1024 * 1024) return $"{buildSizeBytes / 1024.0:F1} KB";
                    return $"{buildSizeBytes / (1024.0 * 1024.0):F1} MB";
                }
            }

            public string FormattedTime => TimestampUtc == DateTime.MinValue
                ? "—"
                : TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        [Serializable]
        public class HistoryData
        {
            public List<Entry> entries = new();
        }

        private static readonly string HistoryPath = Path.Combine(
            Application.dataPath, "ItchDeploy", "deploy-history.json");

        private static HistoryData _cache;

        public static List<Entry> GetAll()
        {
            if (_cache != null) return _cache.entries;
            _cache = new HistoryData();
            if (File.Exists(HistoryPath))
            {
                try
                {
                    var json = File.ReadAllText(HistoryPath);
                    _cache = JsonUtility.FromJson<HistoryData>(json) ?? new HistoryData();
                }
                catch
                {
                    _cache = new HistoryData();
                }
            }
            _cache.entries ??= new List<Entry>();
            _cache.entries = _cache.entries
                .Where(entry => entry != null)
                .OrderByDescending(entry => entry.TimestampUtc)
                .ToList();
            return _cache.entries;
        }

        internal const int MaxEntries = 200;

        public static void AddEntry(Entry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (string.IsNullOrWhiteSpace(entry.timestampUtc))
                entry.timestampUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            var entries = GetAll();
            entries.Insert(0, entry);
            if (entries.Count > MaxEntries)
                entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
            Save();
        }

        public static void Save()
        {
            if (_cache == null) return;
            var json = JsonUtility.ToJson(_cache, true);
            var dir = Path.GetDirectoryName(HistoryPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var temporaryPath = HistoryPath + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, json);
                if (File.Exists(HistoryPath))
                    File.Delete(HistoryPath);
                File.Move(temporaryPath, HistoryPath);
            }
            catch (IOException exception)
            {
                Debug.LogWarning(
                    $"[ItchDeploy] Could not save the deployment history: {exception.Message}");
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                catch (IOException) { }
            }
        }

        public static void Clear()
        {
            _cache = new HistoryData();
            if (File.Exists(HistoryPath))
                File.Delete(HistoryPath);
        }
    }
}
