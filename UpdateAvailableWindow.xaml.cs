using System.Windows;

namespace SoapProxyApp
{
    public enum UpdateChoice { Later, Download, Skip }

    public partial class UpdateAvailableWindow : Window
    {
        public UpdateChoice Choice { get; private set; } = UpdateChoice.Later;

        public UpdateAvailableWindow(string currentVersion, string latestVersion)
        {
            InitializeComponent();
            TxtVersions.Text = $"Installed: {currentVersion}     Latest: {latestVersion}";
        }

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            Choice = UpdateChoice.Download;
            Close();
        }

        private void BtnSkip_Click(object sender, RoutedEventArgs e)
        {
            Choice = UpdateChoice.Skip;
            Close();
        }
    }
}
