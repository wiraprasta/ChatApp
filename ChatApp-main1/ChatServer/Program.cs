// ChatServer/Program.cs (Week 2 Update)

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Linq;
using ChatApp;

class Server
{
    private static ConcurrentDictionary<string, TcpClient> clients = new ConcurrentDictionary<string, TcpClient>();
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

    static async Task Main(string[] args)
    {
        TcpListener server = new TcpListener(IPAddress.Any, 8888);
        server.Start();
        Console.WriteLine($"[LOG] Server started on port 8888 at {DateTime.Now}");

        while (true)
        {
            TcpClient client = await server.AcceptTcpClientAsync();
            _ = Task.Run(() => HandleClient(client));
        }
    }

    static async Task HandleClient(TcpClient tcpClient)
    {
        string currentUsername = string.Empty;
        var clientEndpoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown";
        Console.WriteLine($"[LOG] New client connected from {clientEndpoint}. Waiting for username...");

        NetworkStream stream = tcpClient.GetStream();
        var reader = new StreamReader(stream, Utf8WithoutBom);

        try
        {
            var initialJson = await reader.ReadLineAsync();
            if (initialJson == null) return;

            var initialMsg = JsonSerializer.Deserialize<Message>(initialJson);
            if (initialMsg?.Type == "join" && !string.IsNullOrEmpty(initialMsg.From))
            {
                currentUsername = initialMsg.From;
                if (clients.TryAdd(currentUsername, tcpClient))
                {
                    Console.WriteLine($"[LOG] Client '{currentUsername}' from {clientEndpoint} authenticated successfully.");
                    var joinNotification = new Message { Type = "sys", Text = $"'{currentUsername}' has joined the chat.", Timestamp = DateTime.Now };
                    await BroadcastMessage(joinNotification, currentUsername);
                    await BroadcastUserList(); // BARU: Kirim daftar user ke semua client
                }
                else
                {
                    Console.WriteLine($"[WARN] Client from {clientEndpoint} failed to join with duplicate username '{currentUsername}'.");
                    var errorMsg = new Message { Type = "error", Text = "Username is already taken.", Timestamp = DateTime.Now };
                    await SendMessage(tcpClient, errorMsg);
                    tcpClient.Close();
                    return;
                }
            }
            else
            {
                Console.WriteLine($"[WARN] Invalid join protocol from {clientEndpoint}. Connection closed.");
                tcpClient.Close();
                return;
            }

            while (tcpClient.Connected)
            {
                var jsonMessage = await reader.ReadLineAsync();
                if (jsonMessage == null) break;

                var message = JsonSerializer.Deserialize<Message>(jsonMessage);
                if (message == null) continue;

                message.From = currentUsername;
                message.Timestamp = DateTime.Now;

                if (message.Type == "pm")
                {
                    Console.WriteLine($"[MSG] Private message from '{message.From}' to '{message.To}'.");
                    await SendPrivateMessage(message);
                }
                else
                {
                    Console.WriteLine($"[MSG] Broadcast from '{message.From}'.");
                    await BroadcastMessage(message);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Error with client '{currentUsername}': {ex.Message}");
        }
        finally
        {
            if (!string.IsNullOrEmpty(currentUsername))
            {
                clients.TryRemove(currentUsername, out _);
                Console.WriteLine($"[LOG] Client '{currentUsername}' disconnected.");
                var leaveNotification = new Message { Type = "sys", Text = $"'{currentUsername}' has left the chat.", Timestamp = DateTime.Now };
                await BroadcastMessage(leaveNotification);
                await BroadcastUserList(); // BARU: Update daftar user setelah ada yang keluar
            }
            tcpClient.Close();
        }
    }

    static async Task SendMessage(TcpClient client, Message message)
    {
        try
        {
            if (client.Connected)
            {
                var stream = client.GetStream();
                var writer = new StreamWriter(stream, Utf8WithoutBom) { AutoFlush = true };
                var jsonMessage = JsonSerializer.Serialize(message);
                await writer.WriteLineAsync(jsonMessage);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to send message: {ex.Message}");
        }
    }

    static async Task BroadcastMessage(Message message, string? excludeUsername = null)
    {
        foreach (var (username, client) in clients)
        {
            if (username != excludeUsername)
            {
                await SendMessage(client, message);
            }
        }
    }
    
    // BARU: Method untuk broadcast daftar user online
    static async Task BroadcastUserList()
    {
        var userList = string.Join(",", clients.Keys.OrderBy(name => name));
        var userListMessage = new Message
        {
            Type = "userlist",
            Text = userList,
            Timestamp = DateTime.Now
        };
        Console.WriteLine($"[LOG] Broadcasting updated user list: {userList}");
        await BroadcastMessage(userListMessage);
    }

    static async Task SendPrivateMessage(Message message)
    {
        if (!string.IsNullOrEmpty(message.To) && clients.TryGetValue(message.To, out var recipientClient))
        {
            await SendMessage(recipientClient, message);
        }
        if (!string.IsNullOrEmpty(message.From) && clients.TryGetValue(message.From, out var senderClient))
        {
            await SendMessage(senderClient, message);
        }
    }
}