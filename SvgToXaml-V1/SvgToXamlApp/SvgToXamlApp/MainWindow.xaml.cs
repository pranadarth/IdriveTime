using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Windows;
using System.Windows.Controls;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace SvgToXamlApp
{
    public partial class MainWindow : Window
    {
        private FileInfo _currentSelectedFile = null;

        public MainWindow()
        {
            InitializeComponent();
            LoadMemory(); 
        }

        // --- ZERO-SETUP FOLDER MEMORY LOGIC ---
        private string GetConfigFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appData, "InternalSvgToXamlTool");
            if (!Directory.Exists(appFolder)) Directory.CreateDirectory(appFolder);
            return Path.Combine(appFolder, "lastfolder.txt");
        }

        private void LoadMemory()
        {
            try
            {
                string configPath = GetConfigFilePath();
                if (File.Exists(configPath))
                {
                    string lastFolder = File.ReadAllText(configPath);
                    if (Directory.Exists(lastFolder))
                    {
                        LoadDirectory(lastFolder);
                    }
                }
            }
            catch { /* Ignore if it fails */ }
        }

        // --- FILE BROWSING LOGIC ---
        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select a folder containing SVG files";

                // Point dialog to the last used folder if available
                string configPath = GetConfigFilePath();
                if (File.Exists(configPath))
                {
                    string lastPath = File.ReadAllText(configPath);
                    if (Directory.Exists(lastPath)) dialog.SelectedPath = lastPath;
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    // Save the memory
                    File.WriteAllText(configPath, dialog.SelectedPath);
                    LoadDirectory(dialog.SelectedPath);
                }
            }
        }

        private void LoadDirectory(string path)
        {
            TxtCurrentFolder.Text = path;
            try
            {
                var directoryInfo = new DirectoryInfo(path);
                var svgFiles = directoryInfo.GetFiles("*.svg").OrderBy(f => f.Name).ToList();

                LstFiles.ItemsSource = svgFiles;

                if (svgFiles.Count > 0)
                    LstFiles.SelectedIndex = 0;
                else
                    ResetView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load directory: {ex.Message}");
            }
        }

        private void LstFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstFiles.SelectedItem is FileInfo fileInfo)
            {
                _currentSelectedFile = fileInfo;
                ProcessSvg(fileInfo);
            }
        }

        // --- SINGLE FILE PROCESSING ---
        private void ProcessSvg(FileInfo file)
        {
            TxtPlaceholder.Visibility = Visibility.Collapsed;

            try
            {
                SvgPreview.Source = new Uri(file.FullName);
            }
            catch
            {
                SvgPreview.Source = null;
                TxtPlaceholder.Text = "Preview Error: Invalid SVG Visuals";
                TxtPlaceholder.Visibility = Visibility.Visible;
            }

            try
            {
                var settings = new WpfDrawingSettings { IncludeRuntime = false, TextAsGeometry = true, OptimizePath = true };
                var converter = new FileSvgConverter(settings);
                string tempXamlFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".xaml");

                converter.Convert(file.FullName, tempXamlFile);
                TxtXamlCode.Text = File.ReadAllText(tempXamlFile);
                File.Delete(tempXamlFile);
            }
            catch (Exception ex)
            {
                TxtXamlCode.Text = $"\n";
            }
        }

        // --- EXPORT ALL AS RESOURCE DICTIONARY ---
        // --- EXPORT ALL AS RESOURCE DICTIONARY ---
        private void BtnExportAll_Click(object sender, RoutedEventArgs e)
        {
            if (LstFiles.Items.Count == 0)
            {
                MessageBox.Show("No SVG files to export.", "Empty", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "XAML Resource Dictionary (*.xaml)|*.xaml",
                FileName = "IconsDictionary.xaml"
            };

            if (saveDialog.ShowDialog() == true)
            {
                StringBuilder dictionaryBuilder = new StringBuilder();
                dictionaryBuilder.AppendLine("<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"");
                dictionaryBuilder.AppendLine("                    xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">");

                int successCount = 0;
                var settings = new WpfDrawingSettings { IncludeRuntime = false, TextAsGeometry = true, OptimizePath = true };
                var converter = new FileSvgConverter(settings);

                foreach (FileInfo file in LstFiles.ItemsSource)
                {
                    try
                    {
                        string tempXamlFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".xaml");
                        converter.Convert(file.FullName, tempXamlFile);
                        string xaml = File.ReadAllText(tempXamlFile);
                        File.Delete(tempXamlFile);

                        // Parse the generated XAML to inject the x:Key dictionary attribute
                        XmlDocument doc = new XmlDocument();
                        doc.LoadXml(xaml);

                        var rootElement = doc.DocumentElement;
                        if (rootElement != null)
                        {
                            // 1. Generate a valid x:Key name based on the filename
                            string keyName = Path.GetFileNameWithoutExtension(file.Name).Replace(" ", "_").Replace("-", "_");
                            if (char.IsDigit(keyName[0])) keyName = "_" + keyName; // x:Key cannot start with a number

                            // 2. Add the x:Key attribute
                            var keyAttr = doc.CreateAttribute("x", "Key", "http://schemas.microsoft.com/winfx/2006/xaml");
                            keyAttr.Value = keyName;
                            rootElement.Attributes.Append(keyAttr);

                            // 3. Re-format the XML to ensure it remains beautifully indented
                            var xmlSettings = new XmlWriterSettings
                            {
                                Indent = true,
                                IndentChars = "    ", // 4 spaces for indentation
                                OmitXmlDeclaration = true
                            };

                            string formattedXml = "";
                            using (var sw = new StringWriter())
                            using (var xw = XmlWriter.Create(sw, xmlSettings))
                            {
                                rootElement.WriteTo(xw);
                                xw.Flush();
                                formattedXml = sw.ToString();
                            }

                            // Pad the output slightly so it is cleanly nested inside the <ResourceDictionary> tag
                            formattedXml = string.Join(Environment.NewLine, formattedXml.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Select(line => "    " + line));

                            dictionaryBuilder.AppendLine($"\n    ");
                            dictionaryBuilder.AppendLine(formattedXml);
                            successCount++;
                        }
                    }
                    catch
                    {
                        // Safely skip any files that SharpVectors fails to process
                        dictionaryBuilder.AppendLine($"\n    ");
                    }
                }

                dictionaryBuilder.AppendLine("\n</ResourceDictionary>");
                File.WriteAllText(saveDialog.FileName, dictionaryBuilder.ToString());

                MessageBox.Show($"Successfully bundled {successCount} icons into Resource Dictionary!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // --- HELPER BUTTONS ---
        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(TxtXamlCode.Text))
            {
                Clipboard.SetText(TxtXamlCode.Text);
                MessageBox.Show("XAML copied to clipboard!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSelectedFile == null || string.IsNullOrWhiteSpace(TxtXamlCode.Text)) return;

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "XAML Files (*.xaml)|*.xaml",
                FileName = Path.ChangeExtension(_currentSelectedFile.Name, ".xaml")
            };

            if (saveDialog.ShowDialog() == true)
            {
                File.WriteAllText(saveDialog.FileName, TxtXamlCode.Text);
                MessageBox.Show("File saved successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ResetView()
        {
            _currentSelectedFile = null;
            SvgPreview.Source = null;
            TxtXamlCode.Text = "";
            TxtPlaceholder.Text = "No SVGs found in folder";
            TxtPlaceholder.Visibility = Visibility.Visible;
        }
    }
}