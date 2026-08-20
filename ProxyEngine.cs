using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
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

        public void Start(int port)
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

            var session = new CapturedSession
            {
                Url = e.HttpClient.Request.Url,
                Method = e.HttpClient.Request.Method,
                StatusCode = e.HttpClient.Response.StatusCode,
                RequestHeaders = string.Join(Environment.NewLine, e.HttpClient.Request.Headers.Select(h => $"{h.Name}: {h.Value}")),
                RequestBody = e.HttpClient.Request.IsBodyRead ? e.HttpClient.Request.BodyString : "",
                ResponseHeaders = string.Join(Environment.NewLine, e.HttpClient.Response.Headers.Select(h => $"{h.Name}: {h.Value}")),
                ResponseBody = e.HttpClient.Response.IsBodyRead ? e.HttpClient.Response.BodyString : ""
            };

            // Send captured session to the UI
            OnSessionCompleted?.Invoke(this, session);
        }
    }
}
