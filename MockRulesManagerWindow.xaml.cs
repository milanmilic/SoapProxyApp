using System;
using System.Windows;

namespace SoapProxyApp
{
    public partial class MockRulesManagerWindow : Window
    {
        public MockRulesManagerWindow()
        {
            InitializeComponent();
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            GridRules.ItemsSource = null;
            GridRules.ItemsSource = MockRulesManager.Rules;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var editor = new MockRuleEditorWindow() { Owner = this };
            if (editor.ShowDialog() == true)
            {
                MockRulesManager.AddRule(editor.Rule);
                RefreshGrid();
            }
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (GridRules.SelectedItem is MockRule rule)
            {
                var editor = new MockRuleEditorWindow(rule) { Owner = this };
                if (editor.ShowDialog() == true)
                {
                    MockRulesManager.UpdateRule(editor.Rule);
                    RefreshGrid();
                }
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (GridRules.SelectedItem is MockRule rule)
            {
                if (MessageBox.Show($"Delete rule '{rule.UrlMatch}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    MockRulesManager.RemoveRule(rule.Id);
                    RefreshGrid();
                }
            }
        }
        
        protected override void OnClosed(EventArgs e)
        {
            // Save state of checkboxes when window closes
            MockRulesManager.SaveRules();
            base.OnClosed(e);
        }
    }
}
