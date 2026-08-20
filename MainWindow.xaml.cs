using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace SoapProxyApp
{
    public partial class MainWindow : Window
    {
        private ProxyEngine proxyEngine;
        public ObservableCollection<CapturedSession> Sessions { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            Sessions = new ObservableCollection<CapturedSession>();
            LstSessions.ItemsSource = Sessions;
            
            proxyEngine = new ProxyEngine();
            proxyEngine.OnSessionCompleted += ProxyEngine_OnSessionCompleted;
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtPort.Text, out int port))
            {
                MessageBox.Show("Molimo unesite validan broj porta.", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("Moraš prvo kliknuti 'Start Proxy' da bi sertifikat bio generisan.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    MessageBox.Show("Sertifikat je uspešno instaliran direktno u Local Machine -> Trusted Root Certification Authorities!\n\nTvoje web aplikacije sada automatski veruju proxy-ju.", "Uspeh", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (System.Security.Cryptography.CryptographicException)
                {
                    string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ProxyCert.cer");
                    System.IO.File.WriteAllBytes(path, cert.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Cert));
                    MessageBox.Show($"Nemaš administratorska ovlašćenja za automatsku instalaciju.\n\nIli pokreni ovu aplikaciju kao Administrator (desni klik -> Run as Administrator) pa klikni ponovo, ILI ručno instaliraj fajl koji ti je upravo sačuvan na Desktopu pod imenom 'ProxyCert.cer'.", "Zahteva Administratora", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ProxyEngine_OnSessionCompleted(object sender, CapturedSession session)
        {
            Dispatcher.Invoke(() =>
            {
                Sessions.Add(session);
            });
        }

        private void LstSessions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstSessions.SelectedItem is CapturedSession session)
            {
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
            ExportActiveTab(ReqTabs, TxtReqHeaders, TxtReqRaw, TxtReqXmlFormatted, TxtReqJson, "Request");
        }

        private void BtnExportRes_Click(object sender, RoutedEventArgs e)
        {
            ExportActiveTab(ResTabs, TxtResHeaders, TxtResRaw, TxtResXmlFormatted, TxtResJson, "Response");
        }

        private void ExportActiveTab(TabControl tabControl, TextBox txtHeaders, TextBox txtRaw, TextBox txtXml, TextBox txtJson, string prefix)
        {
            if (tabControl == null || LstSessions.SelectedItem == null) return;

            string content = "";
            string ext = "txt";
            string filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";

            switch (tabControl.SelectedIndex)
            {
                case 0:
                    content = txtHeaders.Text;
                    break;
                case 1:
                    content = txtRaw.Text;
                    ext = "xml";
                    filter = "XML files (*.xml)|*.xml|Text files (*.txt)|*.txt|All files (*.*)|*.*";
                    break;
                case 2:
                    content = txtXml.Text;
                    ext = "xml";
                    filter = "XML files (*.xml)|*.xml|Text files (*.txt)|*.txt|All files (*.*)|*.*";
                    break;
                case 3:
                    content = txtJson.Text;
                    ext = "json";
                    filter = "JSON files (*.json)|*.json|Text files (*.txt)|*.txt|All files (*.*)|*.*";
                    break;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                MessageBox.Show("Nema sadržaja za exportovanje.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    MessageBox.Show("Fajl uspešno sačuvan!", "Uspeh", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Greška pri čuvanju: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RefreshReqTab()
        {
            if (LstSessions.SelectedItem is CapturedSession session && ReqTabs != null)
            {
                if (ReqTabs.SelectedIndex == 2 && string.IsNullOrEmpty(TxtReqXmlFormatted.Text))
                    TxtReqXmlFormatted.Text = FormatXml(session.RequestBody);
                else if (ReqTabs.SelectedIndex == 3 && string.IsNullOrEmpty(TxtReqJson.Text))
                    TxtReqJson.Text = ConvertXmlToJson(session.RequestBody);
            }
        }

        private void RefreshResTab()
        {
            if (LstSessions.SelectedItem is CapturedSession session && ResTabs != null)
            {
                if (ResTabs.SelectedIndex == 2 && string.IsNullOrEmpty(TxtResXmlFormatted.Text))
                    TxtResXmlFormatted.Text = FormatXml(session.ResponseBody);
                else if (ResTabs.SelectedIndex == 3 && string.IsNullOrEmpty(TxtResJson.Text))
                    TxtResJson.Text = ConvertXmlToJson(session.ResponseBody);
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
                return "Sadržaj nije validan XML.\n\n" + xml;
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
                return $"Nije moguće parsirati sadržaj kao XML u JSON.\nGreška: {ex.Message}\n\nRAW:\n{xml}";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            proxyEngine.Stop();
            base.OnClosed(e);
        }
    }
}