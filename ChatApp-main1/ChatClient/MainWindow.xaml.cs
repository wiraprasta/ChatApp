// ChatClient/MainWindow.xaml.cs (Week 2 Update)

using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ChatApp;

namespace ChatClient
{
    public partial class MainWindow : Window
    {
        private TcpClient? client;
        private StreamReader? reader;
        private StreamWriter? writer;
        private bool isConnected = false;
        private const string HistoryFileName = "chat_history.log";

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            // ... (Kode sama seperti sebelumnya, tidak perlu diubah)
            if (isConnected) return;
            client = new TcpClient();
            try
            {
                await client.ConnectAsync(IpTextBox.Text, int.Parse(PortTextBox.Text));
                var utf8WithoutBom = new UTF8Encoding(false);
                var stream = client.GetStream();
                reader = new StreamReader(stream, utf8WithoutBom);
                writer = new StreamWriter(stream, utf8WithoutBom) { AutoFlush = true };
                var joinMessage = new Message { Type = "join", From = UsernameTextBox.Text, Timestamp = DateTime.Now };
                await SendMessageObject(joinMessage);
                isConnected = true;
                UpdateUiOnConnection(true);
                _ = Task.Run(() => ListenForMessages());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to connect to server: {ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                CleanupConnection();
            }
        }

        private async Task ListenForMessages()
        {
            try
            {
                while (isConnected && reader != null)
                {
                    var jsonMessage = await reader.ReadLineAsync();
                    if (jsonMessage == null) throw new IOException("Server connection closed.");

                    var message = JsonSerializer.Deserialize<Message>(jsonMessage);
                    if (message == null) continue;

                    await Dispatcher.Invoke(async () =>
                    {
                        string displayMessage = "";
                        string time = message.Timestamp.ToString("HH:mm:ss");

                        switch (message.Type)
                        {
                            case "msg":
                                displayMessage = $"[{time}] {message.From}: {message.Text}";
                                break;
                            case "pm":
                                displayMessage = $"[{time}] (Private from {message.From}): {message.Text}";
                                break;
                            case "sys":
                                displayMessage = $"[{time}] [SYSTEM]: {message.Text}";
                                break;
                            case "error":
                                displayMessage = $"[{time}] [ERROR]: {message.Text}";
                                if (message.Text != null && message.Text.Contains("taken")) Disconnect();
                                break;
                            case "userlist": // BARU: Menangani pesan daftar user
                                UpdateUserList(message.Text);
                                return; // Tidak perlu ditampilkan di chat box utama
                        }
                        
                        ChatListBox.Items.Add(displayMessage);
                        ChatListBox.ScrollIntoView(ChatListBox.Items[ChatListBox.Items.Count - 1]);
                        
                        // BONUS: Simpan pesan ke file history
                        await LogMessageToFileAsync(displayMessage);
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    if (isConnected) MessageBox.Show($"Connection lost: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Disconnect();
                });
            }
        }
        
        // BARU: Method untuk update list user di GUI
        private void UpdateUserList(string? userListText)
        {
            UserListView.Items.Clear();
            if (!string.IsNullOrEmpty(userListText))
            {
                var users = userListText.Split(',');
                foreach (var user in users)
                {
                    UserListView.Items.Add(user);
                }
            }
        }
        
        // BARU: Event handler untuk memulai PM dari daftar user
        private void UserListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (UserListView.SelectedItem is string username)
            {
                MessageTextBox.Text = $"/w {username} ";
                MessageTextBox.Focus();
                MessageTextBox.CaretIndex = MessageTextBox.Text.Length;
            }
        }

        private async Task SendMessage()
        {
            // ... (Kode sama seperti sebelumnya, tidak perlu diubah)
            if (!isConnected || string.IsNullOrWhiteSpace(MessageTextBox.Text)) return;
            string text = MessageTextBox.Text;
            var message = new Message { From = UsernameTextBox.Text };
            if (text.StartsWith("/w "))
            {
                var parts = text.Split(new[] { ' ' }, 3);
                if (parts.Length == 3) { message.Type = "pm"; message.To = parts[1]; message.Text = parts[2]; }
                else { ChatListBox.Items.Add("[SYSTEM]: Invalid PM format. Use: /w <user> <message>"); return; }
            }
            else { message.Type = "msg"; message.Text = text; }
            await SendMessageObject(message);
            MessageTextBox.Clear();
        }
        
        // ... (Method lainnya seperti SendMessageObject, Disconnect, CleanupConnection, UpdateUiOnConnection, dll tetap sama)
        private async Task SendMessageObject(Message message) { if (writer != null) { try { var jsonMessage = JsonSerializer.Serialize(message); await writer.WriteLineAsync(jsonMessage); } catch (Exception ex) { MessageBox.Show($"Failed to send: {ex.Message}"); } } }
        private void DisconnectButton_Click(object sender, RoutedEventArgs e) { Disconnect(); }
        private async void SendButton_Click(object sender, RoutedEventArgs e) { await SendMessage(); }
        private async void MessageTextBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) await SendMessage(); }
        private void Disconnect() { if (!isConnected) return; CleanupConnection(); UpdateUiOnConnection(false); ChatListBox.Items.Add("[SYSTEM]: You have been disconnected."); UserListView.Items.Clear(); }
        private void CleanupConnection() { isConnected = false; reader?.Close(); writer?.Close(); client?.Close(); reader = null; writer = null; client = null; }
        private void UpdateUiOnConnection(bool c) { isConnected = c; ConnectButton.IsEnabled = !c; DisconnectButton.IsEnabled = c; IpTextBox.IsEnabled = !c; PortTextBox.IsEnabled = !c; UsernameTextBox.IsEnabled = !c; MessageTextBox.IsEnabled = c; SendButton.IsEnabled = c; }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) { Disconnect(); }
        
        // BONUS: Method untuk menyimpan history chat
        private async Task LogMessageToFileAsync(string message)
        {
            try
            {
                await File.AppendAllTextAsync(HistoryFileName, $"{message}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                // Jika gagal, tampilkan di console debug saja agar tidak mengganggu user
                System.Diagnostics.Debug.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }

        // BONUS: Method untuk ganti tema
        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (ThemeToggleButton.IsChecked == true)
            {
                // Dark Mode
                AppWindow.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
                AppWindow.Foreground = new SolidColorBrush(Colors.White);
                ThemeToggleButton.Content = "☀️ Light Mode";
            }
            else
            {
                // Light Mode
                AppWindow.Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                AppWindow.Foreground = new SolidColorBrush(Colors.Black);
                ThemeToggleButton.Content = "🌙 Dark Mode";
            }
        }
    }
}