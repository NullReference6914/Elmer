using DSharpPlus;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using ElmerBot.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ElmerBot.Repositories
{
    internal interface IGlue_Repository
    {
        Task GlueMessage(SlashCommandContext ctx, string message, ulong channelId);
        Task UnglueMessage(SlashCommandContext ctx, ulong channelId);

        Task ProcessMessageCreated(DiscordClient c, DSharpPlus.EventArgs.MessageCreatedEventArgs e);
        Task ProcessMessageCreated(DiscordClient c, DiscordGuild guild);
    }
    internal class Glue_Repository(IOptionsSnapshot<Settings> _config, ILogging_Repository logger) : IGlue_Repository
    {
        Settings settings => _config.Value;

        static string settingsFolder = AppDomain.CurrentDomain.BaseDirectory + "/Settings/";
        internal List<GluedMessage> msgs => _msgs ??= Load();
        List<GluedMessage> _msgs;

        #region JSON File Methods

        internal List<GluedMessage> Load()
        {
            if (Directory.Exists(settingsFolder))
                if (File.Exists(settingsFolder + "list.json"))
                    if (JsonSerializer.Deserialize<List<GluedMessage>>(File.ReadAllText(settingsFolder + "list.json")) is List<GluedMessage> m)
                        return m;
            return [];
        }

        internal async Task<bool> Save()
        {
            try
            {
                if (!Directory.Exists(settingsFolder))
                    Directory.CreateDirectory(settingsFolder);
                await File.WriteAllTextAsync(settingsFolder + "list.json", JsonSerializer.Serialize(msgs));
                return true;
            }
            catch (Exception ex)
            {
                await logger.LogError($"Error saving glued messages", Exception: ex);
                return false;
            }
        }

        #endregion

        public async Task GlueMessage(SlashCommandContext ctx, string message, ulong channelId)
        {
            try
            {
                DiscordChannel? chnl = null;
                try { chnl = await ctx.Guild!.GetChannelAsync(channelId); }
                catch { }

                if (String.IsNullOrEmpty(message))
                {
                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("Please provide a message to glue").AsEphemeral());
                }
                else if (chnl is null)
                {
                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("The provided channel is invalid.").AsEphemeral());
                }
                else if (msgs
                    .FirstOrDefault(m =>
                        m.Channel_ID == channelId
                        && m.Server_ID == ctx.Guild!.Id
                    ) is GluedMessage msg && msg.Channel_ID != 0)
                {
                    msg.Message = message;

                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent((await Save()) ? "The message content has been updated." : "There was an error during the updating of the message content.").AsEphemeral());
                }
                else
                {
                    msgs.Add(new()
                    {
                        Server_ID = ctx.Guild!.Id,
                        Channel_ID = channelId,
                        Message = message
                    });

                    await Save();

                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent((await Save()) ? "The message is now glued." : "There was an error saving the new glued message.").AsEphemeral());

                    await ProcessMessageCreated(ctx.Client, ctx.Guild, chnl);
                }
            }
            catch (Exception ex)
            {
                await logger.LogError($"Error during glue command", ctx, ex);
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("An error occurred while trying to glue the message.").AsEphemeral());
            }
        }

        public async Task UnglueMessage(SlashCommandContext ctx, ulong channelId)
        {
            try
            {
                if (msgs
                    .FirstOrDefault(m =>
                        m.Channel_ID == channelId
                        && m.Server_ID == ctx.Guild!.Id
                    ) is GluedMessage msg && msg.Channel_ID != 0)
                {
                    msgs.Remove(msg);

                    await Save();

                    try
                    {
                        if (msg.Message_ID > 0)
                            if (await ctx.Channel.GetMessageAsync(msg.Message_ID) is DiscordMessage discordMsg)
                                if (discordMsg is not null)
                                    await ctx.Channel.DeleteMessageAsync(discordMsg);
                    }
                    catch { }

                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent((await Save()) ? "The message has been unglued." : "There was an error during the removal of the glued message.").AsEphemeral());
                }
                else
                {
                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("There is currently no message glued").AsEphemeral());
                }
            }
            catch (Exception ex)
            {
                await logger.LogError($"Error during unglue command", ctx, ex);
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("An error occurred while trying to unglue the message.").AsEphemeral());
            }
        }

        #region Processing Methods

        public async Task ProcessMessageCreated(DiscordClient c, DSharpPlus.EventArgs.MessageCreatedEventArgs e)
        {
            await ProcessMessageCreated(c, e.Guild, e.Channel, e.Message);
        }

        public async Task ProcessMessageCreated(DiscordClient c, DiscordGuild guild) 
        {
            List<GluedMessage> serverMsgs = msgs
                .Where(m => m.Server_ID == guild.Id && m.isWatching == false)
                .ToList();

            foreach (GluedMessage msg in serverMsgs)
            {
                DiscordChannel? chnl = null;
                try { chnl = await guild.GetChannelAsync(msg.Channel_ID); }
                catch { }

                if (chnl is not null)
                    await ProcessMessageCreated(c, guild, chnl);
            }
        }

        public async Task ProcessMessageCreated(DiscordClient c, DiscordGuild guild, DiscordChannel channel, DiscordMessage? m = null)
        {
            try
            {
                GluedMessage? msg = msgs
                    .FirstOrDefault(m => m.Server_ID == guild.Id && m.Channel_ID == channel.Id && m.isWatching == false);
                if(msg is not null)
                    if (
                        (settings.EnabledServers.Contains(guild.Id) || settings.Admin.ServerID == guild.Id)
                        && m?.Id != msg?.Message_ID

                    )
                    {
                        msg!.isWatching = true;
                        await Task.Delay(5 * 1000);

                        if (msgs.Contains(msg))
                        {
                            try
                            {
                                if (msg.Message_ID > 0)
                                    if (await channel.GetMessageAsync(msg.Message_ID) is DiscordMessage discordMsg)
                                        if (discordMsg is not null)
                                            await channel.DeleteMessageAsync(discordMsg);
                            }
                            catch (Exception) { }

                            DiscordWebhook? hook = null;

                            try
                            {
                                List<DiscordWebhook>? hooks = [];
                                if (channel.Parent is not null)
                                {
                                    var list = await channel.Parent.GetWebhooksAsync();
                                    hooks = list?.ToList();
                                }
                                else
                                {
                                    var list = await channel.GetWebhooksAsync();
                                    hooks = list?.ToList();
                                }

                                if (hooks?.Count > 0)
                                    hook = hooks.FirstOrDefault(h => h.Name == "Elmers Hook" && h.User.Id == c.CurrentUser.Id);
                            }
                            catch { }

                            if (hook is null)
                                try
                                {
                                    hook = await ((channel.Parent is not null)
                                        ? channel.Parent.CreateWebhookAsync("Elmers Hook")
                                        : channel.CreateWebhookAsync("Elmers Hook"));
                                }
                                catch { }

                            if (hook is not null)
                            {
                                DiscordWebhookBuilder builder = new()
                                {
                                    Content = msg.Message,
                                    AvatarUrl = msg.Avatar_Url ?? guild.IconUrl,
                                    Username = msg.Username ?? guild.Name,
                                    ThreadId = (channel.Parent is not null) ? msg.Channel_ID : null
                                };
                                DiscordMessage hookMsg = await hook.ExecuteAsync(builder);
                                msg.Message_ID = hookMsg.Id;

                                logger.LogBasic("Hook Submitted", $"Server: {channel.Guild.Name} ({channel.Guild.Id}) - #{channel.Name} ({channel.Id})").Wait();
                            }
                            msg.isWatching = false;
                            await Save();
                        }
                    }
            }
            catch (Exception ex)
            {
                await logger.LogError($"Error during processing of message created event", Exception: ex);
            }
        }

        #endregion
    }
}
