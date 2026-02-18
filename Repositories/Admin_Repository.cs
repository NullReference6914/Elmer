using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using ElmerBot.Enums;
using ElmerBot.Models;
using Microsoft.Extensions.Options;

namespace ElmerBot.Repositories
{
    internal interface IAdmin_Repository
    {
        Task GetMembers(SlashCommandContext ctx, DiscordRole role, MemberOutputType type);
        Task Server_Leave(SlashCommandContext ctx, string serverID);
        Task Server_Allow(SlashCommandContext ctx, string serverID);
        Task Server_Disallow(SlashCommandContext ctx, string serverID);
        Task Server_View(SlashCommandContext ctx);
    }
    internal class Admin_Repository(IOptionsSnapshot<Settings> _config, ILogging_Repository logger, IGlue_Repository glueRepo) : IAdmin_Repository
    {
        Settings settings => _config.Value;

        public async Task GetMembers(SlashCommandContext ctx, DiscordRole role, MemberOutputType type)
        {
            try
            {
                List<string> msgs = new List<string>();

                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"Please wait while I build the list. This could take a while.").AsEphemeral());

                await foreach (var m in ctx.Guild!.GetAllMembersAsync())
                {
                    if (m.Roles.Contains(role))
                        this.AddTextToMessage(
                            type switch
                            {
                                MemberOutputType.IDs => m.Id.ToString() + "\r\n",
                                MemberOutputType.Mentions => m.Mention + " ",
                                MemberOutputType.Text => m.DisplayName + "\r\n",
                                MemberOutputType.Mentions_with_IDs => m.Mention + " - " + m.Id.ToString() + "\r\n",
                                MemberOutputType.Text_with_IDs => m.DisplayName + " - " + m.Id.ToString() + "\r\n",
                                _ => throw new ArgumentException("Invalid Display Type.", "type")
                            }
                            , ref msgs
                        );
                }

                if (msgs.Count > 0)
                {
                    msgs.ForEach(m => ctx.Channel.SendMessageAsync(m.Trim(new char[] { '\r', '\n' })).Wait());
                }
                else
                {
                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"There does not seem to be any members with this role assigned. Sorry about that.").AsEphemeral());
                }
            }
            catch (Exception ex)
            {
                await logger.LogError("Error during Get Members Command.", ctx, ex);
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"An error occured during the getting of the members with that role.").AsEphemeral());
            }
        }


        private void AddTextToMessage(string text, ref List<string> msgs)
        {
            if (msgs.Count == 0)
            {
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

        public async Task Server_Leave(SlashCommandContext ctx, string serverID)
        {
            try
            {
                if (!String.IsNullOrEmpty(serverID))
                    if (ulong.TryParse(serverID, out ulong server_id))
                    {
                        await ctx.Client.Guilds[server_id].LeaveAsync();
                        await glueRepo.RemoveServer(server_id);
                    }

                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("I have left or attempted to leave the provided server.").AsEphemeral());
            }
            catch (Exception ex)
            {
                await Task.WhenAll(
                    ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("An error occured during the attempt to leave the provided server.").AsEphemeral()).AsTask(),
                    logger.LogError("Error during Server Leave Command.", ctx, ex)
                );
            }
        }

        public async Task Server_Allow(SlashCommandContext ctx, string serverID)
        {
            try
            {
                if (!String.IsNullOrEmpty(serverID))
                    if (ulong.TryParse(serverID, out ulong server_id))
                    {
                        settings.EnabledServers.Add(server_id);
                        settings.EnabledServers = settings.EnabledServers.Distinct().ToList();
                        var (success, ex) = await settings.Save();
                        if (success)
                        {
                            await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("I have added the server to the list of allowed server IDs.").AsEphemeral());
                        }
                        else
                        {
                            await Task.WhenAll(
                                logger.LogError("Error during Server Allow Command.", ctx, ex),
                                ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"An error occured during the saving of that server ID").AsEphemeral()).AsTask()
                            );
                        }
                        return;
                    }

                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("An error occured during the adding of that server ID.").AsEphemeral());
            }
            catch (Exception ex)
            {
                await Task.WhenAll(
                    logger.LogError("Error during Server Allow Command.", ctx, ex),
                    ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("An error occured during the adding of that server ID.").AsEphemeral()).AsTask()
                );
            }
        }

        public async Task Server_Disallow(SlashCommandContext ctx, string serverID)
        {
            try
            {
                if (!String.IsNullOrEmpty(serverID))
                    if (ulong.TryParse(serverID, out ulong server_id))
                    {
                        settings.EnabledServers.Remove(server_id);

                        var (success, ex) = await settings.Save();
                        if (success)
                        {
                            await Task.WhenAll(
                                ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("I have removed the server from the list of allowed server IDs.").AsEphemeral()).AsTask(),
                                glueRepo.RemoveServer(server_id)
                            );
                        }
                        else
                        {
                            await Task.WhenAll(
                                logger.LogError("Error during Server Disallow Command.", ctx, ex),
                                ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"An error occured during the removing of that server ID").AsEphemeral()).AsTask()
                            );
                        }
                        return;
                    }

                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("An error occured during the removal of that server ID.").AsEphemeral());
            }
            catch (Exception ex)
            {
                await Task.WhenAll(
                    logger.LogError("Error during Server Disallow Command.", ctx, ex),
                    ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("An error occured during the removing of that server ID.").AsEphemeral()).AsTask()
                );
            }
        }

        public async Task Server_View(SlashCommandContext ctx)
        {
            try
            {
            List<string> serverInfo = [];

            await foreach(var server in ctx.Client.GetGuildsAsync())
            {
                DiscordMember? owner = await server.GetAllMembersAsync().FirstOrDefaultAsync(m => m.IsOwner);
                var members = server.GetAllMembersAsync();
                var channels = await server.GetChannelsAsync();

                int memberCount = await members.CountAsync(m => !m.IsBot),
                    botCount = await members.CountAsync(m => m.IsBot),
                    categories = channels.Count(c => c.IsCategory),
                    vcs = channels.Count(c => c.Type == DiscordChannelType.Voice),
                    textChannels = channels.Count(c => !c.IsThread && !c.IsCategory && c.Type == DiscordChannelType.Text);

                serverInfo.Add($@"### {server.Name} ({server.Id}) 
> **Enabled**: :{((settings.EnabledServers.Contains(server.Id)) ? "white_check_mark" : "x")}:
> **Owner**: {owner?.DisplayName} ({owner?.Id})
> **Stats**: Users ( {memberCount} :man_technologist: / {botCount} :robot: ) Chanenls ( {categories} :open_file_folder: / {textChannels} :hash: / {vcs} :microphone: )");
            }
        
            await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("Below are the servers which I am currently in."));
            do
            {
                string mesage = "";
                do
                {
                    string newMsg = stickyMsgs.First();
                    mesage += ((mesage.Length > 0) ? "\r\n\r\n" : "") + newMsg;
                    serverInfo.Remove(newMsg);
                } while (serverInfo.Count > 0 && (mesage + "\r\n\r\n" + serverInfo.FirstOrDefault()).Length < 2000);

                await ctx.Channel.SendMessageAsync(mesage);

                if(serverInfo.Count > 0)
                    await Task.Delay(2000);
            } while (serverInfo.Count > 0);
            }
            catch (Exception ex)
            {
                await Task.WhenAll(
                    logger.LogError("Error during Server View Command.", ctx, ex),
                    ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("An error occured during the viewing of server information.").AsEphemeral()).AsTask()
                );
            }
        }
    }
}
