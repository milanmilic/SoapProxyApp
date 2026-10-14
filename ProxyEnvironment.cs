using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SoapProxyApp
{
    /// <summary>
    /// Builds the HTTP_PROXY / HTTPS_PROXY / NO_PROXY variables that route a single process
    /// (and its child processes) through SoapProxyApp without touching the Windows System Proxy.
    /// </summary>
    public static class ProxyEnvironment
    {
        public const string HttpProxy = "HTTP_PROXY";
        public const string HttpsProxy = "HTTPS_PROXY";
        public const string NoProxy = "NO_PROXY";

        public static readonly string[] VariableNames = { HttpProxy, HttpsProxy, NoProxy };
        public static readonly string[] DefaultBypass = { "localhost", "127.0.0.1", "::1" };

        public static string DataFolder
        {
            get
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoapProxyApp");
                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        public static string GetProxyUrl(int port) => $"http://127.0.0.1:{port}";

        public static Dictionary<string, string> GetVariables(int port, string existingNoProxy = null)
        {
            string url = GetProxyUrl(port);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [HttpProxy] = url,
                [HttpsProxy] = url,
                [NoProxy] = MergeNoProxy(existingNoProxy)
            };
        }

        // Keeps any bypass entries the user already had and adds the loopback ones.
        public static string MergeNoProxy(string existing)
        {
            var entries = new List<string>();
            if (!string.IsNullOrWhiteSpace(existing))
            {
                entries.AddRange(existing.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
            foreach (var host in DefaultBypass)
            {
                if (!entries.Contains(host, StringComparer.OrdinalIgnoreCase)) entries.Add(host);
            }
            return string.Join(",", entries);
        }

        public static void ApplyTo(ProcessStartInfo psi, int port)
        {
            psi.Environment.TryGetValue(NoProxy, out string existingNoProxy);
            foreach (var kv in GetVariables(port, existingNoProxy))
            {
                psi.Environment[kv.Key] = kv.Value;
            }
        }

        public static string Describe(int port)
        {
            return string.Join(Environment.NewLine, GetVariables(port, Environment.GetEnvironmentVariable(NoProxy)).Select(kv => $"{kv.Key}={kv.Value}"));
        }

        public static bool PointsToLocalProxy(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return value.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase) || value.Contains("localhost", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Remembers processes started via "Launch with Proxy" so the user can be warned when the proxy stops
    /// while they are still running (their HTTP calls would fail).
    /// </summary>
    public static class ProxyLaunchTracker
    {
        private static readonly List<(Process Process, string Name)> launched = new();
        private static readonly object sync = new();

        public static void Add(Process process, string name)
        {
            lock (sync) launched.Add((process, name));
        }

        public static List<string> GetRunning()
        {
            lock (sync)
            {
                launched.RemoveAll(p =>
                {
                    try { return p.Process.HasExited; } catch { return true; }
                });
                return launched.Select(p => p.Name).Distinct().ToList();
            }
        }
    }
}
