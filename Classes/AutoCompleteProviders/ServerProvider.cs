using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;

namespace ElmerBot.Classes.AutoCompleteProviders
{

    internal class ServersProvider : IAutoCompleteProvider
    {
        public ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext context)
        {
            IEnumerable<DiscordAutoCompleteChoice> options = context.Client.GetGuildsAsync()
                .Where(g => g.Name.Contains(context.UserInput!, StringComparison.OrdinalIgnoreCase))
                .Select(g => new DiscordAutoCompleteChoice(g.Name + "(ID: " + g.Id + ")", g.Id.ToString()))
                .ToListAsync().Result;

            return ValueTask.FromResult(options);
        }
    }
}
