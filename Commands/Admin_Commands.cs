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
        [Command("hi")]
        [RequireAdmin]
        public async Task Hi(SlashCommandContext ctx) => await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"👋 Hi, {ctx.User.Username}!").AsEphemeral());

        [Command("members")]
        [CustomRequirePermissions(userPermissions: [DiscordPermission.ManageMessages])]
        public async Task members(SlashCommandContext ctx,
            [Parameter("Role")] 
            DiscordRole primaryRole,
            [Parameter("Output_Type")]
            [SlashChoiceProvider<MemberOutputTypeProvider>]
            int type
        ) => await repo.GetMembers(ctx, primaryRole, (MemberOutputType)type);

        [Command("server")]
        [RequireAdmin]
        internal class Server_Commands(IAdmin_Repository repo)
        {
            [Command("leave")]
            public async Task leave_server(SlashCommandContext ctx,
            [Parameter("ServerId"), Description("ID of server")]
                [SlashAutoCompleteProvider<ServersProvider>]
                string serverID) => await repo.Server_Leave(ctx, serverID);

            [Command("allow")]
            public async Task allow_server(SlashCommandContext ctx,
                [Parameter("ServerId"), Description("ID of server")]
                [SlashAutoCompleteProvider<ServersProvider>]
                string serverID) => await repo.Server_Allow(ctx, serverID);

            [Command("disallow")]
            public async Task disallow_server(SlashCommandContext ctx,
                [Parameter("ServerId"), Description("ID of server")]
                [SlashAutoCompleteProvider<ServersProvider>]
                string serverID) => await repo.Server_Disallow(ctx, serverID);
        }
    }
}
