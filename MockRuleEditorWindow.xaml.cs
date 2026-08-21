using System;
using System.Windows;
using ICSharpCode.AvalonEdit.Highlighting;

namespace SoapProxyApp
{
    public partial class MockRuleEditorWindow : Window
    {
        public MockRule Rule { get; private set; }

        public MockRuleEditorWindow(MockRule existingRule = null)
        {
            InitializeComponent();
            
            if (existingRule != null)
            {
                Rule = existingRule;
                TxtUrlMatch.Text = Rule.UrlMatch;
                TxtStatusCode.Text = Rule.StatusCode.ToString();
                TxtContentType.Text = Rule.ContentType;

                if (Rule.ContentType?.ToLower().Contains("xml") == true || Rule.ContentType?.ToLower().Contains("soap") == true)
                    TxtResponseBody.Text = FormatXml(Rule.ResponseBody);
                else if (Rule.ContentType?.ToLower().Contains("json") == true)
                    TxtResponseBody.Text = FormatJson(Rule.ResponseBody);
                else
                    TxtResponseBody.Text = Rule.ResponseBody;
            }
            else
            {
                Rule = new MockRule();
                TxtStatusCode.Text = "200";
                TxtContentType.Text = "application/json";
            }
            SetSyntax();
            TxtContentType.TextChanged += (s, e) => SetSyntax();
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

        private void SetSyntax()
        {
            if (TxtContentType.Text.Contains("xml") || TxtContentType.Text.Contains("soap"))
                TxtResponseBody.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
            else if (TxtContentType.Text.Contains("json"))
                TxtResponseBody.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("JavaScript");
            else
                TxtResponseBody.SyntaxHighlighting = null;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtUrlMatch.Text))
            {
                MessageBox.Show("Please enter a URL match string.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!int.TryParse(TxtStatusCode.Text, out int statusCode))
            {
                MessageBox.Show("Invalid status code.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Rule.UrlMatch = TxtUrlMatch.Text.Trim();
            Rule.StatusCode = statusCode;
            Rule.ContentType = TxtContentType.Text.Trim();
            Rule.ResponseBody = TxtResponseBody.Text;
            
            DialogResult = true;
        }
    }
}
