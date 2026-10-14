using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace SoapProxyApp
{
    public partial class IisAppPoolsWindow : Window
    {
        private readonly int port;
        private bool busy;

        public IisAppPoolsWindow(int port)
        {
            InitializeComponent();
            this.port = port;
            TxtIntro.Text =
                $"Attach adds HTTP_PROXY / HTTPS_PROXY = {ProxyEnvironment.GetProxyUrl(port)} and NO_PROXY to the App Pool's environment variables and recycles it. " +
                "Detach restores the previous values. All attached pools are detached automatically when you stop the proxy or close SoapProxyApp.\n" +
                "Works for ASP.NET Core / .NET 5+ apps (IIS 10 or newer). .NET Framework 4.x pools ignore these variables (use web.config <defaultProxy>). " +
                "HTTPS requires the proxy certificate in Local Machine (use 'Install Cert'), because the App Pool identity does not trust the current user's certificates.";
            Loaded += async (_, _) => await RefreshAsync();
        }

        private async Task RefreshAsync(string status = null)
        {
            if (!IisAppPoolProxyManager.IsAvailable)
            {
                TxtStatus.Text = "IIS is not installed on this machine (appcmd.exe not found).";
                BtnAttach.IsEnabled = BtnDetach.IsEnabled = BtnRefresh.IsEnabled = false;
                return;
            }

            SetBusy(true, "Reading IIS App Pools...");
            try
            {
                var selected = GridPools.SelectedItems.Cast<AppPoolInfo>().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var pools = await Task.Run(IisAppPoolProxyManager.GetAppPools);
                GridPools.ItemsSource = pools;
                foreach (var p in pools.Where(p => selected.Contains(p.Name))) GridPools.SelectedItems.Add(p);
                TxtStatus.Text = status ?? $"{pools.Count} App Pool(s), {pools.Count(p => p.IsAttached)} attached.";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Failed to read App Pools: " + ex.Message;
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool value, string status = null)
        {
            busy = value;
            BtnAttach.IsEnabled = BtnDetach.IsEnabled = BtnRefresh.IsEnabled = GridPools.IsEnabled = !value;
            Cursor = value ? Cursors.Wait : null;
            if (status != null) TxtStatus.Text = status;
        }

        private List<AppPoolInfo> SelectedPools => GridPools.SelectedItems.Cast<AppPoolInfo>().ToList();

        private async void BtnAttach_Click(object sender, RoutedEventArgs e) => await ApplyAsync(SelectedPools, attach: true);

        private async void BtnDetach_Click(object sender, RoutedEventArgs e) => await ApplyAsync(SelectedPools, attach: false);

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

        private async void GridPools_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (busy || GridPools.SelectedItem is not AppPoolInfo pool) return;
            await ApplyAsync(new List<AppPoolInfo> { pool }, attach: !pool.IsAttached);
        }

        private async Task ApplyAsync(List<AppPoolInfo> pools, bool attach)
        {
            if (pools.Count == 0)
            {
                TxtStatus.Text = "Select one or more App Pools first.";
                return;
            }

            if (!attach)
            {
                var unmanaged = pools.Where(p => !p.IsAttached && ProxyEnvironment.PointsToLocalProxy(p.HttpsProxyValue)).ToList();
                pools = pools.Where(p => p.IsAttached).Concat(unmanaged).ToList();
                if (pools.Count == 0)
                {
                    TxtStatus.Text = "The selected App Pools are not attached.";
                    return;
                }
                if (unmanaged.Count > 0 &&
                    MessageBox.Show(this,
                        $"These App Pools point to a local proxy but were not attached by SoapProxyApp:\n\n{string.Join("\n", unmanaged.Select(p => p.Name))}\n\nRemove their HTTP_PROXY / HTTPS_PROXY variables?",
                        "Detach", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    pools = pools.Except(unmanaged).ToList();
                    if (pools.Count == 0) return;
                }
            }

            SetBusy(true, (attach ? "Attaching" : "Detaching") + " and recycling: " + string.Join(", ", pools.Select(p => p.Name)) + "...");
            var errors = await Task.Run(() =>
            {
                var list = new List<string>();
                foreach (var pool in pools)
                {
                    try
                    {
                        if (attach) IisAppPoolProxyManager.Attach(pool.Name, port);
                        else IisAppPoolProxyManager.Detach(pool.Name);
                    }
                    catch (Exception ex) { list.Add($"{pool.Name}: {ex.Message}"); }
                }
                return list;
            });
            SetBusy(false);

            string done = $"{(attach ? "Attached" : "Detached")} {pools.Count - errors.Count} of {pools.Count} App Pool(s).";
            if (errors.Count > 0)
            {
                MessageBox.Show(this, string.Join("\n\n", errors), attach ? "Attach failed" : "Detach failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            await RefreshAsync(done);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Do not close while appcmd is changing the configuration.
            if (busy) e.Cancel = true;
            base.OnClosing(e);
        }
    }
}
