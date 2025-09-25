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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;
using System.Net.Sockets;

namespace UdpChatApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private UdpClient _udpClient;
        private int _port;

        public MainWindow()
        {
            InitializeComponent();
            _port = int.Parse(PortInput.Text);
            StartListening();
        }

        private void StartListening()
        {
            Task.Run(() =>
            {
                _udpClient = new UdpClient(_port);
                var endPoint = new IPEndPoint(IPAddress.Any, _port);

                try
                {
                    while (true)
                    {
                        // Receive UDP packets
                        var receivedBytes = _udpClient.Receive(ref endPoint);
                        var receivedMessage = Encoding.UTF8.GetString(receivedBytes);

                        // Update the chat messages on the UI thread
                        Dispatcher.Invoke(() =>
                        {
                            ChatMessages.Items.Add("Received: " + receivedMessage);
                        });
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => ChatMessages.Items.Add("Error: " + ex.Message));
                }
            });
        }

        private void SendMessage_Click(object sender, RoutedEventArgs e)
        {
            string ipAddress = IpAddressInput.Text;
            int port = int.Parse(PortInput.Text);
            string message = MessageInput.Text;

            if (string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show("Please enter a message to send.");
                return;
            }

            var udpClient = new UdpClient();
            try
            {
                var sendBytes = Encoding.UTF8.GetBytes(message);
                var endPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);

                // Send the UDP packet
                udpClient.Send(sendBytes, sendBytes.Length, endPoint);
                ChatMessages.Items.Add("Sent: " + message);
                MessageInput.Clear();
            }
            catch (Exception ex)
            {
                ChatMessages.Items.Add("Error: " + ex.Message);
            }
            finally
            {
                udpClient.Close();
            }
        }
    }
}
