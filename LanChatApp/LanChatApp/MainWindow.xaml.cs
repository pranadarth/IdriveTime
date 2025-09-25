using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LanChatApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private ChatServer _server;
        private ChatClient _client;
        private const int Port = 5000;
        private List<EmojiData> _dataEmojis;
        private bool iSserver;
        private string ServerIP;
        private FrontWindow fw;
        private string MyName = "You";
        private string RemoteUserName = "Friend";

        public List<EmojiData> DataEmojis
        {
            get => _dataEmojis;
            set
            {
                _dataEmojis = value;
                OnPropertyChanged();
            }
        }

        private MainWindow(bool IsServer, string IP, string name, object x)
        {
            InitializeComponent();
            DataContext = this;

            iSserver = IsServer;
            ServerIP = IP;
            MyName = name;
            fw = x as FrontWindow;

            StartButton_Click();
        }

        private static MainWindow mw;

        public static MainWindow CreateMainWindowObj(bool IsServer, string IP, string name, object x)
        {
            if (mw == null)
                mw = new MainWindow(IsServer, IP, name, x);
            return mw;
        }

        public async Task Emoji_Loaded()
        {
            // Delay briefly to let the main UI finish rendering.
            LoadEmojis();
            await Task.Delay(500);
            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                EmojiPopup.IsOpen = true;
                EmojiPopup.IsOpen = false;
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void LoadEmojis()
        {
            try
            {
                DataEmojis = JsonConvert.DeserializeObject<List<EmojiData>>(EmojiResource.EmojisListJson);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading emojis: " + ex.Message);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text.ToLower();


            DataEmojis.AsParallel().ForAll(emoji =>
            {
                emoji.IsVisible = string.IsNullOrWhiteSpace(query) || emoji.UnicodeName.ToLower().Contains(query)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            });
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            var a = sender as Button;
            MessageTextBox.Text += a.Tag.ToString();
        }



        private async void StartButton_Click()
        {
            if (iSserver == true)
            {
                // Start as server.
                _server = new ChatServer();
                _server.StatusChanged += AppendMessage;
                _server.MessageReceived += (msg) => AppendMessage(msg, 1);
                await _server.StartAsync(Port);
                await _server.SendMessageAsync("_NAME:" + MyName);
                LauchTheChatPage();
            }
            else
            {
                // Start as client.
                _client = new ChatClient();
                _client.StatusChanged += AppendMessage;
                _client.MessageReceived += (msg) => AppendMessage(msg, 1);
                await _client.ConnectAsync(ServerIP, Port);
                await _client.SendMessageAsync("_NAME:" + MyName);
                LauchTheChatPage();
            }

        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string message = MessageTextBox.Text;
            if (string.IsNullOrWhiteSpace(message))
                return;

            AppendMessage(message, 0);

            if (iSserver == true && _server != null)
            {
                await _server.SendMessageAsync(message);
            }
            else if (_client != null)
            {
                await _client.SendMessageAsync(message);
            }
            scroller.ScrollToEnd();
            MessageTextBox.Clear();
            MessageTextBox.Focus();
        }

        private void enterKey(object sender, KeyEventArgs e)
        {

            if (e.Key == Key.Enter)
            {
                SendButton_Click(sender, e);

            }

        }

        private void LauchTheChatPage()
        {
            this.Show();
            fw.Close();
        }

        private void AppendMessage(string message)
        {

            AppendMessage(message, 1);
        }

        private void AppendMessage(string message, int x = 0)
        {

            if (message.Contains("_NAME:"))
            {
                RemoteUserName = message.Substring(6);
                return;
            }
            Dispatcher.Invoke(() =>
            {
                CreateATextBox(message, x);
            });
        }

        private async void CreateATextBox(string Data, int responseSide)
        {

            Border bx = new Border();
            bx.BorderThickness = new Thickness(0);
            bx.VerticalAlignment = VerticalAlignment.Top;
            bx.Margin = new Thickness(10, 5, 10, 0);

            TextBlock txtb = new TextBlock();
            txtb.FontWeight = FontWeights.Medium;
            txtb.MaxWidth = 600;
            txtb.FontSize = 14;
            txtb.Padding = new Thickness(10, 0, 10, 0);
            txtb.TextWrapping = TextWrapping.Wrap;
            txtb.Margin = new Thickness(5);
            if (responseSide == 0)
            {
                Data = "Me: " + Data;
                bx.Background = new SolidColorBrush(Colors.LightSlateGray);
                bx.CornerRadius = new CornerRadius(15, 0, 15, 15);
                bx.HorizontalAlignment = HorizontalAlignment.Right;

            }
            else
            {
                Data = RemoteUserName + ": " + Data;
                bx.Background = new SolidColorBrush(Colors.LightSeaGreen);
                bx.CornerRadius = new CornerRadius(0, 15, 15, 15);
                bx.HorizontalAlignment = HorizontalAlignment.Left;
            }

            txtb.Text = Data;

            bx.Child = txtb;
            TextArea.Children.Add(bx);
        }

        private void EmojiButton_Click(object sender, RoutedEventArgs e)
        {
            EmojiPopup.IsOpen = !EmojiPopup.IsOpen;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }


        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            _server?.Stop();
            _client?.Disconnect();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Application.Current.Shutdown(); // <-- ensures app exits fully
        }

        private async void sendBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            if (dlg.ShowDialog() == true)            {
                string filePath = dlg.FileName;
                AppendMessage("Sending file: " + System.IO.Path.GetFileName(filePath), 0);

                if (iSserver)
                    await _server.SendFileAsync(filePath);
                else
                    await _client.SendFileAsync(filePath);
            }
        }
    }
}
