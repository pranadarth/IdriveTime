using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.IO.Path;

namespace LiveLogs
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string logFilePath;
        private FileSystemWatcher watcher;
        private long lastPosition = 0;
        private string fullLogContent = "";
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private List<int> _matchIndices = new List<int>();
        private int _currentMatchPosition = -1;


        [DllImport("dwmapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        public MainWindow()
        {
            InitializeComponent();
        }

        #region PrivateFunctions
        private void SelectLogFileForNewTab()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Select Log File",
                Filter = "Log Files (*.log;*.txt)|*.log;*.txt|All Files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                CreateNewLogTab(openFileDialog.FileName);
                ct.Visibility = Visibility.Collapsed;
                SaveOpenedFilesPaths();
            }
        }

        private void CreateNewLogTab(string filePath)
        {
            // Create a new TabItem
            TabItem newTab = new TabItem();
            newTab.Header = System.IO.Path.GetFileName(filePath);

            // Create a TextBox for log display (using your style)
            /* TextBox logTextBox = new TextBox();
             logTextBox.Style = (Style)FindResource("StyledMainTextBox");
             logTextBox.IsReadOnly = true;
             logTextBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
             logTextBox.TextWrapping = TextWrapping.Wrap;*/

            ListBox logTextBox = new ListBox
            {
                Style = (Style)FindResource("StyledMainTextBox"),
            };

            newTab.Content = logTextBox;
            newTab.Style = (Style)FindResource("ModernTabItemStyle");
            // Add the tab to the TabControl and select it
            LogTabControl.Items.Add(newTab);
            LogTabControl.SelectedItem = newTab;

            // Create a LogTab instance to hold state for this file
            LogTab newLogTab = new LogTab
            {
                FilePath = filePath,
                LastPosition = 0,
                LogTextBox = logTextBox
            };

            logTextBox.ItemsSource = newLogTab.LogLines;

            
            newTab.Tag = newLogTab;

            
            FileSystemWatcher watcher = new FileSystemWatcher
            {
                Path = System.IO.Path.GetDirectoryName(filePath),
                Filter = System.IO.Path.GetFileName(filePath),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };
            watcher.Changed +=  (s, e) =>  Dispatcher.InvokeAsync(() => ReadNewLines(newLogTab));
            watcher.EnableRaisingEvents = true;
            newLogTab.Watcher = watcher;

            Thread thread = new Thread(() => ReadNewLines(newLogTab))
            {
                IsBackground = true
            };
            thread.Start();

        }

        private void  ReadNewLines(LogTab logTab)
        {
            try
            {
                using (var stream = new FileStream(logTab.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    stream.Seek(logTab.LastPosition, SeekOrigin.Begin);
                    string newContent =  reader.ReadToEnd();
                    logTab.LastPosition = stream.Position;

                    if (!string.IsNullOrEmpty(newContent))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            logTab.AppendLog(newContent);

                            //// Use a timer to throttle UI updates
                            //if (logTab.UpdateTimer == null)
                            //{
                            //    logTab.UpdateTimer = new DispatcherTimer
                            //    {
                            //        Interval = TimeSpan.FromMilliseconds(250)
                            //    };
                            //    logTab.UpdateTimer.Tick += (s, e) =>
                            //    {
                            //        logTab.UpdateTimer.Stop();
                            //        ApplyFilterToLogTab(logTab);
                            //    };
                            //}
                            //logTab.UpdateTimer.Stop();
                            //logTab.UpdateTimer.Start();
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error reading log file: " + ex.Message);
            }
        }


        private void ApplyFilterToLogTab(LogTab logTab)
        {
            string searchQuery = SearchTextBox.Text.ToLower();
            _matchIndices.Clear();
            if (string.IsNullOrEmpty(searchQuery))
            {
                logTab.LogTextBox.ItemsSource = logTab.LogLines;
                logTab.LogTextBox.SelectedIndex = -1;
                return;
            }
            /* else
             {
                 var filteredLines = logTab.LogLines.Where(line => line.ToLower().Contains(searchQuery)).ToList();
                 //logTab.LogTextBox.ItemsSource = filteredLines;
             }

             if (logTab.LogTextBox.Items.Count > 0)
             {
                 logTab.LogTextBox.ScrollIntoView(logTab.LogTextBox.Items[logTab.LogTextBox.Items.Count - 1]);
             }*/

            for (int i = 0; i < logTab.LogLines.Count; i++)
            {
                if (logTab.LogLines[i].ToLower().Contains(searchQuery))
                {
                    _matchIndices.Add(i);
                }
            }

            if (_matchIndices.Count>0)
            {
                _currentMatchPosition = 0;
                NavigateToMatch(logTab, _matchIndices[0]);
            }
            else
            {
                logTab.LogTextBox.SelectedIndex = -1;
            }
        }

        private void NavigateToMatch(LogTab logTab, int matchIndex)
        {
            
            logTab.LogTextBox.SelectedIndex = matchIndex;
            logTab.LogTextBox.Dispatcher.InvokeAsync(() =>
            {
                logTab.LogTextBox.ScrollIntoView(logTab.LogTextBox.Items[matchIndex]);
            }, DispatcherPriority.Background);
        }

        private void FilterLogs()
        {
           
        }

        private void LoadOpenedFilesPaths()
        {
            string storedFiles = Properties.Settings.Default.LastOpenedFile;
            if (!string.IsNullOrWhiteSpace(storedFiles))
            {
                string[] files = storedFiles.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var file in files)
                {
                    if (File.Exists(file))
                        CreateNewLogTab(file);
                }
            }
        }

        private void SaveOpenedFilesPaths()
        {
            List<string> filePaths = new List<string>();
            foreach (TabItem tab in LogTabControl.Items)
            {
                if (tab.Tag is LogTab logTab)
                {
                    filePaths.Add(logTab.FilePath);
                }
            }
            Properties.Settings.Default.LastOpenedFile = string.Join(";", filePaths);
            Properties.Settings.Default.Save();
        }
        #endregion

        #region Events
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ApplySavedTheme();
            LoadOpenedFilesPaths();
            if (LogTabControl.Items.Count == 0)
            {
                ct.Visibility = Visibility.Visible;
            }
        }

        private void ChangeFile_Click(object sender, RoutedEventArgs e)
        {
            SelectLogFileForNewTab();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if(String.IsNullOrEmpty(SearchTextBox.Text))
                UpBtn.Visibility = DownBtn.Visibility = Visibility.Collapsed;
            else
                UpBtn.Visibility = DownBtn.Visibility = Visibility.Visible;

            if (LogTabControl.SelectedItem is TabItem selectedTab && selectedTab.Tag is LogTab logTab)
            {
                ApplyFilterToLogTab(logTab);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button closeButton && closeButton.Tag is TabItem tabItem)
            {
                // Dispose FileSystemWatcher if it exists
                if (tabItem.Tag is LogTab logTab && logTab.Watcher != null)
                {
                    logTab.Watcher.EnableRaisingEvents = false;
                    logTab.Watcher.Dispose();
                }

                LogTabControl.Items.Remove(tabItem);
                SaveOpenedFilesPaths();
                if (LogTabControl.Items.Count == 0)
                {
                    ct.Visibility = Visibility.Visible;
                }
            }
        }


        private void LogTabControl_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;

            e.Handled = true;
        }

        private void LogTabControl_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (files.Length > 0)
                {
                    string filePath = files[0]; // Get the first file
                    CreateNewLogTab(filePath);
                    ct.Visibility = Visibility.Collapsed;
                    SaveOpenedFilesPaths();
                }
            }
        }

        #endregion

        #region Theme
        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            var dictionaries = Application.Current.Resources.MergedDictionaries;
            var darkDictionary = dictionaries.FirstOrDefault(d => d.Source != null &&
                d.Source.OriginalString.EndsWith("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase));
            var lightDictionary = dictionaries.FirstOrDefault(d => d.Source != null &&
                d.Source.OriginalString.EndsWith("LightTheme.xaml", StringComparison.OrdinalIgnoreCase));

            if (darkDictionary != null)
            {
                dictionaries.Remove(darkDictionary);
                dictionaries.Add(new ResourceDictionary()
                {
                    Source = new Uri("LightTheme.xaml", UriKind.Relative)
                });
                Properties.Settings.Default.ThemePreference = "Light";
                SetThemeToTitleBar("Light");
            }
            else if (lightDictionary != null)
            {
                dictionaries.Remove(lightDictionary);
                dictionaries.Add(new ResourceDictionary()
                {
                    Source = new Uri("DarkTheme.xaml", UriKind.Relative)
                });
                Properties.Settings.Default.ThemePreference = "Dark";
                SetThemeToTitleBar("Dark");
            }
            Properties.Settings.Default.Save();
        }

        private void ApplySavedTheme()
        {
            string savedTheme = Properties.Settings.Default.ThemePreference;
            if (savedTheme == "Light")
            {
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary()
                {
                    Source = new Uri("LightTheme.xaml", UriKind.Relative)
                });
                ThemeBtn.IsChecked = false;
            }
            else if (savedTheme == "Dark")
            {
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary()
                {
                    Source = new Uri("DarkTheme.xaml", UriKind.Relative)
                });
                ThemeBtn.IsChecked = true;
            }
            SetThemeToTitleBar(savedTheme);
        }

        public void SetThemeToTitleBar(string enableDarkMode)
        {
            try 
            {
                var window = Application.Current.MainWindow;
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    int isDarkMode = enableDarkMode == "Dark" ? 1 : 0;
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref isDarkMode, sizeof(int));
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("SetThemeToTitleBar : Theme -> { " + enableDarkMode + " }: E @ SwitchTheme : " + e.ToString());
            }
        }
        #endregion

        private void Button_Click_Up(object sender, RoutedEventArgs e)
        {
            if (_matchIndices.Count == 0 )
                return;

            var _currentLogTab = (LogTabControl.SelectedItem as TabItem).Tag as LogTab;
            _currentMatchPosition = (_currentMatchPosition - 1 + _matchIndices.Count) % _matchIndices.Count;
            NavigateToMatch(_currentLogTab, _matchIndices[_currentMatchPosition]);
        }

        private void Button_Click_Down(object sender, RoutedEventArgs e)
        {
            if (_matchIndices.Count == 0)
                return;

            var _currentLogTab = (LogTabControl.SelectedItem as TabItem).Tag as LogTab;
            _currentMatchPosition = (_currentMatchPosition + 1 + _matchIndices.Count) % _matchIndices.Count;
            NavigateToMatch(_currentLogTab, _matchIndices[_currentMatchPosition]);
        }
    }
}
