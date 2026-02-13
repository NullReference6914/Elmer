using ElmerBot.Models;
using DSharpPlus.Commands.ContextChecks;
using Microsoft.Extensions.Options;

namespace ElmerBot.Classes.Attributes
{
    internal class BasicUserCheckAttribute : ContextCheckAttribute { }

    internal class BasicUserCheck(IOptionsSnapshot<Settings> config) : IContextCheck<BasicUserCheckAttribute>
    {
        Settings settings => config.Value;
        public ValueTask<string?> ExecuteCheckAsync(BasicUserCheckAttribute attribute, DSharpPlus.Commands.CommandContext context)
        {
            if(context.User.IsBot)
                return ValueTask.FromResult<string?>("Bots are not allowed to use this command.");

            return ValueTask.FromResult<string?>(null);
        }
    }
}
