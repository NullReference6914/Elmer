using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands;

namespace ElmerBot.Commands
{
    internal class AdminCommands
    {
        [Command("hi")]
        [RequireApplicationOwner]
        public async Task Hi(SlashCommandContext ctx)
        {
            await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"👋 Hi, {ctx.User.Username}!").AsEphemeral());
        }

        [Command("members")]
        [RequirePermissions(DiscordPermission.ManageRoles)]
        public async Task members(SlashCommandContext ctx,
            [Parameter("Role")] DiscordRole primaryRole,
            [Parameter("Output_Type")] MemberListOutputType type
        )
        {
            List<string> msgs = new List<string>();

            await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"Please wait while I build the list. This could take a while.").AsEphemeral());

            await foreach(var m in ctx.Guild.GetAllMembersAsync())
            {
                if(m.Roles.Contains(primaryRole))
                    this.AddTextToMessage(
                        type switch
                        {
                            MemberListOutputType.IDs => m.Id.ToString() + "\r\n",
                            MemberListOutputType.Mentions => m.Mention + " ",
                            MemberListOutputType.Text => m.DisplayName + "\r\n",
                            MemberListOutputType.Mentions_with_IDs => m.Mention + " - " + m.Id.ToString() + "\r\n",
                            MemberListOutputType.Text_with_IDs => m.DisplayName + " - " + m.Id.ToString() + "\r\n",
                            _ => throw new ArgumentException("Invalid Display Type.", "type")
                        }
                        , ref msgs
                    );
            }

            if(msgs.Count > 0)
            {
                msgs.ForEach(m => ctx.Channel.SendMessageAsync(m.Trim(new char[] { '\r','\n' })).Wait());
            }
            else
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"There does not seem to be any members with this role assigned. Sorry about that.").AsEphemeral());
            }
        }

        public enum MemberListOutputType
        {
            Mentions,
            Text,
            IDs,
            Mentions_with_IDs,
            Text_with_IDs
        }

        [Command("servermap")]

        [RequireApplicationOwner]
        public async Task serverMap(SlashCommandContext ctx,
            [Parameter("Primary_View_Role")] DiscordRole primaryRole,
            [Parameter("Secondary_View_Role")] DiscordRole secondaryRole = null,
            [Parameter("IgnoreIDs")]string ignoreIDs_str = "")
        {
            try
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"Please wait. This can take a couple minutes. I am generating the map...").AsEphemeral());

                List<string> msgs = new List<string>(),
                    ignoreList = ignoreIDs_str.Split(' ').ToList();

                Dictionary<int, string> categoryOutput = new();

                List<Task<(int, string)>> tasks = new List<Task<(int, string)>>();

                (await ctx.Guild.GetChannelsAsync())
                    .Where(c => !ignoreList.Contains(c.Id.ToString()) && c.Parent == null)
                    .OrderBy(c => c.Position)
                    .ToList()
                    .ForEach(async c =>
                    {
                        if (c.IsCategory)
                        {
                            string catOutput = $"__**{c.Name}**__",
                                chnlOutput = "";
                            Dictionary<int, string> catSubchannelOutput = new();

                            c.Children
                                .Where(cc => !ignoreList.Contains(cc.Id.ToString()))
                                .OrderBy(cc => cc.Position)
                                .ToList()
                                .ForEach(cc =>
                                {
                                    var chnl = this.ProcessChannel(cc, primaryRole, secondaryRole).Result;
                                    catSubchannelOutput.Add(cc.Position, chnl.Item2);
                                });

                            catSubchannelOutput.OrderBy(kvp => kvp.Key)
                                .ToList()
                                .ForEach(v => chnlOutput += v.Value);

                        categoryOutput.Add(c.Position, ((!String.IsNullOrEmpty(chnlOutput)) ? catOutput + chnlOutput : ""));
                        }
                        else
                        {
                            var chnl = this.ProcessChannel(c, primaryRole, secondaryRole).Result;
                            categoryOutput.Add(c.Position - 250, chnl.Item2);
                        }
                    });

                categoryOutput.OrderBy(k => k.Key)
                    .ToList().ForEach(m => this.AddTextToMessage("\r\n\r\n" + m.Value, ref msgs));

                msgs.ForEach(m => ctx.Channel.SendMessageAsync(m).Wait());
            }
            catch(Exception ex)
            {
                Helpers.log_error("Server Mapping", ex);
            }
        }

        private async Task<(int, string)> ProcessChannel(DiscordChannel chnl, DiscordRole role1, DiscordRole role2)
        {
            string output = "";
            var perms = chnl.PermissionOverwrites
                .Where(o => o.Type == DiscordOverwriteType.Role)
                .Select(o => new
                {
                    Allowed = o.Allowed,
                    Denied = o.Denied,
                    Role = o.GetRoleAsync().Result
                })
                .ToList();

            DiscordRole highestRole = (role1?.Position > role2?.Position) ? role1 : role2,
                lowestRole = (role1?.Position < role2?.Position) ? role1 : role2;

            if (highestRole == lowestRole)
                lowestRole = null;

            var highestOverride = (highestRole != null) ? perms.FirstOrDefault(o => o.Role == highestRole) : null;
            highestRole = null;
            var lowestOverride = (lowestRole != null) ? perms.FirstOrDefault(o => o.Role == lowestRole) : null;
            lowestRole = null;
            var everyoneOverride = perms.FirstOrDefault(o => o.Role == chnl.Guild.EveryoneRole);
            perms = null;

            bool viewable = true;

            if (everyoneOverride != null)
            {
                if (everyoneOverride.Denied.HasPermission(DiscordPermission.ViewChannel))
                {
                    viewable = false;
                    if (lowestOverride != null)
                    {
                        if (lowestOverride.Allowed.HasPermission(DiscordPermission.ViewChannel))
                        {
                            viewable = true;
                            if (highestOverride != null)
                                if (highestOverride.Denied.HasPermission(DiscordPermission.ViewChannel))
                                    viewable = false;
                        }
                        else if (highestOverride != null)
                        {
                            if (highestOverride.Allowed.HasPermission(DiscordPermission.ViewChannel))
                                viewable = true;
                        }
                    }
                    else if (highestOverride != null)
                    {
                        if (highestOverride.Allowed.HasPermission(DiscordPermission.ViewChannel))
                            viewable = true;
                    }
                }
                else
                {
                    if (lowestOverride != null)
                    {
                        if (lowestOverride.Denied.HasPermission(DiscordPermission.ViewChannel))
                        {
                            viewable = false;
                            if (highestOverride != null)
                                if (highestOverride.Allowed.HasPermission(DiscordPermission.ViewChannel))
                                    viewable = true;
                        }
                        else if (highestOverride != null)
                        {
                            if (highestOverride.Denied.HasPermission(DiscordPermission.ViewChannel))
                                viewable = false;
                        }
                    }
                }
            }
            else if (lowestOverride != null)
            {
                if (lowestOverride.Denied.HasPermission(DiscordPermission.ViewChannel))
                {
                    viewable = false;
                    if (highestOverride != null)
                        if (highestOverride.Allowed.HasPermission(DiscordPermission.ViewChannel))
                            viewable = true;
                }
                else if (highestOverride != null)
                {
                    if (highestOverride.Denied.HasPermission(DiscordPermission.ViewChannel))
                        viewable = false;
                }
            }
            else if (highestOverride != null)
            {
                if (highestOverride.Denied.HasPermission(DiscordPermission.ViewChannel))
                    viewable = false;
            }

            if (viewable)
                output = $"\r\n{chnl.Mention + ((!String.IsNullOrEmpty(chnl.Topic)) ? ": " + chnl.Topic : "")}";

            return (chnl.Position, output);
        }

        private void AddTextToMessage(string text, ref List<string> msgs)
        {
            if (msgs.Count == 0) {
                msgs.Add(text);
            } 
            else if (msgs[msgs.Count() - 1].Length + (text).Length < 2000)
            {
                msgs[msgs.Count() - 1] += text;
            } 
            else
            {
                msgs.Add(text);
            }
        }
        [Command("mbrlist")]

        [RequireApplicationOwner]
        public async Task mbrList(SlashCommandContext ctx)
        {
            if (ctx.Guild != null)
            {
                await ctx.DeferResponseAsync();

                List<DSharpPlus.Entities.DiscordMember> mbrs = new();

                await foreach (var m in ctx.Guild.GetAllMembersAsync())
                    mbrs.Add(m);

                if (mbrs.Count > 0)
                {
                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent("Pulling member list...").AsEphemeral());
                    List<string> mbrInfo = new List<string> { "\"ID\",\"DisplayName\",\"UserName\"," };
                    mbrs.ForEach(m => mbrInfo.Add($"\"{m.Id}\",\"{m.DisplayName}\",\"{m.Username}#{m.Discriminator}\""));


                    string folder = AppDomain.CurrentDomain.BaseDirectory;
                    if (!System.IO.Directory.Exists(folder + "/MemberListExport"))
                        System.IO.Directory.CreateDirectory(folder + "/MemberListExport");

                    folder += "/MemberListExport/" + DateTime.Now.ToString("yyyy-MM-dd__hh-mm-ss") + ".txt";

                    System.IO.File.WriteAllLines(folder, mbrInfo);

                    System.IO.FileStream stream = System.IO.File.OpenRead(folder);

                    DSharpPlus.Entities.DiscordMessageBuilder builder = new DSharpPlus.Entities.DiscordMessageBuilder();
                    builder.Content = "Attached is the member list.";
                    builder.AddFile("MemberList_" + DateTime.Now.ToString("yyyy-MM-dd__hh-mm-ss") + ".txt", stream);

                    await ctx.Channel.SendMessageAsync(builder);

                    stream.Close();
                    System.IO.File.Delete(folder);
                }
                else
                {
                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent("Unable to pull member list."));
                }
            }
            else
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("This command must be run in a guild").AsEphemeral());
            }
        }

        [Command("gooffline")]

        [RequireApplicationOwner]
        public async Task hide(SlashCommandContext ctx)
        {
            await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("Okay, let me get the invisibilty Cloak.").AsEphemeral());
            await ctx.Client.UpdateStatusAsync(null, DiscordUserStatus.Invisible);
            await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("I should be hidden now.").AsEphemeral());
        }
        [Command("goonline")]
        [RequireApplicationOwner]
        public async Task show(SlashCommandContext ctx)
        {
            await ctx.Client.UpdateStatusAsync(null, DiscordUserStatus.Online);
            await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("I am back now!").AsEphemeral());
        }



        [Command("servers")]
        [RequireApplicationOwner]
        public async Task view_all_servers(SlashCommandContext ctx)
        {
            List<DiscordGuild> guilds = ctx.Client.Guilds.Select(kvp => kvp.Value).ToList();

            string output = "";

            guilds.ForEach(g => output += ((!String.IsNullOrEmpty(output)) ? "\n" : "") + g.Id + " - " + g.Name + " \nOwner: " + g.GetGuildOwnerAsync().Result.Username);

            await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"Below are the servers I am active in.\n\n{output}"));
        }
        [Command("leave")]
        [RequireApplicationOwner]
        public async Task leave_server(SlashCommandContext ctx, 
            [Parameter("Server_ID")] string serverID)
        {
            if (!String.IsNullOrEmpty(serverID))
                if (ulong.TryParse(serverID, out ulong server_id))
                    ctx.Client.Guilds[server_id].LeaveAsync().Wait();

            await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("I have left or attempted to leave the provided server.").AsEphemeral());
        }

        [Command("move-role")]
        [RequirePermissions(DiscordPermission.ManageRoles)]
        public async Task moveRole(SlashCommandContext ctx, 
            [Parameter("Move_Role")] DiscordRole moveRole, 
            [Parameter("Target_Role")] DiscordRole targetRole, 
            [Parameter("Direction")] MoveDirection direction)
        {
            List<DiscordRole> roles = (await ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id))?.Roles.ToList();

            if(roles.Count > 0)
            {
                int highestRoleOrder = roles.Max(r => r.Position);

                if(moveRole.Position < highestRoleOrder)
                {
                    if(
                        (targetRole.Position < highestRoleOrder && direction == MoveDirection.Above)
                        || (targetRole.Position - 1 < highestRoleOrder && direction == MoveDirection.Below)
                    )
                    {
                        switch(direction)
                        {
                            case MoveDirection.Above:
                                await moveRole.ModifyPositionAsync(targetRole.Position);
                                break;
                            case MoveDirection.Below:
                                await moveRole.ModifyPositionAsync(targetRole.Position - 1);
                                break;
                            default:
                                break;
                        }
                        await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"The role `@{moveRole.Name}` has been moved {direction.ToString().ToLower()} `@{targetRole.Name}`").AsEphemeral());
                    }
                    else
                    {
                        await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("I am unable to move the selected role as the destination position is my highest role.").AsEphemeral());
                    }
                }
                else
                {
                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("I am unable to move the selected roles as its position is higher then my highest role..").AsEphemeral());
                }
            }
        }



        [Command("allow_server")]
        public async Task allow_server(SlashCommandContext ctx,
            [Parameter("Server_ID")] string serverID)
        {
            if (Program.adminUsers.Contains(ctx.Member.Id))
            {
                if (!String.IsNullOrEmpty(serverID))
                    if (ulong.TryParse(serverID, out ulong server_id))
                    {
                        Program.AllowedServers.Add(server_id);
                        Program.AllowedServers = Program.AllowedServers.Distinct().ToList();
                        System.IO.File.WriteAllText(Program.GetAllowedServersFilePath() + "allowedServers.json", System.Text.Json.JsonSerializer.Serialize(Program.AllowedServers));
                        await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("I have added the server to the list of allowed server IDs.").AsEphemeral());
                        return;
                    }

                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("An error occured during the saving of that server ID.").AsEphemeral());
            }
        }
        [Command("disallow_server")]
        public async Task disallow_server(SlashCommandContext ctx,
            [Parameter("Server_ID")] string serverID)
        {
            if (Program.adminUsers.Contains(ctx.Member.Id))
            {
                if (!String.IsNullOrEmpty(serverID))
                    if (ulong.TryParse(serverID, out ulong server_id))
                    {
                        Program.AllowedServers.Remove(server_id);
                        System.IO.File.WriteAllText(Program.GetAllowedServersFilePath() + "allowedServers.json", System.Text.Json.JsonSerializer.Serialize(Program.AllowedServers));
                        await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("I have removed the server from the list of allowed server IDs.").AsEphemeral());
                        return;
                    }

                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("An error occured during the saving of that server ID.").AsEphemeral());
            }
        }

        [Command("clear")]
        [RequirePermissions(DiscordPermission.ManageMessages | DiscordPermission.ViewChannel | DiscordPermission.SendMessages)]
        [RequireApplicationOwner]
        public async Task clearChannel(SlashCommandContext ctx)
        {
            List<DiscordMessage> msgs = new(),
                pulledMsgs = new();
            ulong lastMsgID = 0;

            await ctx.RespondAsync(new DiscordInteractionResponseBuilder { Content = "Pulling messages. This can take time. Please wait." });

            do
            {
                try
                {
                    await foreach (var m in ((lastMsgID != 0) ? ctx.Channel.GetMessagesBeforeAsync(lastMsgID) : ctx.Channel.GetMessagesAsync()))
                        msgs.Add(m);

                    if ((pulledMsgs?.Count() ?? 0) > 0)
                    {
                        DiscordMessage LastMessage = pulledMsgs.OrderBy(m => m.Timestamp).FirstOrDefault();
                        lastMsgID = LastMessage?.Id ?? 0;
                        msgs = msgs.Concat(pulledMsgs.Where(m => m.Timestamp.ToLocalTime() < DateTime.Now.AddDays(-14) && !(m.Pinned ?? false))).Distinct().ToList();
                    }
                }
                catch (Exception e)
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder { Content = $"An error occured during pulling messages. No messages were deleted." });
                    return;
                }
                await Task.Delay(1000);
            }
            while ((pulledMsgs?.Count() ?? 0) > 0);

            Program.DeleteMessages.AddOrUpdate(
                ctx.Channel.Id, 
                msgs, 
                (k, v) =>
                {
                    return v.Concat(msgs).Distinct().ToList();
                }
            );

            await ctx.EditResponseAsync(new DiscordWebhookBuilder { Content = $"A total of {msgs.Count} messages were found and added to the deletion queue." });
        }


        public enum MoveDirection
        {
            Above,
            Below
        }
    }
}
