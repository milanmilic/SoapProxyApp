using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Folding;

namespace SoapProxyApp
{
    public partial class MainWindow : Window
    {
        private ProxyEngine proxyEngine;
        public ObservableCollection<CapturedSession> Sessions { get; set; }
        private bool isDarkMode = false;
        private IHighlightingDefinition lightXml;
        private IHighlightingDefinition darkXml;
        private IHighlightingDefinition lightJson;
        private IHighlightingDefinition darkJson;

        private FoldingManager reqXmlFoldingManager;
        private FoldingManager resXmlFoldingManager;
        private FoldingManager reqJsonFoldingManager;
        private FoldingManager resJsonFoldingManager;

        private XmlFoldingStrategy xmlFoldingStrategy = new XmlFoldingStrategy();
        private BraceFoldingStrategy jsonFoldingStrategy = new BraceFoldingStrategy();
        public const string AppVersion = "v1.5.0";

        public MainWindow()
        {
            InitializeComponent();
            Sessions = new ObservableCollection<CapturedSession>();
            LstSessions.ItemsSource = Sessions;
            TxtVersion.Text = AppVersion;

            proxyEngine = new ProxyEngine();
            proxyEngine.OnSessionCompleted += ProxyEngine_OnSessionCompleted;

            LoadSyntaxDefinitions();

            TxtReqXmlFormatted.SyntaxHighlighting = lightXml;
            TxtReqJson.SyntaxHighlighting = lightJson;
            TxtResXmlFormatted.SyntaxHighlighting = lightXml;
            TxtResJson.SyntaxHighlighting = lightJson;

            reqXmlFoldingManager = FoldingManager.Install(TxtReqXmlFormatted.TextArea);
            resXmlFoldingManager = FoldingManager.Install(TxtResXmlFormatted.TextArea);
            reqJsonFoldingManager = FoldingManager.Install(TxtReqJson.TextArea);
            resJsonFoldingManager = FoldingManager.Install(TxtResJson.TextArea);

            _ = CheckForUpdatesAsync();
        }

        private async System.Threading.Tasks.Task CheckForUpdatesAsync()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "SoapProxyApp-Updater");
                    string response = await client.GetStringAsync("https://api.github.com/repos/milanmilic/SoapProxyApp/releases/latest");
                    var json = JObject.Parse(response);
                    string latestVersion = json["tag_name"]?.ToString();
                    string downloadUrl = json["html_url"]?.ToString();

                    if (!string.IsNullOrEmpty(latestVersion) && latestVersion != AppVersion)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            var result = MessageBox.Show($"A new version of SOAP Proxy App ({latestVersion}) is available!\n\nWould you like to download it now?", "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Information);
                            if (result == MessageBoxResult.Yes && !string.IsNullOrEmpty(downloadUrl))
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = downloadUrl,
                                    UseShellExecute = true
                                });
                            }
                        });
                    }
                }
            }
            catch { /* Ignore update check failures silently */ }
        }

        private void LoadSyntaxDefinitions()
        {
            try
            {
                using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("SoapProxyApp.Themes.LightXML.xshd"))
                {
                    if (stream != null)
                        using (var reader = System.Xml.XmlReader.Create(stream))
                            lightXml = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }

                using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("SoapProxyApp.Themes.LightJSON.xshd"))
                {
                    if (stream != null)
                        using (var reader = System.Xml.XmlReader.Create(stream))
                            lightJson = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
                using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("SoapProxyApp.Themes.DarkXML.xshd"))
                {
                    if (stream != null)
                        using (var reader = System.Xml.XmlReader.Create(stream))
                            darkXml = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }

                using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("SoapProxyApp.Themes.DarkJSON.xshd"))
                {
                    if (stream != null)
                        using (var reader = System.Xml.XmlReader.Create(stream))
                            darkJson = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }
            catch { }
        }

        private void BtnTheme_Click(object sender, RoutedEventArgs e)
        {
            isDarkMode = !isDarkMode;
            if (isDarkMode)
            {
                BtnTheme.Content = "☀️ Light Mode";
                var dict = new ResourceDictionary { Source = new Uri("Themes/DarkTheme.xaml", UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Clear();
                Application.Current.Resources.MergedDictionaries.Add(dict);

                TxtReqXmlFormatted.SyntaxHighlighting = darkXml ?? lightXml;
                TxtReqJson.SyntaxHighlighting = darkJson ?? lightJson;
                TxtResXmlFormatted.SyntaxHighlighting = darkXml ?? lightXml;
                TxtResJson.SyntaxHighlighting = darkJson ?? lightJson;
            }
            else
            {
                BtnTheme.Content = "🌙 Dark Mode";
                var dict = new ResourceDictionary { Source = new Uri("Themes/LightTheme.xaml", UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Clear();
                Application.Current.Resources.MergedDictionaries.Add(dict);

                TxtReqXmlFormatted.SyntaxHighlighting = lightXml;
                TxtReqJson.SyntaxHighlighting = lightJson;
                TxtResXmlFormatted.SyntaxHighlighting = lightXml;
                TxtResJson.SyntaxHighlighting = lightJson;
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtPort.Text, out int port))
            {
                MessageBox.Show("Please enter a valid port number.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            proxyEngine.Start(port);
            TxtPort.IsEnabled = false;
            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
            TxtStatus.Text = $"Proxy is running on 127.0.0.1:{port}";
            TxtStatus.Foreground = System.Windows.Media.Brushes.Green;
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            proxyEngine.Stop();
            TxtPort.IsEnabled = true;
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
            TxtStatus.Text = "Proxy is stopped.";
            TxtStatus.Foreground = System.Windows.Media.Brushes.Red;
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            Sessions.Clear();

            TxtReqHeaders.Text = "";
            TxtReqRaw.Text = "";
            TxtReqXmlFormatted.Text = "";
            TxtReqJson.Text = "";

            TxtResHeaders.Text = "";
            TxtResRaw.Text = "";
            TxtResXmlFormatted.Text = "";
            TxtResJson.Text = "";
        }

        private void BtnExportCert_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var cert = proxyEngine.GetRootCertificate();
                if (cert == null)
                {
                    MessageBox.Show("You must click 'Start Proxy' first to generate the certificate.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    using (System.Security.Cryptography.X509Certificates.X509Store store = new System.Security.Cryptography.X509Certificates.X509Store(System.Security.Cryptography.X509Certificates.StoreName.Root, System.Security.Cryptography.X509Certificates.StoreLocation.LocalMachine))
                    {
                        store.Open(System.Security.Cryptography.X509Certificates.OpenFlags.ReadWrite);
                        store.Add(cert);
                        store.Close();
                    }
                    MessageBox.Show("Certificate successfully installed directly into Local Machine -> Trusted Root Certification Authorities!\n\nYour web applications now automatically trust the proxy.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (System.Security.Cryptography.CryptographicException)
                {
                    string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ProxyCert.cer");
                    System.IO.File.WriteAllBytes(path, cert.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Cert));
                    MessageBox.Show($"You don't have administrator privileges for automatic installation.\n\nEither run this application as Administrator (Right click -> Run as Administrator) and click again, OR manually install the file that was just saved to your Desktop under the name 'ProxyCert.cer'.", "Administrator Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ProxyEngine_OnSessionCompleted(object sender, CapturedSession session)
        {
            Dispatcher.Invoke(() =>
            {
                Sessions.Add(session);
                // Scroll to bottom automatically
                LstSessions.ScrollIntoView(session);
            });
        }

        private string GetSafeSessionName(CapturedSession s, string suffix)
        {
            string app = string.IsNullOrWhiteSpace(s.ProcessName) ? "UnknownApp" : s.ProcessName;
            string action = "NoAction";
            if (!string.IsNullOrWhiteSpace(s.SoapAction))
            {
                action = s.SoapAction.Trim('\"');
                if (action.Contains("/"))
                    action = action.Substring(action.LastIndexOf('/') + 1);
            }
            string safeName = $"{app}_{action}_{s.Timestamp:HHmmss}_{suffix}";
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(c, '_');
            }
            return safeName + ".xml";
        }

        private void MenuCompareReq_Click(object sender, RoutedEventArgs e)
        {
            if (LstSessions.SelectedItems.Count != 2)
            {
                MessageBox.Show("Please select exactly TWO requests to compare.", "Compare", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var s1 = (CapturedSession)LstSessions.SelectedItems[0];
            var s2 = (CapturedSession)LstSessions.SelectedItems[1];
            CompareDiff(FormatXml(s1.RequestBody), FormatXml(s2.RequestBody), GetSafeSessionName(s1, "Req1"), GetSafeSessionName(s2, "Req2"));
        }

        private void MenuCompareRes_Click(object sender, RoutedEventArgs e)
        {
            if (LstSessions.SelectedItems.Count != 2)
            {
                MessageBox.Show("Please select exactly TWO requests to compare.", "Compare", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var s1 = (CapturedSession)LstSessions.SelectedItems[0];
            var s2 = (CapturedSession)LstSessions.SelectedItems[1];
            CompareDiff(FormatXml(s1.ResponseBody), FormatXml(s2.ResponseBody), GetSafeSessionName(s1, "Res1"), GetSafeSessionName(s2, "Res2"));
        }

        private void CompareDiff(string text1, string text2, string name1, string name2)
        {
            try
            {
                string path1 = Path.Combine(Path.GetTempPath(), name1);
                string path2 = Path.Combine(Path.GetTempPath(), name2);
                File.WriteAllText(path1, text1 ?? "");
                File.WriteAllText(path2, text2 ?? "");

                // Try VS Code
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "code",
                        Arguments = $"-d \"{path1}\" \"{path2}\"",
                        UseShellExecute = true,
                        CreateNoWindow = true
                    });
                    return;
                }
                catch { }

                // Try WinMerge
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "winmergeu",
                        Arguments = $"\"{path1}\" \"{path2}\"",
                        UseShellExecute = true,
                        CreateNoWindow = true
                    });
                    return;
                }
                catch { }

                MessageBox.Show("Could not find 'code' (VS Code) or 'winmergeu' (WinMerge) in your system PATH.\nPlease ensure you have a diff tool installed and added to PATH.", "Compare Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error launching diff tool: {ex.Message}", "Compare Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LstSessions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstSessions.SelectedItem is CapturedSession session)
            {
                if (!string.IsNullOrWhiteSpace(session.SoapAction))
                {
                    TxtReqSoapAction.Text = $"SOAPAction: {session.SoapAction}";
                    TxtReqSoapAction.Visibility = Visibility.Visible;
                }
                else
                {
                    TxtReqSoapAction.Visibility = Visibility.Collapsed;
                }

                TxtReqHeaders.Text = session.RequestHeaders;
                TxtReqRaw.Text = session.RequestBody;
                TxtReqXmlFormatted.Text = ""; 
                TxtReqJson.Text = ""; 

                TxtResHeaders.Text = session.ResponseHeaders;
                TxtResRaw.Text = session.ResponseBody;
                TxtResXmlFormatted.Text = "";
                TxtResJson.Text = ""; 

                RefreshReqTab();
                RefreshResTab();
            }
        }

        private void ReqTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl) RefreshReqTab();
        }

        private void ResTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl) RefreshResTab();
        }

        private void BtnExportReq_Click(object sender, RoutedEventArgs e)
        {
            if (ReqTabs == null || LstSessions.SelectedItem == null) return;
            string content = "";
            string ext = "txt";
            string filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";

            switch (ReqTabs.SelectedIndex)
            {
                case 0: content = TxtReqHeaders.Text; break;
                case 1: content = TxtReqRaw.Text; ext = "xml"; filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*"; break;
                case 2: content = TxtReqXmlFormatted.Text; ext = "xml"; filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*"; break;
                case 3: content = TxtReqJson.Text; ext = "json"; filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"; break;
            }
            DoExport(content, ext, filter, "Request");
        }

        private void BtnExportRes_Click(object sender, RoutedEventArgs e)
        {
            if (ResTabs == null || LstSessions.SelectedItem == null) return;
            string content = "";
            string ext = "txt";
            string filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";

            switch (ResTabs.SelectedIndex)
            {
                case 0: content = TxtResHeaders.Text; break;
                case 1: content = TxtResRaw.Text; ext = "xml"; filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*"; break;
                case 2: content = TxtResXmlFormatted.Text; ext = "xml"; filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*"; break;
                case 3: content = TxtResJson.Text; ext = "json"; filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"; break;
            }
            DoExport(content, ext, filter, "Response");
        }

        private void DoExport(string content, string ext, string filter, string prefix)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                MessageBox.Show("No content to export.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog
            {
                FileName = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}",
                Filter = filter
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    System.IO.File.WriteAllText(sfd.FileName, content);
                    MessageBox.Show("File successfully saved!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RefreshReqTab()
        {
            if (LstSessions.SelectedItem is CapturedSession session && ReqTabs != null)
            {
                if (ReqTabs.SelectedIndex == 2 && string.IsNullOrEmpty(TxtReqXmlFormatted.Text))
                {
                    TxtReqXmlFormatted.Text = FormatXml(session.RequestBody);
                    xmlFoldingStrategy.UpdateFoldings(reqXmlFoldingManager, TxtReqXmlFormatted.Document);
                }
                else if (ReqTabs.SelectedIndex == 3 && string.IsNullOrEmpty(TxtReqJson.Text))
                {
                    TxtReqJson.Text = ConvertXmlToJson(session.RequestBody);
                    jsonFoldingStrategy.UpdateFoldings(reqJsonFoldingManager, TxtReqJson.Document);
                }
            }
        }

        private void RefreshResTab()
        {
            if (LstSessions.SelectedItem is CapturedSession session && ResTabs != null)
            {
                if (ResTabs.SelectedIndex == 2 && string.IsNullOrEmpty(TxtResXmlFormatted.Text))
                {
                    TxtResXmlFormatted.Text = FormatXml(session.ResponseBody);
                    xmlFoldingStrategy.UpdateFoldings(resXmlFoldingManager, TxtResXmlFormatted.Document);
                }
                else if (ResTabs.SelectedIndex == 3 && string.IsNullOrEmpty(TxtResJson.Text))
                {
                    TxtResJson.Text = ConvertXmlToJson(session.ResponseBody);
                    jsonFoldingStrategy.UpdateFoldings(resJsonFoldingManager, TxtResJson.Document);
                }
            }
        }

        private string FormatXml(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml)) return "Empty body.";
            try
            {
                XDocument doc = XDocument.Parse(xml);
                return doc.ToString();
            }
            catch (Exception)
            {
                return "Content is not valid XML.\n\n" + xml;
            }
        }

        private string ConvertXmlToJson(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml)) return "Empty body.";
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);
                return JsonConvert.SerializeXmlNode(doc, Newtonsoft.Json.Formatting.Indented);
            }
            catch (Exception ex)
            {
                return $"Unable to parse content as XML to JSON.\nError: {ex.Message}\n\nRAW:\n{xml}";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            proxyEngine.Stop();
            base.OnClosed(e);
        }
    }
}