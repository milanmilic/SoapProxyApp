using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace SoapProxyApp
{
    public partial class LaunchWithProxyWindow : Window
    {
        private readonly int port;
        private List<ProgramPreset> presets = new();
        private bool suppressPresetChange;

        public LaunchWithProxyWindow(int port)
        {
            InitializeComponent();
            this.port = port;
            TxtEnvPreview.Text = ProxyEnvironment.Describe(port);
            LstRecent.ItemsSource = LaunchProfilesManager.Load();
            CmbPreset.IsEnabled = false;
            CmbPreset.Items.Add("Searching for installed programs...");
            CmbPreset.SelectedIndex = 0;
            Loaded += async (_, _) => await LoadPresetsAsync();
        }

        private async Task LoadPresetsAsync()
        {
            presets = await Task.Run(ProgramLocator.FindPresets);
            suppressPresetChange = true;
            CmbPreset.Items.Clear();
            foreach (var p in presets) CmbPreset.Items.Add(p);
            CmbPreset.IsEnabled = true;
            suppressPresetChange = false;

            // Pre-select the most recent launch, otherwise the first preset (usually Visual Studio).
            if (LstRecent.Items.Count > 0) LstRecent.SelectedIndex = 0;
            else CmbPreset.SelectedIndex = 0;
        }

        private void CmbPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressPresetChange || CmbPreset.SelectedItem is not ProgramPreset preset) return;
            TxtPath.Text = preset.Path;
            TxtArgs.Text = preset.DefaultArguments;
            TxtWorkDir.Text = "";
            UpdateHint(preset.Kind);
        }

        private ProgramKind CurrentKind => CmbPreset.SelectedItem is ProgramPreset p ? p.Kind : ProgramKind.Custom;

        private void UpdateHint(ProgramKind kind)
        {
            string hint = kind switch
            {
                ProgramKind.VisualStudio => "Close all running Visual Studio instances first. An instance that is already open does not get the variables. Projects you start with F5 / Ctrl+F5 inherit them. Visual Studio runs as Administrator, like SoapProxyApp.",
                ProgramKind.VSCode => "Close all VS Code windows first. Otherwise the new window joins the running instance without the variables. Programs started from its terminal and debugger inherit them.",
                ProgramKind.Console => "Start your application from this console (e.g. 'dotnet run' or any .exe). Its HTTP/HTTPS calls go through the proxy.",
                _ => "The selected program and every process it starts will use the proxy."
            };
            hint += Environment.NewLine + "Works for .NET Core / .NET 5+, Python, curl, Go. .NET Framework 4.x apps ignore these variables (configure them via web.config / app.config). Calls to localhost are never proxied. Keep the proxy running while the program is open.";
            TxtHint.Text = hint;
            BtnBrowseArg.Content = kind == ProgramKind.VisualStudio ? "Solution..." : "File...";
        }

        private void LstRecent_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstRecent.SelectedItem is not LaunchProfile profile) return;
            suppressPresetChange = true;
            var preset = presets.FirstOrDefault(p => p.Kind != ProgramKind.Custom && string.Equals(p.Path, profile.Path, StringComparison.OrdinalIgnoreCase))
                         ?? presets.FirstOrDefault(p => p.Kind == ProgramKind.Custom);
            if (preset != null) CmbPreset.SelectedItem = preset;
            suppressPresetChange = false;

            TxtPath.Text = profile.Path;
            TxtArgs.Text = profile.Arguments;
            TxtWorkDir.Text = profile.WorkingDirectory;
            UpdateHint(profile.Kind);
        }

        private void LstRecent_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LstRecent.SelectedItem is LaunchProfile) Launch();
        }

        private void BtnRemoveRecent_Click(object sender, RoutedEventArgs e)
        {
            if (LstRecent.SelectedItem is LaunchProfile profile)
            {
                LstRecent.ItemsSource = LaunchProfilesManager.Remove(profile);
            }
        }

        private void BtnBrowseExe_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Programs (*.exe;*.cmd;*.bat)|*.exe;*.cmd;*.bat|All files (*.*)|*.*" };
            if (File.Exists(TxtPath.Text)) dlg.InitialDirectory = Path.GetDirectoryName(TxtPath.Text);
            if (dlg.ShowDialog(this) == true)
            {
                TxtPath.Text = dlg.FileName;
                suppressPresetChange = true;
                CmbPreset.SelectedItem = presets.FirstOrDefault(p => p.Kind == ProgramKind.Custom);
                suppressPresetChange = false;
                UpdateHint(ProgramKind.Custom);
            }
        }

        private void BtnBrowseArg_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = CurrentKind == ProgramKind.VisualStudio
                    ? "Solutions and projects (*.sln;*.slnx;*.csproj;*.vbproj)|*.sln;*.slnx;*.csproj;*.vbproj|All files (*.*)|*.*"
                    : "All files (*.*)|*.*"
            };
            if (dlg.ShowDialog(this) == true)
            {
                TxtArgs.Text = $"\"{dlg.FileName}\"";
                if (string.IsNullOrWhiteSpace(TxtWorkDir.Text)) TxtWorkDir.Text = Path.GetDirectoryName(dlg.FileName);
            }
        }

        private void BtnBrowseDir_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog();
            if (Directory.Exists(TxtWorkDir.Text)) dlg.InitialDirectory = TxtWorkDir.Text;
            if (dlg.ShowDialog(this) == true) TxtWorkDir.Text = dlg.FolderName;
        }

        private void BtnLaunch_Click(object sender, RoutedEventArgs e) => Launch();

        private void Launch()
        {
            string path = TxtPath.Text.Trim().Trim('"');
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                MessageBox.Show(this, "Please select an existing executable.", "Launch with Proxy", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string workDir = TxtWorkDir.Text.Trim().Trim('"');
            if (!string.IsNullOrEmpty(workDir) && !Directory.Exists(workDir))
            {
                MessageBox.Show(this, $"Working folder does not exist:\n{workDir}", "Launch with Proxy", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(workDir)) workDir = Path.GetDirectoryName(path);

            var kind = CurrentKind;
            if (kind == ProgramKind.VisualStudio && Process.GetProcessesByName("devenv").Length > 0)
            {
                var answer = MessageBox.Show(this,
                    "Visual Studio is already running.\n\nThe new instance will use the proxy, but projects started from instances that were already open will not.\n\nLaunch anyway?",
                    "Launch with Proxy", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes) return;
            }

            var psi = new ProcessStartInfo(path)
            {
                Arguments = TxtArgs.Text.Trim(),
                WorkingDirectory = workDir,
                UseShellExecute = false
            };
            ProxyEnvironment.ApplyTo(psi, port);

            string name = CmbPreset.SelectedItem is ProgramPreset preset && preset.Kind != ProgramKind.Custom
                ? preset.Name
                : Path.GetFileNameWithoutExtension(path);

            try
            {
                var process = Process.Start(psi);
                if (process != null) ProxyLaunchTracker.Add(process, name);
            }
            catch (Win32Exception ex)
            {
                MessageBox.Show(this, $"Failed to start '{path}'.\n\n{ex.Message}", "Launch with Proxy", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            LaunchProfilesManager.AddOrUpdate(new LaunchProfile
            {
                Name = name,
                Path = path,
                Arguments = TxtArgs.Text.Trim(),
                WorkingDirectory = TxtWorkDir.Text.Trim(),
                Kind = kind
            });
            DialogResult = true;
        }
    }
}
