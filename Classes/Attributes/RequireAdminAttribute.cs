using ElmerBot.Models;
using DSharpPlus.Commands.ContextChecks;
using Microsoft.Extensions.Options;
using DSharpPlus.Commands;

namespace ElmerBot.Classes.Attributes
{
    internal class RequireAdminAttribute : ContextCheckAttribute { }

    internal class RequireAdminCheck(IOptionsSnapshot<Settings> config) : IContextCheck<RequireAdminAttribute>
    {
        Settings settings => config.Value;

        public ValueTask<string?> ExecuteCheckAsync(RequireAdminAttribute attribute, CommandContext context) => Validate(context);

        public ValueTask<string?> Validate(CommandContext context)
        {
            if (
                settings.Admin.UserID != context.Member?.Id
            )
                return ValueTask.FromResult<string?>("The executing user does not have the needed permissions to execute this command.");

            return ValueTask.FromResult<string?>(null);
        }
    }
}
