using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;
using ElmerBot.Enums;

namespace ElmerBot.Classes.ChoiceProvider
{
    internal class MemberOutputTypeProvider : IChoiceProvider
    {
        private static readonly IEnumerable<DiscordApplicationCommandOptionChoice> options =
        [.. Enum.GetValues<MemberOutputType>().Select(v => new DiscordApplicationCommandOptionChoice(v.ToString(), (int)v))];

    public ValueTask<IEnumerable<DiscordApplicationCommandOptionChoice>> ProvideAsync(CommandParameter parameter) => ValueTask.FromResult(options);
}
}
