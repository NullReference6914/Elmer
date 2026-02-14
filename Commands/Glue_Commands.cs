using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using ElmerBot.Classes.Attributes;
using ElmerBot.Repositories;
using System.ComponentModel;

namespace ElmerBot.Commands
{
    [BasicUserCheck, BasicGuildCheck]
    [CustomRequirePermissions(userPermissions: [DiscordPermission.ManageMessages])]
    internal class Glue_Commands(IGlue_Repository repo)
    {
        [Command("glue"), Description("Create a sticky message")]
        public async Task GlueMessage(SlashCommandContext ctx,
            [Parameter("Message")] 
            string content,
            [Parameter("Channel")] 
            DiscordChannel? chnl = null
        )
        => await repo.GlueMessage(ctx, content, chnl?.Id ?? ctx.Channel.Id);


        [Command("unglue"), Description("remove a sticky message")]
        public async Task unglueMessage(SlashCommandContext ctx,
            [Parameter("Channel")] 
            DiscordChannel? chnl = null
        ) => await repo.UnglueMessage(ctx, chnl?.Id ?? ctx.Channel.Id);
    }
}
