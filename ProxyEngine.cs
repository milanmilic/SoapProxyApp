using System;
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

        public event EventHandler<CapturedSession> OnSessionCompleted;

        public void Start(int port, bool setAsSystemProxy = false)
        {
            proxyServer = new ProxyServer();
            proxyServer.CertificateManager.CreateRootCertificate();
            proxyServer.CertificateManager.TrustRootCertificate();

            proxyServer.BeforeRequest += OnRequest;
            proxyServer.BeforeResponse += OnResponse;

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
                proxyServer.Stop();
                proxyServer.Dispose();
                proxyServer = null;
            }
        }

        private async Task OnRequest(object sender, SessionEventArgs e)
        {
            // Read request body
            if (e.HttpClient.Request.HasBody)
            {
                await e.GetRequestBodyAsString();
            }
        }

        private async Task OnResponse(object sender, SessionEventArgs e)
        {
            // When response is complete, read it and package into our model
            if (e.HttpClient.Response.HasBody)
            {
                await e.GetResponseBodyAsString();
            }

            var soapActionHeader = e.HttpClient.Request.Headers.FirstOrDefault(h => h.Name.Equals("SOAPAction", StringComparison.OrdinalIgnoreCase));
            string processName = "Unknown";
            try
            {
                int pid = e.HttpClient.ProcessId.Value;
                if (pid > 0)
                {
                    processName = System.Diagnostics.Process.GetProcessById(pid).ProcessName;

                    // If it's an IIS worker process, extract the Application Pool name via WMI
                    if (processName.Equals("w3wp", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var searcher = new ManagementObjectSearcher($"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}"))
                        {
                            foreach (ManagementObject obj in searcher.Get())
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
            }
            catch { /* Ignore process access exceptions */ }

            var session = new CapturedSession
            {
                Url = e.HttpClient.Request.Url,
                Method = e.HttpClient.Request.Method,
                StatusCode = e.HttpClient.Response.StatusCode,
                RequestHeaders = string.Join(Environment.NewLine, e.HttpClient.Request.Headers.Select(h => $"{h.Name}: {h.Value}")),
                RequestBody = e.HttpClient.Request.IsBodyRead ? e.HttpClient.Request.BodyString : "",
                RequestBodyBytes = e.HttpClient.Request.IsBodyRead ? e.HttpClient.Request.Body : null,
                ResponseHeaders = string.Join(Environment.NewLine, e.HttpClient.Response.Headers.Select(h => $"{h.Name}: {h.Value}")),
                ResponseBody = e.HttpClient.Response.IsBodyRead ? e.HttpClient.Response.BodyString : "",
                ResponseBodyBytes = e.HttpClient.Response.IsBodyRead ? e.HttpClient.Response.Body : null,
                SoapAction = soapActionHeader?.Value,
                ProcessName = processName
            };

            // Send captured session to the UI
            OnSessionCompleted?.Invoke(this, session);
        }
    }
}
