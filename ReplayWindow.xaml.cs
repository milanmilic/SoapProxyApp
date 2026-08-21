using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Windows;

namespace SoapProxyApp
{
    public partial class ReplayWindow : Window
    {
        private readonly int proxyPort;

        public ReplayWindow(string method, string url, string headers, string body, int currentProxyPort)
        {
            InitializeComponent();
            proxyPort = currentProxyPort;

            TxtMethod.Text = method;
            TxtUrl.Text = url;
            TxtHeaders.Text = headers;
            TxtBody.Text = body;
            
            // Try to set syntax highlighting based on headers
            if (headers?.ToLower().Contains("xml") == true || headers?.ToLower().Contains("soap") == true)
            {
                TxtBody.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("XML");
            }
            else if (headers?.ToLower().Contains("json") == true)
            {
                TxtBody.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("JavaScript");
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            BtnSend.IsEnabled = false;
            BtnSend.Content = "Sending...";

            try
            {
                // Configure HttpClient to route through our own proxy
                var handler = new HttpClientHandler
                {
                    Proxy = new System.Net.WebProxy($"http://127.0.0.1:{proxyPort}"),
                    UseProxy = true,
                    // Ignore certificate errors since our proxy uses a self-signed root
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true 
                };

                using (var client = new HttpClient(handler))
                {
                    var request = new HttpRequestMessage(new HttpMethod(TxtMethod.Text.Trim()), TxtUrl.Text.Trim());

                    // Parse Headers
                    string[] headerLines = TxtHeaders.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    // We must separate Content headers from Request headers
                    var contentHeaders = new System.Collections.Generic.Dictionary<string, string>();

                    foreach (var line in headerLines)
                    {
                        int colonIdx = line.IndexOf(':');
                        if (colonIdx > 0)
                        {
                            string key = line.Substring(0, colonIdx).Trim();
                            string val = line.Substring(colonIdx + 1).Trim();

                            // HttpClient is strict: content headers must be added to HttpContent, not HttpRequestMessage
                            if (key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
                            {
                                contentHeaders[key] = val;
                            }
                            else
                            {
                                // Skip host header, HttpClient sets it automatically from URL
                                if (key.Equals("Host", StringComparison.OrdinalIgnoreCase)) continue;
                                // Skip chunked encoding headers
                                if (key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)) continue;

                                request.Headers.TryAddWithoutValidation(key, val);
                            }
                        }
                    }

                    // Attach Body if POST/PUT/etc
                    if (!string.IsNullOrWhiteSpace(TxtBody.Text))
                    {
                        // We use the exact raw text. Default to UTF8.
                        request.Content = new StringContent(TxtBody.Text, Encoding.UTF8);

                        // Apply the exact Content-Type if it was present
                        if (contentHeaders.TryGetValue("Content-Type", out string contentType))
                        {
                            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
                        }
                        
                        // We don't manually set Content-Length because StringContent does it automatically,
                        // and forcing it can cause exceptions in HttpClient.
                    }

                    // Send the request (fire and forget from the UI perspective, as the proxy will catch it)
                    _ = await client.SendAsync(request);
                }
                
                Close(); // Close the window after sending
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to send request:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnSend.IsEnabled = true;
                BtnSend.Content = "Send Request";
            }
        }
    }
}
