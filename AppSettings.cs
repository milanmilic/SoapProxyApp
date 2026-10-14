using System;
using System.IO;
using Newtonsoft.Json;

namespace SoapProxyApp
{
    public class AppSettings
    {
        public string SkippedUpdateVersion { get; set; }

        private static readonly string filePath = Path.Combine(ProxyEnvironment.DataFolder, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(filePath))
                    return JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(filePath)) ?? new AppSettings();
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
