using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using ElmerBot.Classes.Attributes;
using ElmerBot.Repositories;

namespace ElmerBot.Commands
{
    [Command("customize")]
    [BasicUserCheck, BasicGuildCheck]
    [CustomRequirePermissions(userPermissions: [DiscordPermission.ManageMessages])]
    internal class Customize_Commands(ICustomize_Repository repo)
    {
        [Command("pfp")]
        public async Task ProfilePic(SlashCommandContext ctx,
            [Parameter("URL")] 
            string url,
            [Parameter("Channel")] 
            DiscordChannel? chnl = null
        ) => await repo.SetProfilePicture(ctx, chnl?.Id ?? ctx.Channel.Id, url.Trim());

        [Command("username")]
        public async Task Username(SlashCommandContext ctx,
            [Parameter("Username")] 
            string user,
            [Parameter("Channel")] 
            DiscordChannel? chnl = null
        ) => await repo.SetUsername(ctx, chnl?.Id ?? ctx.Channel.Id, user.Trim());
    }
}
