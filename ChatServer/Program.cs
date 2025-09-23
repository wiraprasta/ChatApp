using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class ChatServer
{
    private static TcpListener _listener = new TcpListener(IPAddress.Any, 8888);
    private static Dictionary<string, TcpClient> _clients = new Dictionary<string, TcpClient>();
    private static object _lock = new object();

    public static async Task Main(string[] args)
    {
        _listener.Start();
        Console.WriteLine("Server started on port 8888. Waiting for clients...");

        while (true)
        {
            TcpClient client = await _listener.AcceptTcpClientAsync();
            Console.WriteLine("New client connected!");
            
            _ = Task.Run(() => HandleClientAsync(client));
        }
    }

    private static async Task HandleClientAsync(TcpClient client)
    {
        var stream = client.GetStream();
        var reader = new StreamReader(stream, Encoding.UTF8);
        string? username = null;
        
        try
        {
            username = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(username)) return;

            lock (_lock)
            {
                _clients.Add(username, client);
            }
            
            await BroadcastMessageAsync($"{username} has joined the chat.", null);
            await SendUserListAsync();
            
            while (true)
            {
                string? jsonMessage = await reader.ReadLineAsync();
                if (jsonMessage == null) break; 
                
                if (jsonMessage.StartsWith("{") && jsonMessage.EndsWith("}"))
                {
                    await BroadcastMessageAsync(jsonMessage, client);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client {username}: {ex.Message}");
        }
        finally
        {
            if (username != null)
            {
                lock (_lock)
                {
                    _clients.Remove(username);
                }
                await BroadcastMessageAsync($"{username} has left the chat.", null);
                await SendUserListAsync();
            }
            client.Close();
        }
    }

    private static async Task BroadcastMessageAsync(string message, TcpClient? sender)
    {
        var msg = new { type = "chat", text = message };
        string json = JsonSerializer.Serialize(msg);
        byte[] buffer = Encoding.UTF8.GetBytes(json + Environment.NewLine);
        
        List<TcpClient> clientList;
        lock (_lock)
        {
            clientList = _clients.Values.ToList();
        }

        foreach (var client in clientList)
        {
            var stream = client.GetStream();
            try
            {
                await stream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch { }
        }
    }

    private static async Task SendUserListAsync()
    {
        var users = new { type = "users", list = _clients.Keys.ToArray() };
        string json = JsonSerializer.Serialize(users);
        byte[] buffer = Encoding.UTF8.GetBytes(json + Environment.NewLine);

        List<TcpClient> clientList;
        lock (_lock)
        {
            clientList = _clients.Values.ToList();
        }

        foreach (var client in clientList)
        {
            try
            {
                await client.GetStream().WriteAsync(buffer, 0, buffer.Length);
            }
            catch { }
        }
    }
}