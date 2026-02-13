using ElmerBot.Models;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using Microsoft.Extensions.Options;

namespace ElmerBot.Classes.Attributes
{
    internal class CustomRequirePermissionsAttribute : RequireGuildAttribute
    {
        public DiscordPermissions BotPermissions { get; init; }

        public DiscordPermissions UserPermissions { get; init; }

        public bool AllowAdmins { get; init; } = true;

        public CustomRequirePermissionsAttribute(bool admins = true, params DiscordPermission[] permissions)
        {
            BotPermissions = (UserPermissions = new DiscordPermissions((IReadOnlyList<DiscordPermission>)permissions));

            if(!admins)
                AllowAdmins = false;
        }

        public CustomRequirePermissionsAttribute(DiscordPermission[]? botPermissions = null, DiscordPermission[]? userPermissions = null, bool admins = true)
        {
            if(botPermissions?.Length > 0)
                BotPermissions = new DiscordPermissions((IReadOnlyList<DiscordPermission>)botPermissions);
            if(userPermissions?.Length > 0)
                UserPermissions = new DiscordPermissions((IReadOnlyList<DiscordPermission>)userPermissions);

            if (!admins)
                AllowAdmins = false;
        }
    }
    internal class CustomRequirePermissionsCheck(IOptionsSnapshot<Settings> config) : IContextCheck<CustomRequirePermissionsAttribute>
    {
        Settings Settings => config.Value;

        public ValueTask<string?> ExecuteCheckAsync(CustomRequirePermissionsAttribute attribute, CommandContext context) => Validate(attribute, context);

        public ValueTask<string?> Validate(CustomRequirePermissionsAttribute attribute, CommandContext context)
        {
            RequireAdminCheck admin = new(config);

            if (admin.Validate(context).Result is string result)
                if (context is SlashCommandContext slashContext)
                {
                    if (!slashContext.Interaction.AppPermissions.HasAllPermissions(attribute.BotPermissions))
                    {
                        return ValueTask.FromResult<string?>("The bot does not have the needed permissions to execute this command.");
                    }
                    else if (
                        !slashContext.Member!.PermissionsIn(slashContext.Channel).HasAllPermissions(attribute.UserPermissions)
                        && !slashContext.Member!.PermissionsIn(slashContext.Channel).HasPermission(DiscordPermission.Administrator)
                    )
                    {
                        return ValueTask.FromResult<string?>("The executing user does not have the needed permissions to execute this command.");
                    }

                    return ValueTask.FromResult<string?>(null);
                }
                else if (!context.Guild!.CurrentMember.PermissionsIn(context.Channel).HasAllPermissions(attribute.BotPermissions))
                {
                    return ValueTask.FromResult<string?>("The bot does not have the needed permissions to execute this command.");
                }
                else if (
                    !context.Member!.PermissionsIn(context.Channel).HasAllPermissions(attribute.UserPermissions)
                    && !context.Member!.PermissionsIn(context.Channel).HasPermission(DiscordPermission.Administrator)
                )
                {
                    return ValueTask.FromResult<string?>("The executing user does not have the needed permissions to execute this command.");
                }
            return ValueTask.FromResult<string?>(null);
        }
    }
}
