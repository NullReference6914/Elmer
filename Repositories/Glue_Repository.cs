using DSharpPlus;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using ElmerBot.Classes;
using ElmerBot.Models;
using Microsoft.Extensions.Options;

namespace ElmerBot.Repositories
{
    internal interface IGlue_Repository
    {
        Task GlueMessage(SlashCommandContext ctx, string message, ulong channelId);
        Task UnglueMessage(SlashCommandContext ctx, ulong channelId);
        Task ViewStickys(SlashCommandContext ctx);
        Task RemoveServer(ulong serverID);

        StickyVault GetMessages();

        Task Process_MessageCreated(DiscordClient c, DSharpPlus.EventArgs.MessageCreatedEventArgs e);
        Task Process_GuildAvailable(DiscordClient c, DiscordGuild guild);
        Task Process_Sticky(DiscordClient c, DiscordGuild guild, DiscordChannel channel, DiscordMessage? m = null);
        Task Process_GuildDownloadCompleted(DiscordClient c, GuildDownloadCompletedEventArgs e);
        Task Process_ChannelDeleted(DiscordClient c, ChannelDeletedEventArgs e);
    }
    internal class Glue_Repository(IOptionsSnapshot<Settings> _config, ILogging_Repository logger) : IGlue_Repository
    {
        Settings Settings => _config.Value;

        readonly StickyVault msgs = new(AppDomain.CurrentDomain.BaseDirectory + "/Settings/", logger);
        readonly DiscordPermission[] requiredPerms = [DiscordPermission.ViewChannel, DiscordPermission.ManageWebhooks, DiscordPermission.ManageMessages];

        public StickyVault GetMessages() => msgs;

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
                else if (!chnl.PermissionsFor(await ctx.Guild!.GetBotMember(ctx.Client))
                    .HasAllPermissions(new DiscordPermissions(requiredPerms))
                )
                {
                    DiscordMember botMember = await ctx.Guild!.GetBotMember(ctx.Client);
                    List<string> missingPerms = [];

                    foreach(var p in requiredPerms)
                        if (!chnl.PermissionsFor(botMember).HasPermission(p))
                            missingPerms.Add(p.ToStringFast());

                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"Unable to use the channel {chnl.Mention}. Missing permissions: {String.Join(" ,", missingPerms)}").AsEphemeral());

                }
                else if (await msgs.TryGetValue($"{ctx.Guild!.Id}_{channelId}") is (true, _))
                {
                    if (await msgs.TryUpdate($"{ctx.Guild!.Id}_{channelId}", (ref m) => { m.Message = message; }))
                    {
                        await Task.WhenAll(
                            ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("The message content has been updated.").AsEphemeral()).AsTask(),
                            Process_Sticky(ctx.Client, ctx.Guild!, chnl)
                        );
                    }
                    else
                    {
                        await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("An error occured during the saving of the message. Please try again.").AsEphemeral());
                    }

                }
                else
                {
                    await msgs.TryAdd($"{ctx.Guild!.Id}_{channelId}", new()
                    {
                        Server_ID = ctx.Guild!.Id,
                        Channel_ID = channelId,
                        Message = message
                    });

                    await Task.WhenAll(
                        ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("The message is now glued.").AsEphemeral()).AsTask(),
                        Process_Sticky(ctx.Client, ctx.Guild, chnl)
                    );
                }
            }
            catch (Exception ex)
            {
                await Task.WhenAll(
                    logger.LogError($"Error during glue command", ctx, ex),
                    ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("An error occurred while trying to glue the message.").AsEphemeral()).AsTask()
                );
            }
        }

        public async Task UnglueMessage(SlashCommandContext ctx, ulong channelId)
        {
            try
            {
                if (await msgs.Remove($"{ctx.Guild!.Id}_{channelId}") is (true, GluedMessage msg))
                {
                    try
                    {
                        if (msg.Message_ID > 0)
                            if (await ctx.Channel.GetMessageAsync(msg.Message_ID.Value) is DiscordMessage discordMsg)
                                if (discordMsg is not null)
                                    await ctx.Channel.DeleteMessageAsync(discordMsg);
                    }
                    catch { }

                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("The message has been unglued.").AsEphemeral());
                }
                else
                {
                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("There is currently no message glued").AsEphemeral());
                }
            }
            catch (Exception ex)
            {
                await Task.WhenAll(
                    logger.LogError($"Error during unglue command", ctx, ex),
                    ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("An error occurred while trying to unglue the message.").AsEphemeral()).AsTask()
                );
            }
        }

        public async Task ViewStickys(SlashCommandContext ctx)
        {
            List<string> keys = [.. msgs.Keys.Where(k => k.StartsWith(ctx.Guild!.Id.ToString()))];

            if(keys.Count == 0)
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("There currently are no stickys."));
            }
            else
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("Generating list. Please wait..."));
                List<string> stickyMsgs = [];
                foreach (var key in keys)
                    if(await msgs.TryGetValue(key) is (true, GluedMessage msg))
                    {
                        string sticky = $@"**Channel**: <#{msg.Channel_ID}> (ID: {msg.Channel_ID}
**Message**: {msg.Message}";
                        if (msg.Username is not null) sticky += $"\r\n**Username**: {msg.Username}";
                        if (msg.Avatar_Url is not null) sticky += $"\r\n**Profile Picture**: [{msg.Avatar_Url}]({msg.Avatar_Url})";

                        stickyMsgs.Add(sticky);
                    }

                _ = ctx.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder() { Content = "Sticky information will display below." });

                do
                {
                    string mesage = "";
                    do
                    {
                        string newMsg = stickyMsgs.First();
                        mesage += ((mesage.Length > 0) ? "\r\n" : "") + newMsg;
                        stickyMsgs.Remove(newMsg);
                    } while (stickyMsgs.Count > 0 && (mesage + "\r\n" + stickyMsgs.FirstOrDefault()).Length < 2000);

                    await ctx.Channel.SendMessageAsync(mesage);

                    if(stickyMsgs.Count > 0)
                        await Task.Delay(2000);
                } while (stickyMsgs.Count > 0);
            }
        }

        public async Task RemoveServer(ulong serverID)
        {
            List<string> invalidServerKeys = [.. msgs.Keys.Where(k => k.StartsWith(serverID.ToString()))];

            if (invalidServerKeys.Count != 0)
            {
                List<Task> tasks = [];
                foreach (var key in invalidServerKeys)
                    tasks.Add(msgs.Remove(key));

                await Task.WhenAll(tasks);
            }
        }

        #region Processing Methods

        public async Task Process_GuildDownloadCompleted(DiscordClient c, GuildDownloadCompletedEventArgs e)
        {
            List<string> invalidServerKeys = [.. msgs.Keys];

            if (invalidServerKeys.Count != 0)
                await foreach (var g in c.GetGuildsAsync())
                    invalidServerKeys = [.. invalidServerKeys.Where(k => !k.StartsWith(g.Id.ToString()))];

            if (invalidServerKeys.Count != 0)
            {
                List<string> servers = [..invalidServerKeys.Select(k => k.Split("_")[0]).Distinct()];
                _ = logger.LogBasic("Auto Clean", $"Automatically cleaning invalid servers ({servers.Count}), stickys ({invalidServerKeys.Count}).\r\n- " + String.Join("\r\n- ", servers));

                List<Task> tasks = [];
                foreach (var key in invalidServerKeys)
                    tasks.Add(msgs.Remove(key));

                await Task.WhenAll(tasks);
            }
        }

        public async Task Process_GuildAvailable(DiscordClient c, DiscordGuild guild)
        {
            _ = Task.Run(async () =>
            {
                _ = logger.LogBasic("Guild Available", $"Server: {guild.Name} ({guild.Id})");

                List<Task> tasks = [];

                foreach (var key in msgs.Keys.Where(k => k.StartsWith($"{guild.Id}_")))
                    if (await msgs.TryGetValue(key) is (bool succes, GluedMessage msg) && succes)
                    {
                        DiscordChannel? chnl = null;
                        try { chnl = await guild.GetChannelAsync(msg.Channel_ID); }
                        catch
                        {
                            if (msg.Channel_Errors < 10)
                            {
                                await msgs.TryUpdate(key, (ref m) => { m.Channel_Errors = msg.Channel_Errors + 1; });
                                _ = logger.LogError($"Failed to access channel for sticky.\r\n**Server**: {guild.Name} ({guild.Id})\r\n**Channel ID**: {msg.Channel_ID}");
                            }
                            else
                            {
                                await msgs.Remove($"{msg.Server_ID}_{msg.Channel_ID}");
                                _ = logger.LogError($"Failed to access channel for sticky after multiple attempts. Removing sticky.\r\n**Server**: {guild.Name} ({guild.Id})\r\n**Channel ID**: {msg.Channel_ID}");
                            }
                        }

                        if(chnl is not null)
                        {
                            if(!chnl.PermissionsFor(await guild.GetBotMember(c)).HasAllPermissions(new DiscordPermissions(requiredPerms)))
                            {
                                DiscordMember botMember = await guild!.GetBotMember(c);
                                List<string> missingPerms = [];

                                foreach (var p in requiredPerms)
                                    if (!chnl.PermissionsFor(botMember).HasPermission(p))
                                        missingPerms.Add(p.ToStringFast());

                                await msgs.TryUpdate(key, (ref m) => { m.Channel_Errors = msg.Channel_Errors + 1; });
                                _ = logger.LogError($"Missing permissions for sticky.\r\n**Server**: {guild.Name} ({guild.Id})\r\n**Channel**: \\#{chnl.Name} ({chnl.Id})\r\nMissing Permissions: {String.Join(", ", missingPerms)}");

                                chnl = null;
                            }
                        }

                        if (chnl is not null)
                        {
                            DiscordMessage? lastMsg = null;
                            try { lastMsg = await chnl.GetMessagesAsync(1).FirstAsync(); }
                            catch { }

                            tasks.Add(Process_Sticky(c, guild, chnl, lastMsg));
                        }
                    }

                await Task.WhenAll(tasks);
            });
        }

        public async Task Process_ChannelDeleted(DiscordClient c, ChannelDeletedEventArgs e)
        {
            if (await msgs.Remove($"{e.Guild!.Id}_{e.Channel.Id}") is (true, _))
                await logger.LogBasic("Auto Clean", $"Sticky automatically deleted as channel was deleted.\r\n**Server**: {e.Guild.Name} ({e.Guild.Id})\r\n**Channel**: \\#{e.Channel.Name} ({e.Channel.Id})");
        }

        public async Task Process_MessageCreated(DiscordClient c, MessageCreatedEventArgs e) => await Process_Sticky(c, e.Guild, e.Channel, e.Message);


        public async Task Process_Sticky(DiscordClient c, DiscordGuild guild, DiscordChannel channel, DiscordMessage? m = null)
        {
            try
            {
                string msgKey = $"{guild.Id}_{channel.Id}";
                if (Settings.EnabledServers.Contains(guild.Id) || Settings.Admin.ServerID == guild.Id)
                    if (await msgs.TryGetValue(msgKey) is (bool success, GluedMessage message) && success)
                        if ((m is null || m.Id != message.Message_ID) && !message.isWatching)
                        {
                            _ = logger.LogBasic("Processing", $"{guild.Name} ({guild.Id}) -> \\#{channel.Name} ({channel.Id})");

                            await msgs.TryUpdate(msgKey, (ref m) => { m.isWatching = true; });
                            await Task.Delay(5 * 1000);

                            if (await msgs.TryGetValue(msgKey) is (bool getSuccess, GluedMessage msg) && getSuccess)
                                try
                                {
                                    try
                                    {
                                        if (msg.Message_ID > 0)
                                            if (await channel.GetMessageAsync(msg.Message_ID.Value) is DiscordMessage discordMsg)
                                                if (discordMsg is not null)
                                                    await channel.DeleteMessageAsync(discordMsg);
                                    }
                                    catch { }

                                    DiscordWebhook? hook = null;
                                    bool childChannel = channel.Parent is not null && channel.Parent?.Type != DiscordChannelType.Category;
                                    try
                                    {
                                        List<DiscordWebhook>? hooks = [];

                                        if (childChannel)
                                        {
                                            var list = await channel.Parent!.GetWebhooksAsync();
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
                                            hook = await ((childChannel)
                                                ? channel.Parent!.CreateWebhookAsync("Elmers Hook")
                                                : channel.CreateWebhookAsync("Elmers Hook"));
                                        }
                                        catch
                                        {
                                            if (msg.Webhook_Errors < 10)
                                            {
                                                await msgs.TryUpdate(msgKey, (ref m) => { m.Webhook_Errors = msg.Webhook_Errors + 1; });
                                                _ = logger.LogError($"Failed to generate a webhook for sticky.\r\n**Server**: {guild.Name} ({guild.Id})\r\n**Channel:** \\#{channel.Name} ({channel.Id})");
                                            }
                                            else
                                            {
                                                await msgs.Remove($"{msg.Server_ID}_{msg.Channel_ID}");
                                                _ = logger.LogError($"Failed to generate a webhook for sticky after multiple attempts. Removing sticky.\r\n**Server**: {guild.Name} ({guild.Id})\r\n**Channel**: \\#{channel.Name} ({channel.Id})");
                                            }
                                        }

                                    if (hook is not null)
                                        try
                                        {
                                            DiscordWebhookBuilder builder = new()
                                            {
                                                Content = msg.Message,
                                                AvatarUrl = msg.Avatar_Url ?? guild.IconUrl,
                                                Username = msg.Username ?? guild.Name,
                                                ThreadId = (childChannel) ? msg.Channel_ID : null
                                            };
                                            DiscordMessage hookMsg = await hook.ExecuteAsync(builder);

                                            await msgs.TryUpdate(msgKey, (ref m) =>
                                                {
                                                    m.Message_ID = hookMsg.Id;
                                                    m.Channel_Errors = 0;
                                                    m.Webhook_Errors = 0; 
                                                }
                                            );

                                            _ = logger.LogBasic("Posted", $"{channel.Guild.Name} ({channel.Guild.Id}) -> \\#{channel.Name} ({channel.Id})");
                                        }
                                        catch (Exception ex)
                                        {
                                            _ = logger.LogError($"Error during webhook submission.\r\n**Server**: {guild.Name} ({guild.Id})\r\n**Channel:** \\#{channel.Name} ({channel.Id})", guild, ex);
                                        }

                                     await msgs.TryUpdate(msgKey, (ref m) => { m.isWatching = false; });
                                }
                                catch (Exception ex)
                                {
                                    _ = logger.LogError($"Error during processing of sticky.\r\n**Server**: {guild.Name} ({guild.Id})\r\n**Channel**: \\#{channel.Name} ({channel.Id})", guild, ex);
                                }
                        }
            }
            catch (Exception ex)
            {
                await logger.LogError($"Error during processing of message created event\r\n**Server**: {guild.Name} ({guild.Id})\r\n**Channel**: \\#{channel.Name} ({channel.Id})", Exception: ex);
            }
        }

        #endregion
    }
}
