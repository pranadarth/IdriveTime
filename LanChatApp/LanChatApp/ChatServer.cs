using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;

namespace LanChatApp
{
    public class ChatServer
    {
        private TcpListener _listener;
        private TcpClient _client;
        private CancellationTokenSource _cts;

        public event Action<string> MessageReceived;
        public event Action<string> StatusChanged;

        public async Task StartAsync(int port)
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _client = await _listener.AcceptTcpClientAsync();
            //StatusChanged?.Invoke("Client connected");
            _ = Task.Run(() => ListenForMessagesAsync(_client, _cts.Token));
        }

        private async Task ListenForMessagesAsync(TcpClient client, CancellationToken token)
        {
            var stream = client.GetStream();
            byte[] buffer = new byte[1024];
            try
            {
                while (!token.IsCancellationRequested)
                {
                    int byteCount = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (byteCount <= 0) break;
                    string message = Encoding.UTF8.GetString(buffer, 0, byteCount);
                    if (message.StartsWith("FILE|"))
                    {
                        string[] parts = message.Split('|');
                        if (parts.Length >= 3)
                        {
                            string fileName = parts[1];
                            int fileLength = int.Parse(parts[2]);

                            // Read the rest of the file bytes
                            byte[] fileBuffer = new byte[fileLength];
                            int totalRead = 0;
                            while (totalRead < fileLength)
                            {
                                int bytesRead = await stream.ReadAsync(fileBuffer, totalRead, fileLength - totalRead);
                                if (bytesRead == 0) break;
                                totalRead += bytesRead;
                            }

                            string savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
                            File.WriteAllBytes(savePath, fileBuffer);

                            MessageReceived?.Invoke($"Received file: {fileName} (Saved to Desktop)");
                        }
                    }
                    else
                    {
                        MessageReceived?.Invoke(message); // Normal text
                    }
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke("Error: " + ex.Message);
            }
        }

        public async Task SendMessageAsync(string message)
        {
            if (_client == null)
                return;
            var stream = _client.GetStream();
            byte[] data = Encoding.UTF8.GetBytes(message);
            await stream.WriteAsync(data, 0, data.Length);
        }
        public async Task SendFileAsync(string filePath)
        {
            if (!File.Exists(filePath)) return;

            byte[] fileBytes = File.ReadAllBytes(filePath);
            string fileName = Path.GetFileName(filePath);
            string header = $"FILE|{fileName}|{fileBytes.Length}|";
            byte[] headerBytes = Encoding.UTF8.GetBytes(header);

            var stream = _client.GetStream();
            if (stream == null) return;

            await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
            await stream.WriteAsync(fileBytes, 0, fileBytes.Length);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _client?.GetStream().Close();
            _client?.Close();
            _client?.Dispose();

            _listener?.Stop();
        }
    }
}
