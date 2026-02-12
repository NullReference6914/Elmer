using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.ContextChecks.ParameterChecks;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;


namespace ElmerBot.Commands
{
    internal class GlueMessageCmd
    {
        private bool isNotValid(SlashCommandContext ctx, ref List<GluedMessage> msgs)
        {
            if (ctx.Member.IsBot || ctx.Guild == null)
                return true;

            msgs = Program.msgs?.Where(m => m.Server_ID == ctx.Guild.Id).ToList();

            return false;
        }


        [Command("glue")]
        [RequirePermissions(DiscordPermission.ManageMessages)]
        public async Task glueMessage(SlashCommandContext ctx,  string test, 
            [Parameter("Message")] string content,
            [Parameter("Channel")] DiscordChannel chnl = null)
        {
            //await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            if (chnl == null)
                chnl = ctx.Channel;

            List<GluedMessage> msgs = new List<GluedMessage>();

            if (this.isNotValid(ctx, ref msgs))
                return;

            if (msgs.FirstOrDefault(m => m.Channel_ID == chnl.Id) is GluedMessage msg && msg.Channel_ID != 0)
            {
                msg.Message = content;
                Program.Save();
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("The message content has been updated."));
            }
            else if (String.IsNullOrEmpty(content))
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("Please provide a message to glue"));
            }
            else
            {
                Program.msgs.Add(new GluedMessage
                {
                    Server_ID = chnl.Guild.Id,
                    Channel_ID = chnl.Id,
                    Message = content
                });
                Program.Save();

                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("The message is now glued."));

                GlueMessageCmd.ProcessMessageCreated(ctx.Client, ctx.Guild, chnl);
            }
        }

        [Command("unglue")]
        [RequirePermissions(DiscordPermission.ManageMessages)]
        public async Task unglueMessage(SlashCommandContext ctx,
            [Parameter("Channel")] DiscordChannel chnl = null)
        {
            if (chnl == null)
                chnl = ctx.Channel;

            List<GluedMessage> msgs = new List<GluedMessage>();

            if (this.isNotValid(ctx, ref msgs))
                return;

            if (msgs.Where(m => m.Channel_ID == ctx.Channel.Id).Count() > 0)
            {
                GluedMessage m = msgs.FirstOrDefault(m => m.Channel_ID == ctx.Channel.Id);
                Program.msgs.Remove(m);
                try
                {
                    if (m.Message_ID > 0)
                        if (await ctx.Channel.GetMessageAsync(m.Message_ID) is DSharpPlus.Entities.DiscordMessage discordMsg)
                            if (discordMsg != null)
                                ctx.Channel.DeleteMessageAsync(discordMsg);
                }
                catch (Exception) { }
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("The message has been unglued."));
            }
            else
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("There is currently no message glued"));
            }
        }

        public static async void ProcessMessageCreated(DiscordClient c, DSharpPlus.EventArgs.MessageCreatedEventArgs e)
        {
            GlueMessageCmd.ProcessMessageCreated(c, e.Guild, e.Channel, e.Message);
        }
        public static async void ProcessMessageCreated(DiscordClient c, DiscordGuild guild, DiscordChannel channel, DiscordMessage? m = null)
        { 
            GluedMessage msg = Program.msgs?
                .FirstOrDefault(m => m.Server_ID == guild.Id && m.Channel_ID == channel.Id && m.isWatching == false);

          if (msg != null && Program.AllowedServers.Contains(guild.Id) && (m?.Id ?? 0) != msg.Message_ID)
            {
                msg.isWatching = true;
                await Task.Delay(5 * 1000);

                if (Program.msgs.Contains(msg))
                {
                    try
                    {
                        if (msg.Message_ID > 0)
                            if (await channel.GetMessageAsync(msg.Message_ID) is DSharpPlus.Entities.DiscordMessage discordMsg)
                                if (discordMsg != null)
                                    channel.DeleteMessageAsync(discordMsg);
                    }
                    catch (Exception) { }

                    DSharpPlus.Entities.DiscordWebhook hook = null;

                    try
                    {
                        List<DSharpPlus.Entities.DiscordWebhook> hooks = new();
                        if (channel.Parent != null)
                        {
                            hooks = (await channel.Parent.GetWebhooksAsync())?.ToList();
                        }
                        else
                        {
                            hooks = (await channel.GetWebhooksAsync())?.ToList();
                        }

                        if (hooks.Count > 0)
                            hook = hooks.FirstOrDefault(h => h.Name == "Elmers Hook" && h.User.Id == Program.client.CurrentUser.Id);
                    }
                    catch (Exception) { }

                    if (hook == null)
                        try
                        {
                            if (channel.Parent != null)
                            {
                                hook = await channel.Parent.CreateWebhookAsync("Elmers Hook");
                            }
                            else
                            {
                                hook = await channel.CreateWebhookAsync("Elmers Hook");
                            }
                        }
                        catch (Exception e) { }

                    if (hook != null)
                    {
                        DSharpPlus.Entities.DiscordWebhookBuilder builder = new DSharpPlus.Entities.DiscordWebhookBuilder
                        {
                            Content = msg.Message,
                            AvatarUrl = msg.Avatar_Url ?? guild.IconUrl,
                            Username = msg.Username ?? guild.Name,
                            ThreadId = (channel.Parent != null) ? msg.Channel_ID : null
                        };
                        DSharpPlus.Entities.DiscordMessage hookMsg = await hook.ExecuteAsync(builder);
                        msg.Message_ID = hookMsg.Id;
                        
                        Console.WriteLine($"[{DateTime.Now}][Hook Submitted] Server: {channel.Guild.Name} ({channel.Guild.Id}) - #{channel.Name} ({channel.Id})");
                        Program.Save();
                    }
                    msg.isWatching = false;
                }
            }
        }
    }
}
