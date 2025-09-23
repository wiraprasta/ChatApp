using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ChatClient
{
    public partial class MainWindow : Window
    {
        private TcpClient _client = null!;
        private NetworkStream _stream = null!;
        private StreamReader _reader = null!;
        private StreamWriter _writer = null!;
        private bool _isConnected = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(txtIp.Text, int.Parse(txtPort.Text));
                
                _stream = _client.GetStream();
                _reader = new StreamReader(_stream, Encoding.UTF8);
                _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true };
                
                await _writer.WriteLineAsync(txtUsername.Text);

                _isConnected = true;
                btnConnect.IsEnabled = false;
                btnDisconnect.IsEnabled = true;
                txtMessage.IsEnabled = true;
                btnSend.IsEnabled = true;
                
                lbChatLog.Items.Add("Connected to server!");
                
                _ = Task.Run(() => ReceiveMessagesAsync());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed: {ex.Message}");
            }
        }
        
        private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            _isConnected = false;
            _client?.Close();
            
            btnConnect.IsEnabled = true;
            btnDisconnect.IsEnabled = false;
            txtMessage.IsEnabled = false;
            btnSend.IsEnabled = false;
            lbChatLog.Items.Add("Disconnected from server.");
        }
        
        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected || string.IsNullOrWhiteSpace(txtMessage.Text)) return;

            var msg = new { type = "chat", text = $"[{txtUsername.Text}]: {txtMessage.Text}" };
            string json = JsonSerializer.Serialize(msg);

            await _writer.WriteLineAsync(json);
            txtMessage.Clear();
        }
        
        private async Task ReceiveMessagesAsync()
        {
            try
            {
                while (_isConnected)
                {
                    string? jsonMessage = await _reader.ReadLineAsync();
                    if (jsonMessage == null) break; 
                    
                    try
                    {
                        using (JsonDocument doc = JsonDocument.Parse(jsonMessage))
                        {
                            JsonElement root = doc.RootElement;
                            string type = root.GetProperty("type").GetString() ?? string.Empty;

                            if (type == "chat")
                            {
                                string text = root.GetProperty("text").GetString() ?? string.Empty;
                                Dispatcher.Invoke(() => lbChatLog.Items.Add(text));
                            }
                            else if (type == "users")
                            {
                                var userList = root.GetProperty("list").EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToList();
                                Dispatcher.Invoke(() =>
                                {
                                    lbActiveUsers.Items.Clear();
                                    foreach (var user in userList)
                                    {
                                        lbActiveUsers.Items.Add(user);
                                    }
                                });
                            }
                        }
                    }
                    catch (JsonException)
                    {
                    }
                }
            }
            catch
            {
                Dispatcher.Invoke(() =>
                {
                    lbChatLog.Items.Add("Connection to server lost.");
                    BtnDisconnect_Click(null, null);
                });
            }
        }

        private void TxtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnSend_Click(sender, e);
            }
        }
    }
}