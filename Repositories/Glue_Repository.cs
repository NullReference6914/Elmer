using DSharpPlus;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using ElmerBot.Models;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;

namespace ElmerBot.Repositories
{
    internal interface IGlue_Repository
    {
        Task GlueMessage(SlashCommandContext ctx, string message, ulong channelId);
        Task UnglueMessage(SlashCommandContext ctx, ulong channelId);
        Task ViewStickys(SlashCommandContext ctx);
        Task RemoveServer(ulong serverID);

        void Save();
        ConcurrentDictionary<string, GluedMessage> GetMessages();

        Task Process_MessageCreated(DiscordClient c, DSharpPlus.EventArgs.MessageCreatedEventArgs e);
        Task Process_GuildAvailable(DiscordClient c, DiscordGuild guild);
        Task Process_Sticky(DiscordClient c, DiscordGuild guild, DiscordChannel channel, DiscordMessage? m = null);
        Task Process_GuildDownloadCompleted(DiscordClient c, GuildDownloadCompletedEventArgs e);
        Task Process_ChannelDeleted(DiscordClient c, ChannelDeletedEventArgs e);
    }
    internal class Glue_Repository(IOptionsSnapshot<Settings> _config, ILogging_Repository logger) : IGlue_Repository
    {
        Settings settings => _config.Value;

        string settingsFolder = AppDomain.CurrentDomain.BaseDirectory + "/Settings/";
        internal ConcurrentDictionary<string, GluedMessage> msgs => _msgs ??= Load();
        ConcurrentDictionary<string, GluedMessage> _msgs;
        DateTime? lastSavedTime;
        System.Timers.Timer saveTimer;
        bool needSave = false;

        #region JSON File Methods

        internal ConcurrentDictionary<string, GluedMessage> Load()
        {
            if (saveTimer is null)
            {
                saveTimer = new System.Timers.Timer(5000);
                saveTimer.AutoReset = true;
                saveTimer.Elapsed += async (s, e) =>
                {
                    if (needSave)
                        try
                        {
                            if (!Directory.Exists(settingsFolder))
                                Directory.CreateDirectory(settingsFolder);
                            await File.WriteAllTextAsync(settingsFolder + "list.json", JsonSerializer.Serialize(msgs.Select(m => m.Value).ToList()));

                            needSave = false;

                            if ((DateTime.Now - (lastSavedTime ?? DateTime.Now)).TotalMinutes > 2)
                            {
                                _msgs = null!;
                                lastSavedTime = null;
                                await logger.LogBasic("Memory Release", "Released messages from memory, as it has been over 2 minutes since the last save.");
                            }
                            else
                            {
                                lastSavedTime = DateTime.Now;
                            }
                        }
                        catch (IOException) { }
                        catch (Exception ex)
                        {
                            await logger.LogError($"Error saving stickys", Exception: ex);
                        }
                };
                saveTimer.Start();
            }

            if (Directory.Exists(settingsFolder))
                if (File.Exists(settingsFolder + "list.json"))
                    if (JsonSerializer.Deserialize<List<GluedMessage>>(File.ReadAllText(settingsFolder + "list.json")) is List<GluedMessage> m)
                    {
                        if(lastSavedTime is null)
                            m.ForEach(m => m.isWatching = false);
                        return new ConcurrentDictionary<string, GluedMessage>(m.ToDictionary(k => $"{k.Server_ID}_{k.Channel_ID}", v => v));
                    }

            return [];
        }

        public void Save() => needSave = true;


        #endregion

        public ConcurrentDictionary<string, GluedMessage> GetMessages() => msgs;

        public async Task GlueMessage(SlashCommandContext ctx, string message, ulong channelId)
        {
            try
            {
                DiscordChannel? chnl = null;
                try { chnl = await ctx.Guild!.GetChannelAsync(channelId); }
                catch { }

                DiscordPermission[] perms = [DiscordPermission.ViewChannel, DiscordPermission.ManageWebhooks, DiscordPermission.ManageMessages];

                if (String.IsNullOrEmpty(message))
                {
                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("Please provide a message to glue").AsEphemeral());
                }
                else if (chnl is null)
                {
                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("The provided channel is invalid.").AsEphemeral());
                }
                else if (!chnl.PermissionsFor(await ctx.Guild!.GetBotMember(ctx.Client))
                    .HasAllPermissions(new DiscordPermissions(perms))
                )
                {
                    DiscordMember botMember = await ctx.Guild!.GetBotMember(ctx.Client);
                    List<string> missingPerms = [];

                    foreach(var p in perms)
                        if (!chnl.PermissionsFor(botMember).HasPermission(p))
                            missingPerms.Add(p.ToStringFast());

                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"Unable to use the channel {chnl.Mention}. Missing permissions: {String.Join(" ,", missingPerms)}").AsEphemeral());

                }
                else if (msgs.TryGetValue($"{ctx.Guild!.Id}_{channelId}", out var msg))
                {
                    if (msgs.TryUpdate($"{ctx.Guild!.Id}_{channelId}", new GluedMessage(msg) { Message = message }, msg))
                    {
                        needSave = true;
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
                    msgs.TryAdd($"{ctx.Guild!.Id}_{channelId}", new()
                    {
                        Server_ID = ctx.Guild!.Id,
                        Channel_ID = channelId,
                        Message = message
                    });

                    needSave = true;

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
                if (msgs.Remove($"{ctx.Guild!.Id}_{channelId}", out var msg))
                {
                    needSave = true;

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
                    if(msgs.TryGetValue(key, out var msg))
                    {
                        string sticky = $@"**Channel**: <#{msg.Channel_ID}> (ID: {msg.Channel_ID}
**Message**: {msg.Message}";
                        if (msg.Username is not null) sticky += $"\r\n**Username**: {msg.Username}";
                        if (msg.Avatar_Url is not null) sticky += $"\r\n**Profile Picture**: {msg.Avatar_Url}";

                        stickyMsgs.Add(sticky);
                    }

                _ = ctx.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder() { Content = "Sticky information will display below." });

                string mesage = "";

                do
                {
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
            List<string> invalidServerKeys = [.. msgs.Keys];

            if (invalidServerKeys.Count != 0)
                invalidServerKeys = [.. invalidServerKeys.Where(k => !k.StartsWith(serverID.ToString()))];

            if (invalidServerKeys.Count != 0)
            {
                foreach (var key in invalidServerKeys)
                    msgs.Remove(key, out _);

                needSave = true;
            }
        }

        public async Task ViewStickys(SlashCommandContext ctx)
        {
            List<string> keys = [.. msgs.Keys.Where(k => k.StartsWith(ctx.Guild!.Id.ToString()))];

            if(keys.Count == 0)
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("There currently are no saved stickys."));
            }
            else
            {
                List<string> stickyMsgs = [];
                foreach (var key in keys)
                    if(msgs.TryGetValue(key, out var msg))
                    {
                        string sticky = $@"**Channel**: <#{msg.Channel_ID}> (ID: {msg.Channel_ID}
**Message**: {msg.Message}";
                        if (msg.Username is not null) sticky += $"\r\n**Username**: {msg.Username}";
                        if (msg.Avatar_Url is not null) sticky += $"\r\n**Profile Picture**: {msg.Avatar_Url}";

                        stickyMsgs.Add(sticky);
                    }
            }
        }

        public async Task RemoveServer(ulong serverID)
        {
            List<string> invalidServerKeys = [.. msgs.Keys];

            if (invalidServerKeys.Count != 0)
                invalidServerKeys = [.. invalidServerKeys.Where(k => !k.StartsWith(serverID.ToString()))];

            if (invalidServerKeys.Count != 0)
            {
                foreach (var key in invalidServerKeys)
                    msgs.Remove(key, out _);

                needSave = true;
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
                foreach (var key in invalidServerKeys)
                    msgs.Remove(key, out _);

                needSave = true;
            }
        }

        public async Task Process_GuildAvailable(DiscordClient c, DiscordGuild guild)
        {
            _ = Task.Run(async () =>
            {
                _ = logger.LogBasic("Guild Available", $"Server: {guild.Name} ({guild.Id})");

                List<Task> tasks = [];

                foreach (var key in msgs.Keys.Where(k => k.StartsWith($"{guild.Id}_")))
                    if (msgs.TryGetValue(key, out var msg))
                    {
                        DiscordChannel? chnl = null;
                        try { chnl = await guild.GetChannelAsync(msg.Channel_ID); }
                        catch
                        {
                            if (msg.Channel_Errors < 10)
                            {
                                if (msgs.TryGetValue(key, out var gluedMessage))
                                    msgs.TryUpdate(key, new GluedMessage(gluedMessage) { Channel_Errors = msg.Channel_Errors + 1 }, gluedMessage);
                                _ = logger.LogError($"Failed to access channel for sticky.\r\n**Server**: {guild.Name} ({guild.Id})\r\n**Channel ID**: {msg.Channel_ID}");
                            }
                            else
                            {
                                msgs.Remove($"{msg.Server_ID}_{msg.Channel_ID}", out _);
                                _ = logger.LogError($"Failed to access channel for sticky after multiple attempts. Removing sticky.\r\n**Server**: {guild.Name} ({guild.Id})\r\n**Channel ID**: {msg.Channel_ID}");
                            }

                            needSave = true;
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
            if (msgs.Remove($"{e.Guild!.Id}_{e.Channel.Id}", out _))
            {
                needSave = true;
                await logger.LogBasic("Auto Clean", $"Sticky automatically deleted as channel was deleted.\r\n**Server**: {e.Guild.Name} ({e.Guild.Id})\r\n**Channel**: \\#{e.Channel.Name} ({e.Channel.Id})");
            }
        }

        public async Task Process_MessageCreated(DiscordClient c, MessageCreatedEventArgs e) => await Process_Sticky(c, e.Guild, e.Channel, e.Message);


        public async Task Process_Sticky(DiscordClient c, DiscordGuild guild, DiscordChannel channel, DiscordMessage? m = null)
        {
            try
            {
                string msgKey = $"{guild.Id}_{channel.Id}";
                if (settings.EnabledServers.Contains(guild.Id) || settings.Admin.ServerID == guild.Id)
                    if (msgs.TryGetValue(msgKey, out var message))
                        if ((m is null || m.Id != message.Message_ID) && !message.isWatching)
                        {
                            _ = logger.LogBasic("Processing Sticky", $"**Server**: {guild.Name} ({guild.Id}) -> \\#{channel.Name} ({channel.Id})");

                            msgs.TryUpdate(msgKey, new GluedMessage(message) { isWatching = true }, message);
                            needSave = true;
                            await Task.Delay(5 * 1000);

                            if (msgs.TryGetValue(msgKey, out var msg))
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
                                                if (msgs.TryGetValue(msgKey, out var hookErrorMsg))
                                                    msgs.TryUpdate(msgKey, new GluedMessage(hookErrorMsg) { Webhook_Errors = msg.Webhook_Errors + 1 }, hookErrorMsg);
                                                _ = logger.LogError($"Failed to generate a webhook for sticky.\r\n**Server**: {guild.Name} ({guild.Id})\r\n**Channel:** \\#{channel.Name} ({channel.Id})");
                                            }
                                            else
                                            {
                                                msgs.Remove($"{msg.Server_ID}_{msg.Channel_ID}", out _);
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

                                            if (msgs.TryGetValue(msgKey, out var successfulMessage))
                                                msgs.TryUpdate(msgKey, new GluedMessage(successfulMessage)
                                                    { 
                                                        Message_ID = hookMsg.Id,
                                                        Channel_Errors = 0,
                                                        Webhook_Errors = 0 
                                                    }
                                                    , successfulMessage
                                                );

                                            _ = logger.LogBasic("Sticky Posted", $"**Server**: {channel.Guild.Name} ({channel.Guild.Id}) -> \\#{channel.Name} ({channel.Id})");
                                        }
                                        catch (Exception ex)
                                        {
                                            _ = logger.LogError($"Error during webhook submission.\r\n**Server**: {guild.Name} ({guild.Id})\r\n**Channel:** \\#{channel.Name} ({channel.Id})", guild, ex);
                                        }

                                    if (msgs.TryGetValue(msgKey, out var gluedMessage))
                                        msgs.TryUpdate(msgKey, new GluedMessage(gluedMessage) { isWatching = false }, gluedMessage);
                                    needSave = true;
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
