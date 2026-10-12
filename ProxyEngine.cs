using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Management;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;

namespace SoapProxyApp
{
    public class ProxyEngine
    {
        private ProxyServer proxyServer;
        private ExplicitProxyEndPoint explicitEndPoint;
        private static readonly ConcurrentDictionary<int, string> ProcessNameCache = new();

        public event EventHandler<CapturedSession> OnSessionCompleted;

        public void Start(int port, bool setAsSystemProxy = false)
        {
            // Make sure a previous instance is not left running
            Stop();

            try
            {
                proxyServer = new ProxyServer();
                proxyServer.CertificateManager.CreateRootCertificate();
                proxyServer.CertificateManager.TrustRootCertificate();

                proxyServer.BeforeRequest += OnRequest;
                proxyServer.BeforeResponse += OnResponse;
                proxyServer.AfterResponse += OnAfterResponse;

                // Set proxy on specified port
                explicitEndPoint = new ExplicitProxyEndPoint(IPAddress.Any, port, true);
                proxyServer.AddEndPoint(explicitEndPoint);
                proxyServer.Start();

                if (setAsSystemProxy)
                {
                    proxyServer.SetAsSystemHttpProxy(explicitEndPoint);
                    proxyServer.SetAsSystemHttpsProxy(explicitEndPoint);
                }
            }
            catch
            {
                // Clean up the half-initialized server (and system proxy, if it was set) so Start can be retried
                try { Stop(); } catch { proxyServer = null; }
                throw;
            }
        }

        public System.Security.Cryptography.X509Certificates.X509Certificate2 GetRootCertificate()
        {
            return proxyServer?.CertificateManager?.RootCertificate;
        }

        public void Stop()
        {
            if (proxyServer != null)
            {
                proxyServer.BeforeRequest -= OnRequest;
                proxyServer.BeforeResponse -= OnResponse;
                proxyServer.AfterResponse -= OnAfterResponse;
                try
                {
                    try
                    {
                        proxyServer.DisableAllSystemProxies();
                    }
                    catch { }

                    if (proxyServer.ProxyRunning)
                    {
                        proxyServer.Stop();
                    }
                }
                finally
                {
                    proxyServer.Dispose();
                    proxyServer = null;
                }
            }
        }

        private async Task OnRequest(object sender, SessionEventArgs e)
        {
            // Check for Mock Rules first
            if (MockRulesManager.Rules != null)
            {
                var mockRule = MockRulesManager.Rules.FirstOrDefault(r => r.IsEnabled && e.HttpClient.Request.Url.IndexOf(r.UrlMatch, StringComparison.OrdinalIgnoreCase) >= 0);
                if (mockRule != null)
                {
                    // Construct mock response
                    var fakeHeaders = new System.Collections.Generic.Dictionary<string, Titanium.Web.Proxy.Models.HttpHeader>();
                    fakeHeaders.Add("Content-Type", new Titanium.Web.Proxy.Models.HttpHeader("Content-Type", mockRule.ContentType));
                    
                    byte[] mockBytes = System.Text.Encoding.UTF8.GetBytes(mockRule.ResponseBody ?? "");
                    fakeHeaders.Add("Content-Length", new Titanium.Web.Proxy.Models.HttpHeader("Content-Length", mockBytes.Length.ToString()));

                    e.GenericResponse(
                        mockBytes, 
                        (System.Net.HttpStatusCode)mockRule.StatusCode, 
                        fakeHeaders);
                        
                    // Short-circuit: we don't proceed to read request or contact the server
                    
                    e.UserData = "MOCKED"; // Flag for OnResponse to ignore if it fires

                    // Build the session manually to show it in the UI
                    string processName = "Unknown";
                    if (e.HttpClient.ProcessId != null && e.HttpClient.ProcessId.Value > 0)
                    {
                        processName = GetProcessName(e.HttpClient.ProcessId.Value);
                    }
                    processName += " [MOCKED]";

                    string reqBody = "";
                    byte[] reqBytes = null;
                    if (e.HttpClient.Request.HasBody)
                    {
                        try {
                            reqBytes = e.HttpClient.Request.Body;
                            if (reqBytes != null) reqBody = System.Text.Encoding.UTF8.GetString(reqBytes);
                        } catch { }
                    }

                    var session = new CapturedSession
                    {
                        Url = e.HttpClient.Request.Url,
                        Method = e.HttpClient.Request.Method,
                        StatusCode = mockRule.StatusCode,
                        RequestHeaders = string.Join(Environment.NewLine, e.HttpClient.Request.Headers.Select(h => $"{h.Name}: {h.Value}")),
                        RequestBody = reqBody,
                        RequestBodyBytes = reqBytes,
                        ResponseHeaders = $"Content-Type: {mockRule.ContentType}",
                        ResponseBody = mockRule.ResponseBody ?? "",
                        ResponseBodyBytes = System.Text.Encoding.UTF8.GetBytes(mockRule.ResponseBody ?? ""),
                        ProcessName = processName,
                        Timestamp = DateTime.Now
                    };

                    OnSessionCompleted?.Invoke(this, session);
                    return;
                }
            }

            // Read request body for normal requests with payload size check (20 MB safety limit)
            if (e.HttpClient.Request.HasBody && e.HttpClient.Request.ContentLength < 20 * 1024 * 1024)
            {
                await e.GetRequestBodyAsString();
            }
        }

        private string GetProcessName(int pid)
        {
            if (pid <= 0) return "Unknown";
            if (ProcessNameCache.TryGetValue(pid, out string cachedName))
            {
                return cachedName;
            }

            string processName = "Unknown";
            try
            {
                var proc = System.Diagnostics.Process.GetProcessById(pid);
                processName = proc.ProcessName;

                // If it's an IIS worker process, extract the Application Pool name via WMI
                if (processName.Equals("w3wp", StringComparison.OrdinalIgnoreCase))
                {
                    using (var searcher = new System.Management.ManagementObjectSearcher($"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}"))
                    {
                        foreach (System.Management.ManagementObject obj in searcher.Get())
                        {
                            string cmdLine = obj["CommandLine"]?.ToString();
                            if (!string.IsNullOrEmpty(cmdLine))
                            {
                                int apIndex = cmdLine.IndexOf("-ap \"", StringComparison.OrdinalIgnoreCase);
                                if (apIndex >= 0)
                                {
                                    int start = apIndex + 5;
                                    int end = cmdLine.IndexOf("\"", start);
                                    if (end > start)
                                    {
                                        processName = cmdLine.Substring(start, end - start);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { /* Ignore process access exceptions */ }

            ProcessNameCache.TryAdd(pid, processName);
            return processName;
        }

        private async Task OnResponse(object sender, SessionEventArgs e)
        {
            if (e.UserData as string == "MOCKED") return;

            // When response is complete, read it with size limit protection (20 MB limit)
            if (e.HttpClient.Response.HasBody && e.HttpClient.Response.ContentLength < 20 * 1024 * 1024)
            {
                await e.GetResponseBodyAsString();
            }

            var soapActionHeader = e.HttpClient.Request.Headers.FirstOrDefault(h => h.Name.Equals("SOAPAction", StringComparison.OrdinalIgnoreCase));
            string processName = "Unknown";
            if (e.HttpClient.ProcessId != null && e.HttpClient.ProcessId.Value > 0)
            {
                processName = GetProcessName(e.HttpClient.ProcessId.Value);
            }

            string responseBody = e.HttpClient.Response.IsBodyRead ? e.HttpClient.Response.BodyString : "";
            if (string.IsNullOrEmpty(responseBody) && e.HttpClient.Response.HasBody && e.HttpClient.Response.ContentLength >= 20 * 1024 * 1024)
            {
                responseBody = $"[Payload exceeds 20 MB ({e.HttpClient.Response.ContentLength / (1024 * 1024.0):F1} MB) - body skipped to preserve memory]";
            }

            var session = new CapturedSession
            {
                Url = e.HttpClient.Request.Url,
                Method = e.HttpClient.Request.Method,
                StatusCode = e.HttpClient.Response.StatusCode,
                RequestHeaders = string.Join(Environment.NewLine, e.HttpClient.Request.Headers.Select(h => $"{h.Name}: {h.Value}")),
                RequestBody = e.HttpClient.Request.IsBodyRead ? e.HttpClient.Request.BodyString : "",
                RequestBodyBytes = e.HttpClient.Request.IsBodyRead ? e.HttpClient.Request.Body : null,
                ResponseHeaders = string.Join(Environment.NewLine, e.HttpClient.Response.Headers.Select(h => $"{h.Name}: {h.Value}")),
                ResponseBody = responseBody,
                ResponseBodyBytes = e.HttpClient.Response.IsBodyRead ? e.HttpClient.Response.Body : null,
                SoapAction = soapActionHeader?.Value,
                ProcessName = processName
            };

            e.UserData = "CAPTURED";

            // Send captured session to the UI
            OnSessionCompleted?.Invoke(this, session);
        }

        private Task OnAfterResponse(object sender, SessionEventArgs e)
        {
            // If already processed in mock rule or OnResponse, skip
            if (e.UserData as string == "MOCKED" || e.UserData as string == "CAPTURED") return Task.CompletedTask;

            // Capture connection failures, timeouts, DNS errors (where StatusCode == 0 or exception thrown)
            if (e.Exception != null || e.HttpClient.Response == null || e.HttpClient.Response.StatusCode == 0)
            {
                string processName = "Unknown";
                if (e.HttpClient.ProcessId != null && e.HttpClient.ProcessId.Value > 0)
                {
                    processName = GetProcessName(e.HttpClient.ProcessId.Value);
                }

                string errorDetails = e.Exception != null
                    ? $"{e.Exception.GetType().Name}: {e.Exception.Message}" + (e.Exception.InnerException != null ? $"{Environment.NewLine}Inner: {e.Exception.InnerException.GetType().Name}: {e.Exception.InnerException.Message}" : "")
                    : "Upstream server closed connection or failed to respond.";

                var session = new CapturedSession
                {
                    Url = e.HttpClient.Request.Url,
                    Method = e.HttpClient.Request.Method,
                    StatusCode = 0,
                    RequestHeaders = string.Join(Environment.NewLine, e.HttpClient.Request.Headers.Select(h => $"{h.Name}: {h.Value}")),
                    RequestBody = e.HttpClient.Request.IsBodyRead ? e.HttpClient.Request.BodyString : "",
                    RequestBodyBytes = e.HttpClient.Request.IsBodyRead ? e.HttpClient.Request.Body : null,
                    ResponseHeaders = $"X-Proxy-Status: Failed{Environment.NewLine}X-Proxy-Error: {e.Exception?.Message ?? "No response"}",
                    ResponseBody = $"[Proxy Error - Connection Failed]{Environment.NewLine}{errorDetails}",
                    ResponseBodyBytes = System.Text.Encoding.UTF8.GetBytes($"[Proxy Error] {errorDetails}"),
                    ProcessName = processName,
                    Timestamp = DateTime.Now
                };

                e.UserData = "CAPTURED";
                OnSessionCompleted?.Invoke(this, session);
            }

            return Task.CompletedTask;
        }

        public static void DisableSystemProxySettings()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);
                if (key != null)
                {
                    key.SetValue("ProxyEnable", 0, Microsoft.Win32.RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        public static bool IsSystemProxyConfiguredLocally(out string configuredProxy)
        {
            configuredProxy = null;
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", false);
                if (key != null)
                {
                    int proxyEnable = (int)(key.GetValue("ProxyEnable") ?? 0);
                    string server = key.GetValue("ProxyServer") as string;
                    if (proxyEnable == 1 && !string.IsNullOrEmpty(server))
                    {
                        if (server.Contains("127.0.0.1") || server.Contains("localhost"))
                        {
                            configuredProxy = server;
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }
    }
}
