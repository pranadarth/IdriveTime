using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace StatusReporter
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private SettingsModel settings;

        public SettingsWindow(SettingsModel current)
        {
            InitializeComponent();
            settings = current ?? SettingsModel.Default();
            ToBox.Text = settings.To;
            CcBox.Text = settings.Cc;
            SigBox.Text = settings.SignatureName;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // minimal validation: ensure To is not empty and contains '@'
            var to = ToBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(to) || !to.Contains("@"))
            {
                MessageBox.Show("Please enter a valid To address.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Normalize CC: remove spaces after commas
            var cc = string.Join(",", CcBox.Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));

            settings.To = to;
            settings.Cc = cc;
            settings.SignatureName = SigBox.Text.Trim();

            try
            {
                settings.Save();
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save settings: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}