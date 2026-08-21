using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
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
using System.Linq;
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
        
        public static readonly string AppVersion = "v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

        public MainWindow()
        {
            InitializeComponent();
            Sessions = new ObservableCollection<CapturedSession>();
            LstSessions.ItemsSource = Sessions;
            TxtVersion.Text = AppVersion;
            
            MockRulesManager.LoadRules();

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

                    if (!string.IsNullOrEmpty(latestVersion))
                    {
                        string cleanLatest = latestVersion.TrimStart('v', 'V');
                        string cleanCurrent = AppVersion.TrimStart('v', 'V');

                        if (Version.TryParse(cleanLatest, out Version vLatest) && Version.TryParse(cleanCurrent, out Version vCurrent))
                        {
                            if (vLatest > vCurrent)
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

            bool isSystemProxy = ChkSystemProxy.IsChecked == true;
            proxyEngine.Start(port, isSystemProxy);
            TxtPort.IsEnabled = false;
            ChkSystemProxy.IsEnabled = false;
            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
            TxtStatus.Text = $"Proxy is running on 127.0.0.1:{port}" + (isSystemProxy ? " (System Proxy)" : "");
            TxtStatus.Foreground = System.Windows.Media.Brushes.Green;
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            proxyEngine.Stop();
            TxtPort.IsEnabled = true;
            ChkSystemProxy.IsEnabled = true;
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
            TxtReqHtml.Text = "";
            ImgReqImage.Source = null;
            TxtReqXmlFormatted.Text = "";
            TxtReqJson.Text = "";

            TxtResHeaders.Text = "";
            TxtResRaw.Text = "";
            TxtResHtml.Text = "";
            ImgResImage.Source = null;
            TxtResXmlFormatted.Text = "";
            TxtResJson.Text = "";
            
            TxtReqSoapAction.Visibility = Visibility.Collapsed;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "SoapProxy Sessions (*.sps)|*.sps",
                DefaultExt = ".sps",
                FileName = $"Session_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string json = JsonConvert.SerializeObject(Sessions, Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(dlg.FileName, json);
                    MessageBox.Show($"Saved {Sessions.Count} sessions successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving sessions: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "SoapProxy Sessions (*.sps)|*.sps"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string json = File.ReadAllText(dlg.FileName);
                    var loaded = JsonConvert.DeserializeObject<List<CapturedSession>>(json);
                    if (loaded != null)
                    {
                        if (Sessions.Count > 0)
                        {
                            var result = MessageBox.Show("Do you want to clear the current list before loading?", "Clear List", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                            if (result == MessageBoxResult.Cancel) return;
                            if (result == MessageBoxResult.Yes) BtnClear_Click(null, null);
                        }
                        
                        foreach (var s in loaded)
                        {
                            Sessions.Add(s);
                        }
                        MessageBox.Show($"Loaded {loaded.Count} sessions successfully.", "Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading sessions: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MenuSaveSelected_Click(object sender, RoutedEventArgs e)
        {
            if (LstSessions.SelectedItems.Count == 0) return;

            var dlg = new SaveFileDialog
            {
                Filter = "SoapProxy Sessions (*.sps)|*.sps",
                DefaultExt = ".sps",
                FileName = $"SelectedSessions_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var selected = LstSessions.SelectedItems.Cast<CapturedSession>().ToList();
                    string json = JsonConvert.SerializeObject(selected, Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(dlg.FileName, json);
                    MessageBox.Show($"Saved {selected.Count} selected sessions successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving sessions: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MenuLoad_Click(object sender, RoutedEventArgs e)
        {
            BtnLoad_Click(sender, e);
        }

        private void DeleteSelectedSessions()
        {
            if (LstSessions.SelectedItems.Count > 0)
            {
                var selected = LstSessions.SelectedItems.Cast<CapturedSession>().ToList();
                foreach (var item in selected)
                {
                    Sessions.Remove(item);
                }
                if (Sessions.Count == 0)
                {
                    BtnClear_Click(null, null);
                }
            }
        }

        private void MenuDelete_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedSessions();
        }

        private void LstSessions_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                DeleteSelectedSessions();
            }
        }

        private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filterText = TxtFilter.Text.ToLower();
            if (string.IsNullOrWhiteSpace(filterText))
            {
                System.Windows.Data.CollectionViewSource.GetDefaultView(Sessions).Filter = null;
            }
            else
            {
                System.Windows.Data.CollectionViewSource.GetDefaultView(Sessions).Filter = item =>
                {
                    if (item is CapturedSession s)
                    {
                        return (s.Url?.ToLower().Contains(filterText) == true) ||
                               (s.ProcessName?.ToLower().Contains(filterText) == true) ||
                               (s.Method?.ToLower().Contains(filterText) == true) ||
                               (s.StatusCode.ToString().Contains(filterText) == true);
                    }
                    return false;
                };
            }
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

        private void BtnMock_Click(object sender, RoutedEventArgs e)
        {
            var win = new MockRulesManagerWindow() { Owner = this };
            win.ShowDialog();
        }

        private void MenuMock_Click(object sender, RoutedEventArgs e)
        {
            if (LstSessions.SelectedItem is CapturedSession session)
            {
                var newRule = new MockRule
                {
                    UrlMatch = session.Url.Length > 50 ? session.Url.Substring(0, 50) : session.Url,
                    StatusCode = session.StatusCode > 0 ? session.StatusCode : 200,
                    ResponseBody = session.ResponseBody ?? "",
                };
                
                // Extract Content-Type
                var ctLine = session.ResponseHeaders?.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                      .FirstOrDefault(h => h.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase));
                if (ctLine != null)
                {
                    newRule.ContentType = ctLine.Substring(ctLine.IndexOf(':') + 1).Trim();
                }

                var editor = new MockRuleEditorWindow(newRule) { Owner = this };
                if (editor.ShowDialog() == true)
                {
                    MockRulesManager.AddRule(editor.Rule);
                    var win = new MockRulesManagerWindow() { Owner = this };
                    win.ShowDialog();
                }
            }
        }

        private void MenuReplay_Click(object sender, RoutedEventArgs e)
        {
            if (LstSessions.SelectedItem is CapturedSession session)
            {
                if (int.TryParse(TxtPort.Text, out int port))
                {
                    var replayWindow = new ReplayWindow(session.Method, session.Url, session.RequestHeaders, session.RequestBody, port, (captured) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            Sessions.Add(captured);
                            // Scroll to the new item
                            LstSessions.ScrollIntoView(captured);
                            LstSessions.SelectedItem = captured;
                        });
                    })
                    {
                        Owner = this
                    };
                    replayWindow.ShowDialog();
                }
            }
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
                TxtReqHtml.Text = "";
                ImgReqImage.Source = null;
                TxtReqXmlFormatted.Text = ""; 
                TxtReqJson.Text = ""; 

                TxtResHeaders.Text = session.ResponseHeaders;
                TxtResRaw.Text = session.ResponseBody;
                TxtResHtml.Text = "";
                ImgResImage.Source = null;
                TxtResXmlFormatted.Text = "";
                TxtResJson.Text = ""; 

                ReqTabs.SelectedIndex = GetBestTabIndex(session.RequestHeaders, session.RequestBodyBytes);
                ResTabs.SelectedIndex = GetBestTabIndex(session.ResponseHeaders, session.ResponseBodyBytes);

                RefreshReqTab();
                RefreshResTab();
            }
        }

        private int GetBestTabIndex(string headers, byte[] bodyBytes)
        {
            if (bodyBytes == null || bodyBytes.Length == 0) return 0; // Headers
            if (string.IsNullOrWhiteSpace(headers)) return 1; // Raw

            string[] lines = headers.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string contentType = lines.FirstOrDefault(l => l.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase))?.ToLower();

            if (!string.IsNullOrEmpty(contentType))
            {
                if (contentType.Contains("image/")) return 3; // Image
                if (contentType.Contains("html")) return 2;   // HTML
                if (contentType.Contains("json")) return 5;   // JSON
                if (contentType.Contains("xml") || contentType.Contains("soap")) return 4; // XML
            }

            return 1; // Raw
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
                case 2: content = TxtReqHtml.Text; ext = "html"; filter = "HTML files (*.html)|*.html|All files (*.*)|*.*"; break;
                case 4: content = TxtReqXmlFormatted.Text; ext = "xml"; filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*"; break;
                case 5: content = TxtReqJson.Text; ext = "json"; filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"; break;
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
                case 2: content = TxtResHtml.Text; ext = "html"; filter = "HTML files (*.html)|*.html|All files (*.*)|*.*"; break;
                case 4: content = TxtResXmlFormatted.Text; ext = "xml"; filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*"; break;
                case 5: content = TxtResJson.Text; ext = "json"; filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"; break;
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
                if (ReqTabs.SelectedIndex == 2 && string.IsNullOrEmpty(TxtReqHtml.Text))
                {
                    TxtReqHtml.Text = session.RequestBody ?? "";
                }
                else if (ReqTabs.SelectedIndex == 3 && ImgReqImage.Source == null && session.RequestBodyBytes != null)
                {
                    ImgReqImage.Source = LoadImage(session.RequestBodyBytes);
                }
                else if (ReqTabs.SelectedIndex == 4 && string.IsNullOrEmpty(TxtReqXmlFormatted.Text))
                {
                    TxtReqXmlFormatted.Text = FormatXml(session.RequestBody);
                    xmlFoldingStrategy.UpdateFoldings(reqXmlFoldingManager, TxtReqXmlFormatted.Document);
                }
                else if (ReqTabs.SelectedIndex == 5 && string.IsNullOrEmpty(TxtReqJson.Text))
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
                if (ResTabs.SelectedIndex == 2 && string.IsNullOrEmpty(TxtResHtml.Text))
                {
                    TxtResHtml.Text = session.ResponseBody ?? "";
                }
                else if (ResTabs.SelectedIndex == 3 && ImgResImage.Source == null && session.ResponseBodyBytes != null)
                {
                    ImgResImage.Source = LoadImage(session.ResponseBodyBytes);
                }
                else if (ResTabs.SelectedIndex == 4 && string.IsNullOrEmpty(TxtResXmlFormatted.Text))
                {
                    TxtResXmlFormatted.Text = FormatXml(session.ResponseBody);
                    xmlFoldingStrategy.UpdateFoldings(resXmlFoldingManager, TxtResXmlFormatted.Document);
                }
                else if (ResTabs.SelectedIndex == 5 && string.IsNullOrEmpty(TxtResJson.Text))
                {
                    TxtResJson.Text = ConvertXmlToJson(session.ResponseBody);
                    jsonFoldingStrategy.UpdateFoldings(resJsonFoldingManager, TxtResJson.Document);
                }
            }
        }

        private BitmapImage LoadImage(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0) return null;
            try
            {
                var image = new BitmapImage();
                using (var mem = new System.IO.MemoryStream(imageData))
                {
                    mem.Position = 0;
                    image.BeginInit();
                    image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = null;
                    image.StreamSource = mem;
                    image.EndInit();
                }
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
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