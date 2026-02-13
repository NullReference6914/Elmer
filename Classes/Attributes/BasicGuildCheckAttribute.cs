using ElmerBot.Models;
using DSharpPlus.Commands.ContextChecks;
using Microsoft.Extensions.Options;

namespace ElmerBot.Classes.Attributes
{
    internal class BasicGuildCheckAttribute : ContextCheckAttribute { }

    internal class BasicGuildCheck(IOptionsSnapshot<Settings> config) : IContextCheck<BasicGuildCheckAttribute>
    {
        Settings settings => config.Value;

        public ValueTask<string?> ExecuteCheckAsync(BasicGuildCheckAttribute attribute, DSharpPlus.Commands.CommandContext context)
        {
            if(context.Guild is null)
            {
                return ValueTask.FromResult<string?>("This command can only be used in a server.");
            }
            else if (!this.settings.EnabledServers.Contains(context.Guild!.Id) && this.settings.Admin.ServerID != context.Guild!.Id)
            {
                return ValueTask.FromResult<string?>("This server is has not been enabled to use this bot by the host.");
            }

            return ValueTask.FromResult<string?>(null);
        }
    }
}
