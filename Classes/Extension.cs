using DSharpPlus;
using DSharpPlus.Entities;

namespace ElmerBot
{
    internal static class DateTimeExtensions
    {
        public static string ToDiscordDisplay(this DateTime? dateTime, TimeFormat format = TimeFormat.LongDateTime) => dateTime.HasValue ? dateTime.Value.ToDiscordDisplay(format) : "";
        public static string ToDiscordDisplay(this DateTime dateTime, TimeFormat format = TimeFormat.LongDateTime) => $"<t:{((DateTimeOffset)dateTime).ToUnixTimeSeconds()}" + format switch
        {
            TimeFormat.Relative => ":R",
            TimeFormat.ShortTime => ":t",
            TimeFormat.LongTime => ":T",
            TimeFormat.ShortDate => ":d",
            TimeFormat.LongDate => ":D",
            _ => ":f"
        } + ">";
    }

    internal enum TimeFormat
    {
        Relative,
        ShortTime,
        LongTime,
        ShortDate,
        LongDate,
        LongDateTime
    }

    internal static class DiscordGuildExtensions
    {
        public static async Task<DiscordMember> GetBotMember(this DiscordGuild guild, DiscordClient client) => await guild.GetMemberAsync(client.CurrentUser.Id);
    }
}
