using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace AquaVectorUI.services
{
    public static class EnvConfig
    {
        private static readonly Dictionary<string, string> _values =
            new(StringComparer.OrdinalIgnoreCase);

        static EnvConfig()
        {
            string? path = FindEnvFile();
            if (path == null) return;

            foreach (string line in File.ReadLines(path))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith('#') || !trimmed.Contains('=')) continue;
                int idx = trimmed.IndexOf('=');
                string key   = trimmed[..idx].Trim();
                string value = trimmed[(idx + 1)..].Trim();
                _values[key] = value;
            }
        }

        // 실행 파일 디렉토리에서 루트까지 올라가며 .env 탐색
        private static string? FindEnvFile()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, ".env");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return null;
        }

        public static string Get(string key, string defaultValue = "") =>
            _values.TryGetValue(key, out string? v) ? v : defaultValue;

        public static double GetDouble(string key, double defaultValue) =>
            double.TryParse(Get(key), NumberStyles.Float,
                CultureInfo.InvariantCulture, out double v) ? v : defaultValue;

        public static int GetInt(string key, int defaultValue) =>
            int.TryParse(Get(key), out int v) ? v : defaultValue;
    }
}
