using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using ElmerBot.Classes.Attributes;
using ElmerBot.Classes.AutoCompleteProviders;
using ElmerBot.Classes.ChoiceProvider;
using ElmerBot.Enums;
using ElmerBot.Repositories;
using System.ComponentModel;

namespace ElmerBot.Commands
{
    [BasicUserCheck, BasicGuildCheck]
    internal class Admin_Commands(IAdmin_Repository repo)
    {
        [Command("hi"), Description("General bot responsiveness test command")]
        [RequireAdmin]
        public async Task Hi(SlashCommandContext ctx) => await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"👋 Hi, {ctx.User.Username}!").AsEphemeral());

        [Command("members"), Description("Build a list of members from a role")]
        [CustomRequirePermissions(userPermissions: [DiscordPermission.ManageMessages])]
        public async Task members(SlashCommandContext ctx,
            [Parameter("Role")] 
            DiscordRole primaryRole,
            [Parameter("Output_Type")]
            [SlashChoiceProvider<MemberOutputTypeProvider>]
            int type
        ) => await repo.GetMembers(ctx, primaryRole, (MemberOutputType)type);

        [Command("leave"), Description("Force the bot to leave a server")]
        [RequireAdmin]
        public async Task leave_server(SlashCommandContext ctx,
        [Parameter("ServerId"), Description("ID of server")]
            [SlashAutoCompleteProvider<ServersProvider>]
            string serverID) => await repo.Server_Leave(ctx, serverID);

        [Command("allow"), Description("Allow a server to use the bot")]
        [RequireAdmin]
        public async Task allow_server(SlashCommandContext ctx,
            [Parameter("ServerId"), Description("ID of server")]
            [SlashAutoCompleteProvider<ServersProvider>]
            string serverID) => await repo.Server_Allow(ctx, serverID);

        [Command("disallow"), Description("Revoke a server from using the bot")]
        [RequireAdmin]
        public async Task disallow_server(SlashCommandContext ctx,
            [Parameter("ServerId"), Description("ID of server")]
            [SlashAutoCompleteProvider<ServersProvider>]
            string serverID) => await repo.Server_Disallow(ctx, serverID);

        [Command("view"), Description("View all servers the bot is in")]
        [RequireAdmin]
        public async Task view_servers(SlashCommandContext ctx) => await repo.Server_View(ctx);

    }
}
