using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace SoapProxyApp
{
    public class AppPoolInfo
    {
        public string Name { get; set; }
        public string State { get; set; }
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public bool IsAttached { get; set; }

        public string HttpsProxyValue => EnvironmentVariables.TryGetValue(ProxyEnvironment.HttpsProxy, out var v) ? v
            : EnvironmentVariables.TryGetValue(ProxyEnvironment.HttpProxy, out var h) ? h : "";
        public string AttachedText => IsAttached ? "✔ Attached" : (ProxyEnvironment.PointsToLocalProxy(HttpsProxyValue) ? "⚠ Not managed" : "");
    }

    public class AttachedPoolState
    {
        public string Name { get; set; }
        public int Port { get; set; }
        public DateTime AttachedAt { get; set; }
        // Original values of the variables before attaching; null means the variable did not exist.
        public Dictionary<string, string> OriginalValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Points IIS App Pools (w3wp) at the proxy by adding HTTP_PROXY/HTTPS_PROXY/NO_PROXY to the pool's
    /// environmentVariables (IIS 10+) via appcmd.exe. Attached pools are persisted to a state file before any
    /// change is made, so they can be restored after a crash.
    /// </summary>
    public static class IisAppPoolProxyManager
    {
        private const string Section = "system.applicationHost/applicationPools";
        private static readonly object sync = new();
        private static readonly string stateFile = Path.Combine(ProxyEnvironment.DataFolder, "iis_proxy_state.json");

        public static string AppCmdPath
        {
            get
            {
                string system = Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Sysnative")
                    : Environment.SystemDirectory;
                return Path.Combine(system, "inetsrv", "appcmd.exe");
            }
        }

        public static bool IsAvailable => File.Exists(AppCmdPath);

        public static List<AttachedPoolState> GetAttachedPools()
        {
            lock (sync) return LoadState();
        }

        public static bool HasAttachedPools => GetAttachedPools().Count > 0;

        public static List<AppPoolInfo> GetAppPools()
        {
            var output = RunAppCmd(out int exitCode, "list", "apppool", "/config:*", "/xml");
            if (exitCode != 0) throw new InvalidOperationException($"appcmd failed to list App Pools (exit code {exitCode}).{Environment.NewLine}{output}");

            var attached = GetAttachedPools().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var result = new List<AppPoolInfo>();
            var doc = XDocument.Parse(output.Trim());
            foreach (var pool in doc.Root.Elements("APPPOOL"))
            {
                var info = new AppPoolInfo
                {
                    Name = (string)pool.Attribute("APPPOOL.NAME"),
                    State = (string)pool.Attribute("state")
                };
                var envVars = pool.Element("add")?.Element("environmentVariables");
                if (envVars != null)
                {
                    foreach (var v in envVars.Elements("add"))
                    {
                        string name = (string)v.Attribute("name");
                        if (!string.IsNullOrEmpty(name)) info.EnvironmentVariables[name] = (string)v.Attribute("value") ?? "";
                    }
                }
                info.IsAttached = attached.Contains(info.Name);
                result.Add(info);
            }
            return result;
        }

        public static void Attach(string poolName, int port)
        {
            lock (sync)
            {
                var pool = GetAppPools().FirstOrDefault(p => p.Name.Equals(poolName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"App Pool '{poolName}' was not found.");

                var state = LoadState();
                var entry = state.FirstOrDefault(p => p.Name.Equals(pool.Name, StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                {
                    entry = new AttachedPoolState { Name = pool.Name };
                    foreach (var name in ProxyEnvironment.VariableNames)
                    {
                        entry.OriginalValues[name] = pool.EnvironmentVariables.TryGetValue(name, out var v) ? v : null;
                    }
                    state.Add(entry);
                }
                entry.Port = port;
                entry.AttachedAt = DateTime.Now;

                // Persist first, so a crash in the middle can still be cleaned up on next start.
                SaveState(state);

                entry.OriginalValues.TryGetValue(ProxyEnvironment.NoProxy, out string originalNoProxy);
                foreach (var kv in ProxyEnvironment.GetVariables(port, originalNoProxy))
                {
                    SetVariable(pool.Name, kv.Key, kv.Value, pool.EnvironmentVariables.ContainsKey(kv.Key));
                }
                Recycle(pool.Name);
            }
        }

        public static void Detach(string poolName)
        {
            lock (sync)
            {
                var state = LoadState();
                var entry = state.FirstOrDefault(p => p.Name.Equals(poolName, StringComparison.OrdinalIgnoreCase));
                var pool = GetAppPools().FirstOrDefault(p => p.Name.Equals(poolName, StringComparison.OrdinalIgnoreCase));

                if (pool != null)
                {
                    if (entry != null)
                    {
                        foreach (var name in ProxyEnvironment.VariableNames)
                        {
                            entry.OriginalValues.TryGetValue(name, out string original);
                            bool exists = pool.EnvironmentVariables.ContainsKey(name);
                            if (original == null)
                            {
                                if (exists) RemoveVariable(pool.Name, name);
                            }
                            else
                            {
                                SetVariable(pool.Name, name, original, exists);
                            }
                        }
                    }
                    else
                    {
                        // Not attached by this app (e.g. state file lost): only remove proxy entries that point to localhost.
                        foreach (var name in new[] { ProxyEnvironment.HttpProxy, ProxyEnvironment.HttpsProxy })
                        {
                            if (pool.EnvironmentVariables.TryGetValue(name, out var v) && ProxyEnvironment.PointsToLocalProxy(v))
                                RemoveVariable(pool.Name, name);
                        }
                    }
                    Recycle(pool.Name);
                }

                if (entry != null)
                {
                    state.Remove(entry);
                    SaveState(state);
                }
            }
        }

        /// <summary>Detaches every pool recorded in the state file. Returns error messages for pools that failed.</summary>
        public static List<string> DetachAll()
        {
            var errors = new List<string>();
            foreach (var entry in GetAttachedPools())
            {
                try { Detach(entry.Name); }
                catch (Exception ex) { errors.Add($"{entry.Name}: {ex.Message}"); }
            }
            return errors;
        }

        private static void SetVariable(string pool, string name, string value, bool exists)
        {
            // The "[name='..',value='..']" syntax cannot carry quotes, so values are always written with ".value:".
            if (!exists)
            {
                Run("set", "config", $"-section:{Section}", $"/+[name='{pool}'].environmentVariables.[name='{name}',value='-']", "/commit:apphost");
            }
            Run("set", "config", $"-section:{Section}", $"/[name='{pool}'].environmentVariables.[name='{name}'].value:{value}", "/commit:apphost");
        }

        private static void RemoveVariable(string pool, string name)
        {
            Run("set", "config", $"-section:{Section}", $"/-[name='{pool}'].environmentVariables.[name='{name}']", "/commit:apphost");
        }

        private static void Recycle(string pool)
        {
            // Fails for stopped pools; the new settings are picked up on the next start anyway.
            RunAppCmd(out _, "recycle", "apppool", $"/apppool.name:{pool}");
        }

        private static void Run(params string[] args)
        {
            string output = RunAppCmd(out int exitCode, args);
            if (exitCode != 0) throw new InvalidOperationException(output.Trim());
        }

        private static string RunAppCmd(out int exitCode, params string[] args)
        {
            if (!IsAvailable) throw new InvalidOperationException("IIS (appcmd.exe) is not installed on this machine.");

            var psi = new ProcessStartInfo(AppCmdPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var process = Process.Start(psi);
            var stdErrTask = process.StandardError.ReadToEndAsync();
            string stdOut = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(60000))
            {
                try { process.Kill(); } catch { }
                throw new TimeoutException("appcmd.exe did not respond within 60 seconds.");
            }
            exitCode = process.ExitCode;
            return stdOut + stdErrTask.Result;
        }

        private static List<AttachedPoolState> LoadState()
        {
            try
            {
                if (File.Exists(stateFile))
                {
                    return JsonConvert.DeserializeObject<List<AttachedPoolState>>(File.ReadAllText(stateFile)) ?? new();
                }
            }
            catch { }
            return new();
        }

        private static void SaveState(List<AttachedPoolState> state)
        {
            if (state.Count == 0)
            {
                if (File.Exists(stateFile)) File.Delete(stateFile);
                return;
            }
            File.WriteAllText(stateFile, JsonConvert.SerializeObject(state, Formatting.Indented));
        }
    }
}
