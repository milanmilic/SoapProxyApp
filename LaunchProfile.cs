using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SoapProxyApp
{
    public enum ProgramKind { VisualStudio, VSCode, Console, Custom }

    public class ProgramPreset
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string DefaultArguments { get; set; } = "";
        public ProgramKind Kind { get; set; }
        public override string ToString() => Name;
    }

    public class LaunchProfile
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Arguments { get; set; } = "";
        public string WorkingDirectory { get; set; } = "";
        public ProgramKind Kind { get; set; } = ProgramKind.Custom;
        public DateTime LastUsed { get; set; }

        [JsonIgnore]
        public string Display => $"{Name}  —  {System.IO.Path.GetFileName(Path)} {Arguments}".TrimEnd();

        public bool IsSameAs(LaunchProfile other) =>
            string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Arguments ?? "", other.Arguments ?? "", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(WorkingDirectory ?? "", other.WorkingDirectory ?? "", StringComparison.OrdinalIgnoreCase);
    }

    public static class LaunchProfilesManager
    {
        private const int MaxRecent = 10;
        private static readonly string filePath = Path.Combine(ProxyEnvironment.DataFolder, "launch_profiles.json");

        public static List<LaunchProfile> Load()
        {
            try
            {
                if (File.Exists(filePath))
                    return JsonConvert.DeserializeObject<List<LaunchProfile>>(File.ReadAllText(filePath)) ?? new();
            }
            catch { }
            return new();
        }

        public static void Save(List<LaunchProfile> profiles)
        {
            try { File.WriteAllText(filePath, JsonConvert.SerializeObject(profiles, Formatting.Indented)); }
            catch { }
        }

        public static List<LaunchProfile> AddOrUpdate(LaunchProfile profile)
        {
            var list = Load();
            list.RemoveAll(p => p.IsSameAs(profile));
            profile.LastUsed = DateTime.Now;
            list.Insert(0, profile);
            if (list.Count > MaxRecent) list.RemoveRange(MaxRecent, list.Count - MaxRecent);
            Save(list);
            return list;
        }

        public static List<LaunchProfile> Remove(LaunchProfile profile)
        {
            var list = Load();
            list.RemoveAll(p => p.IsSameAs(profile));
            Save(list);
            return list;
        }
    }

    public static class ProgramLocator
    {
        public static List<ProgramPreset> FindPresets()
        {
            var presets = new List<ProgramPreset>();
            presets.AddRange(FindVisualStudio());

            string code = FindVSCode();
            if (code != null) presets.Add(new ProgramPreset { Name = "Visual Studio Code", Path = code, Kind = ProgramKind.VSCode });

            presets.Add(new ProgramPreset { Name = "Command Prompt (cmd.exe)", Path = Path.Combine(Environment.SystemDirectory, "cmd.exe"), Kind = ProgramKind.Console });

            string pwsh = FindOnPath("pwsh.exe");
            if (pwsh != null) presets.Add(new ProgramPreset { Name = "PowerShell 7 (pwsh.exe)", Path = pwsh, DefaultArguments = "-NoExit", Kind = ProgramKind.Console });
            string winPs = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
            if (File.Exists(winPs)) presets.Add(new ProgramPreset { Name = "Windows PowerShell", Path = winPs, DefaultArguments = "-NoExit", Kind = ProgramKind.Console });

            presets.Add(new ProgramPreset { Name = "Custom program (.exe)...", Path = "", Kind = ProgramKind.Custom });
            return presets;
        }

        private static IEnumerable<ProgramPreset> FindVisualStudio()
        {
            var result = new List<ProgramPreset>();
            try
            {
                string vswhere = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft Visual Studio\Installer\vswhere.exe");
                if (!File.Exists(vswhere)) return result;

                var psi = new ProcessStartInfo(vswhere, "-all -prerelease -products * -format json -utf8")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };
                using var p = Process.Start(psi);
                string json = p.StandardOutput.ReadToEnd();
                p.WaitForExit(10000);

                foreach (var item in JArray.Parse(json))
                {
                    string path = item["productPath"]?.ToString();
                    // Skip other products built on the VS shell (e.g. SSMS)
                    if (string.IsNullOrEmpty(path) || !File.Exists(path) || !Path.GetFileName(path).Equals("devenv.exe", StringComparison.OrdinalIgnoreCase)) continue;
                    string name = item["displayName"]?.ToString() ?? "Visual Studio";
                    string version = item["installationVersion"]?.ToString();
                    result.Add(new ProgramPreset { Name = string.IsNullOrEmpty(version) ? name : $"{name} ({version})", Path = path, Kind = ProgramKind.VisualStudio });
                }
            }
            catch { }
            return result.OrderByDescending(r => r.Name);
        }

        private static string FindVSCode()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Microsoft VS Code\Code.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft VS Code\Code.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft VS Code\Code.exe")
            };
            return candidates.FirstOrDefault(File.Exists);
        }

        private static string FindOnPath(string exe)
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string full = Path.Combine(dir.Trim(), exe);
                    if (File.Exists(full)) return full;
                }
                catch { }
            }
            return null;
        }
    }
}
