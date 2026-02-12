using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;

namespace ElmerBot.Commands
{
    [Command("customize")]
    [RequirePermissions(DiscordPermission.ManageMessages)]
    internal class CustomizeMessage
    {
        private bool isNotValid(SlashCommandContext ctx, ref List<GluedMessage> msgs)
        {
            if (ctx.Member.IsBot || ctx.Guild == null)
                return true;

            msgs = Program.msgs?.Where(m => m.Server_ID == ctx.Guild.Id).ToList();

            return false;
        }


        [Command("pfp")]
        public async Task glueMessage(SlashCommandContext ctx, 
            [Parameter("URL")] string url,
            [Parameter("Channel")] DiscordChannel chnl = null)
        {
            if (chnl == null)
                chnl = ctx.Channel;

            List<GluedMessage> msgs = new List<GluedMessage>();

            if (this.isNotValid(ctx, ref msgs))
                return;

            GluedMessage msg = msgs.FirstOrDefault(m => m.Channel_ID == chnl.Id);

            if (msg == null)
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("There is currently no glued message for the provided channel."));
            }
            else
            {
                if (url.Trim() == "")
                    url = null;

                msg.Avatar_Url = url;
                Program.Save();

                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("The pfp has been set."));
                GlueMessageCmd.ProcessMessageCreated(ctx.Client, ctx.Guild, chnl);
            }
        }

        [Command("username")]
        public async Task unglueMessage(SlashCommandContext ctx,
            [Parameter("Username")]string user,
            [Parameter("Channel")] DiscordChannel chnl = null)
        {
            if (chnl == null)
                chnl = ctx.Channel;

            List<GluedMessage> msgs = new List<GluedMessage>();

            if (this.isNotValid(ctx, ref msgs))
                return;

            GluedMessage msg = msgs.FirstOrDefault(m => m.Channel_ID == chnl.Id);

            if (msg == null)
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("There is currently no glued message for the provided channel."));
            }
            else
            {
                if (user.Trim() == "")
                    user = null;

                msg.Username = user;
                Program.Save();

                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("The username has been set."));
                GlueMessageCmd.ProcessMessageCreated(ctx.Client, ctx.Guild, chnl);
            }
        }
    }
}
