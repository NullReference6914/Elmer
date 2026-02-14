using System.Text.Json;

namespace ElmerBot.Models
{
    internal class Settings
    {
        public string Token { get; set; } = string.Empty;
        public AdminSettings Admin { get; set; } = new();
        public List<ulong> EnabledServers { get; set; } = [];

        public async Task<(bool Success, Exception ex)> Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(this);

                await File.WriteAllTextAsync($"{AppDomain.CurrentDomain.BaseDirectory}\\settings.json", json);
                return (true, null!);
            }
            catch (Exception ex)
            {
                return (false, ex);
            }
        }
    }

    internal class AdminSettings
    {
        public ulong UserID { get; set; }
        public ulong? ChannelID { get; set; }
        public ulong? ServerID { get; set; }
    }
}
