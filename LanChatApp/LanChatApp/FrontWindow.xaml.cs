using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
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
using System.Windows.Threading;
using System.Net.NetworkInformation;

namespace LanChatApp
{
    /// <summary>
    /// Interaction logic for FrontWindow.xaml
    /// </summary>
    public partial class FrontWindow : Window
    {
        public FrontWindow()
        {
            InitializeComponent();
        }

        private void NavigateTo(string page)
        {
            MainGrid.Visibility = Visibility.Collapsed;
            GenerateGrid.Visibility = Visibility.Collapsed;
            EnterGrid.Visibility = Visibility.Collapsed;

            Error.Content = "";

            switch (page)
            {
                case "Home": MainGrid.Visibility = Visibility.Visible; break;
                case "Generator": GenerateGrid.Visibility = Visibility.Visible; break;
                case "EnterGrid": EnterGrid.Visibility = Visibility.Visible; break;
            }
        }

        private string GetLocalIPAddress()
        {
            int count = 0;
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                {
                    var ipProps = ni.GetIPProperties();
                    foreach (UnicastIPAddressInformation ip in ipProps.UnicastAddresses)
                    {
                        count++;
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            return ip.Address.ToString();
                        }
                    }
                }
            }
            return "127.0.0.1";
        }

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo("Home");
        }

        private async void Generator_Click(object sender, RoutedEventArgs e)
        {

            Error.Content = "";
            if (String.IsNullOrEmpty(MyName.Text))
            {
                Error.Content = "Please enter the name to proceed";
                return;
            }

            NavigateTo("Generator");

            string ipAddress = GetLocalIPAddress();
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(ipAddress));
            encoded = encoded.TrimEnd('=').Replace('+', '-').Replace('/', '_');
            Code.Text = encoded;

            MainWindow mw = MainWindow.CreateMainWindowObj(true, "", MyName.Text, this);
            await mw.Emoji_Loaded();
        }

        private void EnteredGrid_Click(object sender, RoutedEventArgs e)
        {
            Error.Content = "";
            if (String.IsNullOrEmpty(MyName.Text))
            {
                Error.Content = "Please enter the name to proceed";
                return;
            }

            NavigateTo("EnterGrid");
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(Code.Text);
            if (CopyBtn.ToolTip is ToolTip tt)
            {
                tt.Content = "Copied!";
                tt.IsOpen = true;

                // Auto-close after 1 second
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                timer.Tick += (s, args) =>
                {
                    tt.IsOpen = false;
                    tt.Content = "Copy";
                    timer.Stop();
                };
                timer.Start();
            }
        }

        private async void EnterBtn_Click(object sender, RoutedEventArgs e)
        {
            Error.Content = "";
            if (String.IsNullOrEmpty(EnteredCode.Text))
            {
                Error.Content = "Please enter code to proceed";
                return;
            }
            string code = EnteredCode.Text.Trim();

            code = code.Replace(" ", "").Replace("\n", "").Replace("\r", "");

            try
            {
                int mod = code.Length % 4;
                if (mod > 0)
                {
                    code = code.PadRight(code.Length + (4 - mod), '=');
                }

                string decodedIp = Encoding.UTF8.GetString(Convert.FromBase64String(code));
                MainWindow mw = MainWindow.CreateMainWindowObj(false, decodedIp,MyName.Text, this);
                await mw.Emoji_Loaded();
            }
            catch (FormatException)
            {
                Error.Content = "Invalid code format!";
            }
        }
    }
}
