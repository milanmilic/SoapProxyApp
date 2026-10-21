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
using ICSharpCode.AvalonEdit.Search;

namespace SoapProxyApp
{
    public partial class MainWindow : Window
    {
        private ProxyEngine proxyEngine;
        private int runningPort;
        private AppSettings appSettings;
        private bool isLoadingSettings;
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

            SearchPanel.Install(TxtReqXmlFormatted);
            SearchPanel.Install(TxtReqJson);
            SearchPanel.Install(TxtResXmlFormatted);
            SearchPanel.Install(TxtResJson);

            LoadAndApplyUserSettings();
            Loaded += MainWindow_Loaded;

            _ = CheckForUpdatesAsync();
            CheckStaleSystemProxy();
            CheckStaleIisAppPools();
        }

        private void LoadAndApplyUserSettings()
        {
            isLoadingSettings = true;
            try
            {
                appSettings = AppSettings.Load();
                TxtPort.Text = appSettings.Port.ToString();
                ChkAutoStart.IsChecked = appSettings.AutoStartProxy;
                ChkSystemProxy.IsChecked = appSettings.UseSystemProxy;
                if (appSettings.IsDarkMode && !isDarkMode)
                {
                    BtnTheme_Click(null, null);
                }
                SyncCustomHeaderColumns();
                if (!string.IsNullOrEmpty(appSettings.FilterText))
                {
                    TxtFilter.Text = appSettings.FilterText;
                }
            }
            finally
            {
                isLoadingSettings = false;
            }
        }

        private void SaveUserSettings()
        {
            if (isLoadingSettings || appSettings == null) return;
            if (int.TryParse(TxtPort.Text, out int p) && p >= 1 && p <= 65535)
                appSettings.Port = p;
            appSettings.AutoStartProxy = ChkAutoStart.IsChecked == true;
            appSettings.UseSystemProxy = ChkSystemProxy.IsChecked == true;
            appSettings.FilterText = TxtFilter.Text ?? "";
            appSettings.IsDarkMode = isDarkMode;
            appSettings.Save();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (appSettings?.AutoStartProxy == true && runningPort == 0)
            {
                BtnStart_Click(this, new RoutedEventArgs());
            }
        }

        private void ChkAutoStart_Changed(object sender, RoutedEventArgs e)
        {
            SaveUserSettings();
        }

        private void CheckStaleIisAppPools()
        {
            var attached = IisAppPoolProxyManager.GetAttachedPools();
            if (attached.Count == 0) return;

            var result = MessageBox.Show(
                "These IIS App Pools are still routed through the proxy:\n\n" +
                string.Join("\n", attached.Select(p => $"• {p.Name} (port {p.Port})")) +
                "\n\nThis usually means SoapProxyApp was closed unexpectedly. While the proxy is not running, their outgoing HTTP calls will fail.\n\n" +
                "Would you like to detach them now (restores previous settings and recycles the App Pools)?",
                "IIS App Pools Still Attached",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                Cursor = Cursors.Wait;
                List<string> errors;
                try { errors = IisAppPoolProxyManager.DetachAll(); }
                finally { Cursor = null; }

                if (errors.Count > 0)
                    MessageBox.Show("Some App Pools could not be detached:\n\n" + string.Join("\n", errors) + "\n\nYou can retry via 🎯 Capture App → IIS App Pools.", "Detach Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CheckStaleSystemProxy()
        {
            if (ProxyEngine.IsSystemProxyConfiguredLocally(out string configuredProxy))
            {
                var result = MessageBox.Show(
                    $"Windows System Proxy is currently configured to '{configuredProxy}'.\n\n" +
                    "This setting may have been left enabled after a previous session was closed or terminated abruptly.\n\n" +
                    "Would you like to disable the system proxy now to restore normal direct internet connection?",
                    "Stale System Proxy Detected",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    ProxyEngine.DisableSystemProxySettings();
                    MessageBox.Show("Windows System Proxy has been disabled. Direct connection restored.", "Proxy Disabled", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
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

                    Version vLatest = AppSettings.ParseVersionTag(latestVersion);
                    Version vCurrent = AppSettings.ParseVersionTag(AppVersion);
                    if (vLatest == null || vCurrent == null || vLatest <= vCurrent) return;

                    var settings = AppSettings.Load();
                    Version vSkipped = AppSettings.ParseVersionTag(settings.SkippedUpdateVersion);
                    if (vSkipped != null && vLatest <= vSkipped) return;

                    Dispatcher.Invoke(() =>
                    {
                        var dlg = new UpdateAvailableWindow(AppVersion, "v" + vLatest.ToString(3));
                        if (IsVisible) dlg.Owner = this;
                        else dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                        dlg.ShowDialog();

                        if (dlg.Choice == UpdateChoice.Download && !string.IsNullOrEmpty(downloadUrl))
                        {
                            Process.Start(new ProcessStartInfo { FileName = downloadUrl, UseShellExecute = true });
                        }
                        else if (dlg.Choice == UpdateChoice.Skip)
                        {
                            settings.SkippedUpdateVersion = vLatest.ToString(3);
                            settings.Save();
                        }
                    });
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
            SaveUserSettings();
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtPort.Text, out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Please enter a valid port number (1-65535).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!IsPortAvailable(port, out string portError))
            {
                MessageBox.Show($"Port {port} is already in use or cannot be opened.\n\n{portError}\n\nPlease choose a different port.", "Port Unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool isSystemProxy = ChkSystemProxy.IsChecked == true;
            try
            {
                proxyEngine.Start(port, isSystemProxy);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start proxy on port {port}.\n\nError: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            runningPort = port;
            TxtPort.IsEnabled = false;
            ChkSystemProxy.IsEnabled = false;
            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
            BtnCapture.IsEnabled = true;
            TxtStatus.Text = $"Proxy is running on 127.0.0.1:{port}" + (isSystemProxy ? " (System Proxy)" : "");
            TxtStatus.Foreground = System.Windows.Media.Brushes.Green;
            SaveUserSettings();
        }

        // Checks both the OS listener table and an actual exclusive bind attempt.
        private static bool IsPortAvailable(int port, out string error)
        {
            error = null;
            try
            {
                var listeners = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
                var existing = listeners.FirstOrDefault(l => l.Port == port);
                if (existing != null)
                {
                    error = $"Another process is already listening on {existing}.";
                    return false;
                }
            }
            catch { /* If the listener table cannot be read, rely on the bind test below */ }

            System.Net.Sockets.TcpListener probe = null;
            try
            {
                probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, port);
                probe.ExclusiveAddressUse = true;
                probe.Start();
                return true;
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                probe?.Stop();
            }
        }

        private async void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            proxyEngine.Stop();
            runningPort = 0;
            BtnStop.IsEnabled = false;
            BtnCapture.IsEnabled = false;

            if (IisAppPoolProxyManager.HasAttachedPools)
            {
                TxtStatus.Text = "Detaching IIS App Pools...";
                TxtStatus.Foreground = System.Windows.Media.Brushes.DarkOrange;
                var errors = await System.Threading.Tasks.Task.Run(IisAppPoolProxyManager.DetachAll);
                if (errors.Count > 0)
                    MessageBox.Show("Some App Pools could not be detached:\n\n" + string.Join("\n", errors), "Detach Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            TxtPort.IsEnabled = true;
            ChkSystemProxy.IsEnabled = true;
            BtnStart.IsEnabled = true;
            TxtStatus.Text = "Proxy is stopped.";
            TxtStatus.Foreground = System.Windows.Media.Brushes.Red;

            var running = ProxyLaunchTracker.GetRunning();
            if (running.Count > 0)
            {
                MessageBox.Show(
                    "These programs were launched with the proxy and are still running:\n\n" +
                    string.Join("\n", running.Select(r => "• " + r)) +
                    "\n\nTheir outgoing HTTP/HTTPS calls will fail until you start the proxy again on the same port, or restart them normally.",
                    "Programs Still Using the Proxy", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnCapture_Click(object sender, RoutedEventArgs e)
        {
            BtnCapture.ContextMenu.PlacementTarget = BtnCapture;
            BtnCapture.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            BtnCapture.ContextMenu.IsOpen = true;
        }

        private void MenuLaunchWithProxy_Click(object sender, RoutedEventArgs e)
        {
            if (runningPort == 0) return;
            new LaunchWithProxyWindow(runningPort) { Owner = this }.ShowDialog();
        }

        private void MenuIisAppPools_Click(object sender, RoutedEventArgs e)
        {
            if (runningPort == 0) return;
            new IisAppPoolsWindow(runningPort) { Owner = this }.ShowDialog();
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
            SaveUserSettings();
            string filterText = TxtFilter.Text.ToLower();
            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(Sessions);
            if (string.IsNullOrWhiteSpace(filterText))
            {
                view.Filter = null;
            }
            else
            {
                view.Filter = item =>
                {
                    if (item is CapturedSession s)
                    {
                        if ((s.Url?.ToLower().Contains(filterText) == true) ||
                            (s.ProcessName?.ToLower().Contains(filterText) == true) ||
                            (s.Method?.ToLower().Contains(filterText) == true) ||
                            (s.StatusCode.ToString().Contains(filterText) == true))
                            return true;

                        if (appSettings?.CustomHeaderColumns != null)
                        {
                            foreach (var hdr in appSettings.CustomHeaderColumns)
                            {
                                if (s.GetHeaderValue(hdr)?.ToLower().Contains(filterText) == true)
                                    return true;
                            }
                        }
                    }
                    return false;
                };
            }
        }

        private static readonly string[] PresetHeaderNames = new[]
        {
            "SOAPAction",
            "Content-Type",
            "X-Correlation-Id",
            "X-Request-Id",
            "Authorization",
            "Server",
            "User-Agent"
        };

        private void BtnColumns_Click(object sender, RoutedEventArgs e)
        {
            RebuildColumnsMenu();
            BtnColumns.ContextMenu.PlacementTarget = BtnColumns;
            BtnColumns.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            BtnColumns.ContextMenu.IsOpen = true;
        }

        private void RebuildColumnsMenu()
        {
            MenuColumns.Items.Clear();

            var addItem = new MenuItem { Header = "➕ Add Custom Header Column...", FontWeight = FontWeights.Bold };
            addItem.Click += (s, e) => PromptAndAddCustomHeader();
            MenuColumns.Items.Add(addItem);
            MenuColumns.Items.Add(new Separator());

            var allHeaders = PresetHeaderNames
                .Concat(appSettings?.CustomHeaderColumns ?? Enumerable.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var hdr in allHeaders)
            {
                bool isActive = appSettings?.CustomHeaderColumns?.Any(c => c.Equals(hdr, StringComparison.OrdinalIgnoreCase)) == true;
                var mi = new MenuItem
                {
                    Header = hdr,
                    IsCheckable = true,
                    IsChecked = isActive
                };
                string capturedHdr = hdr;
                mi.Click += (s, e) => ToggleCustomHeaderColumn(capturedHdr);
                MenuColumns.Items.Add(mi);
            }

            if (appSettings?.CustomHeaderColumns?.Count > 0)
            {
                MenuColumns.Items.Add(new Separator());
                var clearItem = new MenuItem { Header = "🗑 Remove All Custom Columns", Foreground = System.Windows.Media.Brushes.Red };
                clearItem.Click += (s, e) =>
                {
                    appSettings.CustomHeaderColumns.Clear();
                    SyncCustomHeaderColumns();
                    SaveUserSettings();
                };
                MenuColumns.Items.Add(clearItem);
            }
        }

        private void ToggleCustomHeaderColumn(string headerName)
        {
            if (string.IsNullOrWhiteSpace(headerName) || appSettings == null) return;
            headerName = headerName.Trim().TrimEnd(':');

            int idx = appSettings.CustomHeaderColumns.FindIndex(c => c.Equals(headerName, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                appSettings.CustomHeaderColumns.RemoveAt(idx);
            else
                appSettings.CustomHeaderColumns.Add(headerName);

            SyncCustomHeaderColumns();
            SaveUserSettings();
        }

        private void PromptAndAddCustomHeader()
        {
            var dlg = new Window
            {
                Title = "Add Custom Header Column",
                Width = 380,
                Height = 165,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Owner = this,
                Background = (System.Windows.Media.Brush)FindResource("WindowBg")
            };

            var root = new StackPanel { Margin = new Thickness(14) };
            root.Children.Add(new TextBlock
            {
                Text = "Enter Request or Response header name (e.g. X-Correlation-Id, SOAPAction, Server):",
                Foreground = (System.Windows.Media.Brush)FindResource("TextFg"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var txt = new TextBox
            {
                Height = 28,
                Padding = new Thickness(5, 3, 5, 3),
                BorderThickness = new Thickness(1),
                BorderBrush = (System.Windows.Media.Brush)FindResource("BorderColor"),
                Background = (System.Windows.Media.Brush)FindResource("PanelBg"),
                Foreground = (System.Windows.Media.Brush)FindResource("TextFg")
            };
            root.Children.Add(txt);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var btnAdd = new Button { Content = "Add Column", Width = 95, Height = 28, Margin = new Thickness(0, 0, 8, 0), IsDefault = true, Style = (Style)FindResource("AppButtonStyle"), FontWeight = FontWeights.Bold };
            var btnCancel = new Button { Content = "Cancel", Width = 75, Height = 28, IsCancel = true, Style = (Style)FindResource("AppButtonStyle") };

            btnAdd.Click += (s, e) =>
            {
                string val = txt.Text?.Trim().TrimEnd(':');
                if (!string.IsNullOrWhiteSpace(val))
                {
                    if (!appSettings.CustomHeaderColumns.Any(c => c.Equals(val, StringComparison.OrdinalIgnoreCase)))
                    {
                        appSettings.CustomHeaderColumns.Add(val);
                        SyncCustomHeaderColumns();
                        SaveUserSettings();
                    }
                    dlg.DialogResult = true;
                }
            };

            btnRow.Children.Add(btnAdd);
            btnRow.Children.Add(btnCancel);
            root.Children.Add(btnRow);
            dlg.Content = root;
            dlg.Loaded += (s, e) => txt.Focus();
            dlg.ShowDialog();
        }

        private void SyncCustomHeaderColumns()
        {
            if (LstSessions == null) return;

            // Remove existing dynamic custom columns (keep the 7 built-in columns: Time, Status, Method, Duration, Size, App, URL)
            for (int i = LstSessions.Columns.Count - 1; i >= 0; i--)
            {
                if (LstSessions.Columns[i].Header is string h && h.StartsWith("[") && h.EndsWith("]"))
                {
                    LstSessions.Columns.RemoveAt(i);
                }
            }

            if (appSettings?.CustomHeaderColumns == null) return;

            // Insert each custom header column just before the last column (URL)
            foreach (var rawHeader in appSettings.CustomHeaderColumns)
            {
                string hdr = rawHeader?.Trim();
                if (string.IsNullOrEmpty(hdr)) continue;

                var elemStyle = new Style(typeof(TextBlock));
                elemStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
                elemStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new System.Windows.Data.Binding($"[{hdr}]") { Mode = System.Windows.Data.BindingMode.OneWay }));

                var col = new DataGridTextColumn
                {
                    Header = $"[{hdr}]",
                    Binding = new System.Windows.Data.Binding($"[{hdr}]") { Mode = System.Windows.Data.BindingMode.OneWay },
                    SortMemberPath = $"[{hdr}]",
                    Width = new DataGridLength(150),
                    ElementStyle = elemStyle
                };

                int insertIdx = Math.Max(0, LstSessions.Columns.Count - 1);
                LstSessions.Columns.Insert(insertIdx, col);
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
                var view = System.Windows.Data.CollectionViewSource.GetDefaultView(Sessions);
                if (view?.SortDescriptions.Count == 0)
                {
                    // Scroll to bottom automatically when in chronological order
                    LstSessions.ScrollIntoView(session);
                }
            });
        }

        private string GetSafeSessionName(CapturedSession s, string suffix, string extension = ".xml")
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
            return safeName + extension;
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
            var f1 = FormatForCompare(s1.RequestBody);
            var f2 = FormatForCompare(s2.RequestBody);
            CompareDiff(f1.Text, f2.Text, GetSafeSessionName(s1, "Req1", f1.Extension), GetSafeSessionName(s2, "Req2", f2.Extension));
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
            var f1 = FormatForCompare(s1.ResponseBody);
            var f2 = FormatForCompare(s2.ResponseBody);
            CompareDiff(f1.Text, f2.Text, GetSafeSessionName(s1, "Res1", f1.Extension), GetSafeSessionName(s2, "Res2", f2.Extension));
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
                    TxtReqJson.Text = FormatAsJson(session.RequestBody);
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
                    TxtResJson.Text = FormatAsJson(session.ResponseBody);
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

        private static bool IsLikelyJson(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return false;
            string trimmed = body.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            return trimmed.StartsWith("{") || trimmed.StartsWith("[");
        }

        // Pretty-prints real JSON directly; falls back to XML -> JSON conversion for XML/SOAP bodies.
        private string FormatAsJson(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return "Empty body.";
            if (IsLikelyJson(body))
            {
                try
                {
                    return JToken.Parse(body).ToString(Newtonsoft.Json.Formatting.Indented);
                }
                catch (Exception ex)
                {
                    return $"Content looks like JSON but could not be parsed.\nError: {ex.Message}\n\nRAW:\n{body}";
                }
            }
            return ConvertXmlToJson(body);
        }

        // Formats a body for the diff tool and returns the matching file extension.
        private (string Text, string Extension) FormatForCompare(string body)
        {
            if (IsLikelyJson(body))
            {
                try { return (JToken.Parse(body).ToString(Newtonsoft.Json.Formatting.Indented), ".json"); }
                catch { return (body, ".txt"); }
            }
            if (string.IsNullOrWhiteSpace(body)) return ("", ".txt");
            try { return (XDocument.Parse(body).ToString(), ".xml"); }
            catch { return (body, ".txt"); }
        }

        protected override void OnClosed(EventArgs e)
        {
            SaveUserSettings();
            proxyEngine.Stop();
            try { IisAppPoolProxyManager.DetachAll(); } catch { }
            base.OnClosed(e);
        }
    }
}