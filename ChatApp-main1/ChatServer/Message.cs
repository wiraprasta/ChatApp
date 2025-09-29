// Message.cs
// Simpan file ini di proyek ChatServer dan ChatClient

namespace ChatApp
{
    public class Message
    {
        public string Type { get; set; } = "msg"; // msg, pm, join, leave, error
        public string? From { get; set; }
        public string? To { get; set; }
        public string? Text { get; set; }
        public DateTime Timestamp { get; set; }
    }
}