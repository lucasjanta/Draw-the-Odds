using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ItchDeploy.Editor
{
    public static class BuildStatistics
    {
        private const string StatsFile = "ItchDeploy_BuildStats";

        [Serializable]
        public class BuildEntry
        {
            public string date;
            public string platform;
            public string version;
            public double buildTimeSeconds;
            public double buildSizeMB;
            public bool success;
        }

        [Serializable]
        private class StatsData
        {
            public List<BuildEntry> entries = new();
        }

        private static StatsData _data;

        private static StatsData Data
        {
            get
            {
                if (_data == null)
                    Load();
                return _data;
            }
        }

        public static IReadOnlyList<BuildEntry> Entries => Data.entries;

        public static void RecordBuild(string platform, string version, double timeSeconds, double sizeMB, bool success)
        {
            Data.entries.Add(new BuildEntry
            {
                date = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                platform = platform,
                version = version,
                buildTimeSeconds = timeSeconds,
                buildSizeMB = sizeMB,
                success = success
            });

            if (Data.entries.Count > 100)
                Data.entries.RemoveAt(0);

            Save();
        }

        public static void Load()
        {
            var json = EditorPrefs.GetString(StatsFile, "");
            if (string.IsNullOrEmpty(json))
                _data = new StatsData();
            else
            {
                try { _data = JsonUtility.FromJson<StatsData>(json) ?? new StatsData(); }
                catch { _data = new StatsData(); }
            }
        }

        public static void Save()
        {
            EditorPrefs.SetString(StatsFile, JsonUtility.ToJson(Data));
        }

        public static void Clear()
        {
            _data = new StatsData();
            Save();
        }
    }
}
