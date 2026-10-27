using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace LiveLogs
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private readonly DispatcherTimer _searchDebounce;

        [DllImport("dwmapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        public MainWindow()
        {
            InitializeComponent();
            _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _searchDebounce.Tick += async (s, e) =>
            {
                _searchDebounce.Stop();
                var tab = CurrentLogTab;
                if (tab != null)
                    MatchCountText.Text = await tab.SearchAsync(SearchTextBox.Text);
            };
        }

        private bool IsDark => Properties.Settings.Default.ThemePreference == "Dark";

        private LogTab CurrentLogTab => (LogTabControl.SelectedItem as TabItem)?.Tag as LogTab;

        private IEnumerable<LogTab> AllLogTabs =>
            LogTabControl.Items.OfType<TabItem>().Select(t => t.Tag).OfType<LogTab>();

        #region PrivateFunctions
        private void SelectLogFileForNewTab()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Select Log File",
                Filter = "Log Files (*.log;*.txt)|*.log;*.txt|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var file in openFileDialog.FileNames)
                    OpenOrFocus(file);
                SaveOpenedFilesPaths();
            }
        }

        private void OpenOrFocus(string filePath)
        {
            var existing = AllLogTabs.FirstOrDefault(t =>
                string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                LogTabControl.SelectedItem = existing.Tab;
                return;
            }
            CreateNewLogTab(filePath);
        }

        private void CreateNewLogTab(string filePath)
        {
            var logTab = new LogTab(filePath, IsDark);
            logTab.CommandReceived += OnViewCommand;

            TabItem newTab = new TabItem
            {
                Header = Path.GetFileName(filePath),
                ToolTip = filePath,
                Tag = logTab,
                Style = (Style)FindResource("ModernTabItemStyle")
            };
            logTab.Tab = newTab;

            ViewHost.Children.Add(logTab.Host);
            LogTabControl.Items.Add(newTab);
            LogTabControl.SelectedItem = newTab;
            UpdateEmptyState();
        }

        private void CloseTab(TabItem tabItem)
        {
            if (tabItem.Tag is LogTab logTab)
            {
                logTab.CommandReceived -= OnViewCommand;
                ViewHost.Children.Remove(logTab.Host);
                logTab.Dispose();
            }
            LogTabControl.Items.Remove(tabItem);
            SaveOpenedFilesPaths();
            UpdateEmptyState();
        }

        private void UpdateEmptyState()
        {
            bool empty = LogTabControl.Items.Count == 0;
            ct.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            OpenExternalBtn.IsEnabled = OpenFolderBtn.IsEnabled = ClearLogsBtn.IsEnabled = !empty;
        }

        private async void OnViewCommand(LogTab tab, string cmd)
        {
            switch (cmd)
            {
                case "find":
                    SearchTextBox.Focus();
                    SearchTextBox.SelectAll();
                    break;
                case "next":
                    if (!string.IsNullOrEmpty(SearchTextBox.Text)) MatchCountText.Text = await tab.NextAsync();
                    break;
                case "prev":
                    if (!string.IsNullOrEmpty(SearchTextBox.Text)) MatchCountText.Text = await tab.PrevAsync();
                    break;
            }
        }

        private void LoadOpenedFilesPaths()
        {
            string storedFiles = Properties.Settings.Default.LastOpenedFile;
            if (!string.IsNullOrWhiteSpace(storedFiles))
            {
                string[] files = storedFiles.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var file in files.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (File.Exists(file))
                        CreateNewLogTab(file);
                }
            }
        }

        private void SaveOpenedFilesPaths()
        {
            Properties.Settings.Default.LastOpenedFile = string.Join(";", AllLogTabs.Select(t => t.FilePath));
            Properties.Settings.Default.Save();
        }
        #endregion

        #region Events
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ApplySavedTheme();
            LoadOpenedFilesPaths();
            UpdateEmptyState();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            foreach (var tab in AllLogTabs.ToList())
                tab.Dispose();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
            {
                SearchTextBox.Focus();
                SearchTextBox.SelectAll();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O)
            {
                SelectLogFileForNewTab();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.W && LogTabControl.SelectedItem is TabItem t)
            {
                CloseTab(t);
                e.Handled = true;
            }
            else if (e.Key == Key.F3)
            {
                if (Keyboard.Modifiers == ModifierKeys.Shift) Button_Click_Up(null, null);
                else Button_Click_Down(null, null);
                e.Handled = true;
            }
        }

        private void ChangeFile_Click(object sender, RoutedEventArgs e)
        {
            SelectLogFileForNewTab();
        }

        private void OpenExternal_Click(object sender, RoutedEventArgs e)
        {
            var tab = CurrentLogTab;
            if (tab == null) return;
            try
            {
                Process.Start(new ProcessStartInfo(tab.FilePath) { UseShellExecute = true });
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // no association for this extension: fall back to Notepad
                try { Process.Start("notepad.exe", "\"" + tab.FilePath + "\""); }
                catch (Exception ex) { MessageBox.Show("Unable to open file: " + ex.Message); }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to open file: " + ex.Message);
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var tab = CurrentLogTab;
            if (tab == null) return;
            try
            {
                Process.Start("explorer.exe", "/select,\"" + tab.FilePath + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to open folder: " + ex.Message);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            bool hasText = !string.IsNullOrEmpty(SearchTextBox.Text);
            UpBtn.Visibility = DownBtn.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
            if (!hasText) MatchCountText.Text = "";

            _searchDebounce.Stop();
            _searchDebounce.Start();
        }

        private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (_searchDebounce.IsEnabled) return; // let pending search run first
                if (Keyboard.Modifiers == ModifierKeys.Shift) Button_Click_Up(null, null);
                else Button_Click_Down(null, null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                SearchTextBox.Text = "";
                CurrentLogTab?.Focus();
                e.Handled = true;
            }
        }

        private async void LogTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource != LogTabControl) return;

            var current = CurrentLogTab;
            foreach (var tab in AllLogTabs)
                tab.Host.Visibility = tab == current ? Visibility.Visible : Visibility.Collapsed;

            if (current != null && !string.IsNullOrEmpty(SearchTextBox.Text))
                MatchCountText.Text = await current.SearchAsync(SearchTextBox.Text);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button closeButton && closeButton.Tag is TabItem tabItem)
                CloseTab(tabItem);
        }

        private void LogTabControl_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void LogTabControl_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var file in files.Where(File.Exists))
                    OpenOrFocus(file);
                SaveOpenedFilesPaths();
            }
        }

        private async void Button_Click_Up(object sender, RoutedEventArgs e)
        {
            var tab = CurrentLogTab;
            if (tab == null || string.IsNullOrEmpty(SearchTextBox.Text)) return;
            MatchCountText.Text = await tab.PrevAsync();
        }

        private async void Button_Click_Down(object sender, RoutedEventArgs e)
        {
            var tab = CurrentLogTab;
            if (tab == null || string.IsNullOrEmpty(SearchTextBox.Text)) return;
            MatchCountText.Text = await tab.NextAsync();
        }

        private void ClearLogsBtn_Click(object sender, RoutedEventArgs e)
        {
            var logTab = CurrentLogTab;
            if (logTab == null)
            {
                MessageBox.Show("No log tab is selected.", "Clear Logs", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"This will clear the file:\n\n{logTab.FilePath}\n\nAre you sure?",
                                         "Clear Logs", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                using (var fs = new FileStream(logTab.FilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                {
                }
                logTab.Reset();
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Access denied while trying to clear the file. Try running the app with elevated permissions or close any program locking the file.",
                                "Clear Logs - Permission Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (IOException ioEx)
            {
                MessageBox.Show("I/O error while clearing the file: " + ioEx.Message + "\n\nMake sure the file isn't locked by another process.",
                                "Clear Logs - IO Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unexpected error: " + ex.Message, "Clear Logs - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Theme
        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            string theme = IsDark ? "Light" : "Dark";
            SetThemeDictionary(theme);
            Properties.Settings.Default.ThemePreference = theme;
            Properties.Settings.Default.Save();
            ThemeBtn.IsChecked = theme == "Dark";
            SetThemeToTitleBar(theme);
            foreach (var tab in AllLogTabs)
                tab.SetTheme(theme == "Dark");
        }

        private void ApplySavedTheme()
        {
            string savedTheme = Properties.Settings.Default.ThemePreference == "Dark" ? "Dark" : "Light";
            SetThemeDictionary(savedTheme);
            ThemeBtn.IsChecked = savedTheme == "Dark";
            SetThemeToTitleBar(savedTheme);
        }

        private static void SetThemeDictionary(string theme)
        {
            var dictionaries = Application.Current.Resources.MergedDictionaries;
            foreach (var d in dictionaries.Where(d => d.Source != null &&
                     (d.Source.OriginalString.EndsWith("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase) ||
                      d.Source.OriginalString.EndsWith("LightTheme.xaml", StringComparison.OrdinalIgnoreCase))).ToList())
            {
                dictionaries.Remove(d);
            }
            dictionaries.Add(new ResourceDictionary { Source = new Uri(theme + "Theme.xaml", UriKind.Relative) });
        }

        public void SetThemeToTitleBar(string enableDarkMode)
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    int isDarkMode = enableDarkMode == "Dark" ? 1 : 0;
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref isDarkMode, sizeof(int));
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("SetThemeToTitleBar failed: " + e);
            }
        }
        #endregion
    }
}
