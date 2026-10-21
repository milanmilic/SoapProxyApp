using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace SoapProxyApp
{
    public class AppSettings
    {
        public string SkippedUpdateVersion { get; set; }
        public int Port { get; set; } = 8899;
        public bool AutoStartProxy { get; set; }
        public bool UseSystemProxy { get; set; }
        public string FilterText { get; set; } = "";
        public bool IsDarkMode { get; set; }
        public List<string> CustomHeaderColumns { get; set; } = new List<string>();

        private static readonly string filePath = Path.Combine(ProxyEnvironment.DataFolder, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var loaded = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(filePath)) ?? new AppSettings();
                    if (loaded.Port < 1 || loaded.Port > 65535) loaded.Port = 8899;
                    if (loaded.CustomHeaderColumns == null) loaded.CustomHeaderColumns = new List<string>();
                    return loaded;
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try { File.WriteAllText(filePath, JsonConvert.SerializeObject(this, Formatting.Indented)); }
            catch { }
        }

        /// <summary>Parses release tags like "v2.2.0", "v.2.2.0" or "2.2.0".</summary>
        public static Version ParseVersionTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            string clean = tag.Trim().TrimStart('v', 'V', '.', ' ');
            return Version.TryParse(clean, out var v) ? v : null;
        }
    }
}
