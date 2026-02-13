namespace ElmerBot.Models
{
    internal class GluedMessage
    {
        public ulong Server_ID { get; set; }
        public ulong Channel_ID { get; set; }
        public string Message { get; set; }
        public ulong Message_ID { get; set; }
        public string? Username { get; set; } = null;
        public string? Avatar_Url { get; set; } = null;
        public bool isWatching { get; set; } = false;
    }
}
