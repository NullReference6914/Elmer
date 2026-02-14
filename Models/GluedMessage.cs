namespace ElmerBot.Models
{
    internal class GluedMessage
    {
        public ulong Server_ID { get; set; }
        public ulong Channel_ID { get; set; }
        public string Message { get; set; }
        public ulong? Message_ID { get; set; }
        public string? Username { get; set; } = null;
        public string? Avatar_Url { get; set; } = null;
        public bool isWatching { get; set; } = false;
        public int Channel_Errors { get; set; } = 0;
        public int Webhook_Errors { get; set; } = 0;

        public GluedMessage() { }
        public GluedMessage(GluedMessage message)
        {
            Server_ID = message.Server_ID;
            Channel_ID = message.Channel_ID;
            Message = message.Message;
            Message_ID = message.Message_ID;
            Username = message.Username;
            Avatar_Url = message.Avatar_Url;
            isWatching = message.isWatching;
            Channel_Errors = message.Channel_Errors;
            Webhook_Errors = message.Webhook_Errors;
        }
    }
}
