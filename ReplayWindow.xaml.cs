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
        private readonly Action<CapturedSession> onComplete;

        public ReplayWindow(string method, string url, string headers, string body, int currentProxyPort, Action<CapturedSession> onCompleteCallback)
        {
            InitializeComponent();
            proxyPort = currentProxyPort;
            onComplete = onCompleteCallback;

            TxtMethod.Text = method;
            TxtUrl.Text = url;
            TxtHeaders.Text = headers;
            
            // Try to set syntax highlighting and format body based on headers
            if (headers?.ToLower().Contains("xml") == true || headers?.ToLower().Contains("soap") == true)
            {
                TxtBody.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("XML");
                TxtBody.Text = FormatXml(body);
            }
            else if (headers?.ToLower().Contains("json") == true)
            {
                TxtBody.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("JavaScript");
                TxtBody.Text = FormatJson(body);
            }
            else
            {
                TxtBody.Text = body;
            }
        }

        private string FormatXml(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml)) return "";
            try { return System.Xml.Linq.XDocument.Parse(xml).ToString(); }
            catch { return xml; }
        }

        private string FormatJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return "";
            try { return Newtonsoft.Json.Linq.JToken.Parse(json).ToString(Newtonsoft.Json.Formatting.Indented); }
            catch { return json; }
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
                // We use HttpClient directly, bypassing the proxy because Titanium ignores requests from its own process!
                var handler = new HttpClientHandler
                {
                    UseProxy = false, // Direct connection to target
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.Brotli | System.Net.DecompressionMethods.None
                };

                using (var client = new HttpClient(handler))
                {
                    var request = new HttpRequestMessage(new HttpMethod(TxtMethod.Text.Trim()), TxtUrl.Text.Trim());

                    // Parse Headers
                    string[] headerLines = TxtHeaders.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    var contentHeaders = new System.Collections.Generic.Dictionary<string, string>();

                    foreach (var line in headerLines)
                    {
                        int colonIdx = line.IndexOf(':');
                        if (colonIdx > 0)
                        {
                            string key = line.Substring(0, colonIdx).Trim();
                            string val = line.Substring(colonIdx + 1).Trim();

                            if (key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
                                contentHeaders[key] = val;
                            else
                            {
                                if (key.Equals("Host", StringComparison.OrdinalIgnoreCase)) continue;
                                if (key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)) continue;
                                if (key.Equals("Accept-Encoding", StringComparison.OrdinalIgnoreCase)) continue;
                                request.Headers.TryAddWithoutValidation(key, val);
                            }
                        }
                    }

                    byte[] reqBytes = null;
                    if (!string.IsNullOrWhiteSpace(TxtBody.Text))
                    {
                        reqBytes = Encoding.UTF8.GetBytes(TxtBody.Text);
                        request.Content = new ByteArrayContent(reqBytes);
                        if (contentHeaders.TryGetValue("Content-Type", out string contentType))
                            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
                    }

                    // CHECK MOCK RULES FIRST
                    var mockRule = MockRulesManager.Rules?.FirstOrDefault(r => r.IsEnabled && TxtUrl.Text.IndexOf(r.UrlMatch, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (mockRule != null)
                    {
                        var capturedMock = new CapturedSession
                        {
                            Url = TxtUrl.Text.Trim(),
                            Method = TxtMethod.Text.Trim(),
                            StatusCode = mockRule.StatusCode,
                            RequestHeaders = TxtHeaders.Text,
                            RequestBody = TxtBody.Text,
                            RequestBodyBytes = reqBytes,
                            ResponseHeaders = $"Content-Type: {mockRule.ContentType}",
                            ResponseBody = mockRule.ResponseBody ?? "",
                            ResponseBodyBytes = Encoding.UTF8.GetBytes(mockRule.ResponseBody ?? ""),
                            ProcessName = "SoapProxyApp (Replay Mocked)",
                            Timestamp = DateTime.Now
                        };

                        onComplete?.Invoke(capturedMock);
                        Close();
                        return;
                    }

                    var response = await client.SendAsync(request);
                    byte[] resBytes = await response.Content.ReadAsByteArrayAsync();
                    string resString = Encoding.UTF8.GetString(resBytes);

                    var resHeaders = string.Join(Environment.NewLine, response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"));
                    if (response.Content.Headers.Any())
                    {
                        resHeaders += Environment.NewLine + string.Join(Environment.NewLine, response.Content.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"));
                    }

                    var captured = new CapturedSession
                    {
                        Url = TxtUrl.Text.Trim(),
                        Method = TxtMethod.Text.Trim(),
                        StatusCode = (int)response.StatusCode,
                        RequestHeaders = TxtHeaders.Text,
                        RequestBody = TxtBody.Text,
                        RequestBodyBytes = reqBytes,
                        ResponseHeaders = resHeaders.Trim(),
                        ResponseBody = resString,
                        ResponseBodyBytes = resBytes,
                        ProcessName = "SoapProxyApp (Replay)",
                        Timestamp = DateTime.Now
                    };

                    onComplete?.Invoke(captured);
                }
                
                Close();
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
