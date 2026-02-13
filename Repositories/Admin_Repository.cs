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
    }
    internal class Admin_Repository(IOptionsSnapshot<Settings> _config, ILogging_Repository logger) : IAdmin_Repository
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
                        ctx.Client.Guilds[server_id].LeaveAsync().Wait();

                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("I have left or attempted to leave the provided server.").AsEphemeral());
            }
            catch (Exception ex)
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("An error occured during the attempt to leave the provided server.").AsEphemeral());
                await logger.LogError("Error during Server Leave Command.", ctx, ex);
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
                            await logger.LogError("Error during Server Allow Command.", ctx, ex);
                            await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"An error occured during the saving of that server ID").AsEphemeral());
                        }
                        return;
                    }

                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("An error occured during the adding of that server ID.").AsEphemeral());
            }
            catch (Exception ex)
            {
                await logger.LogError("Error during Server Allow Command.", ctx, ex);
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("An error occured during the adding of that server ID.").AsEphemeral());
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
                            await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("I have removed the server from the list of allowed server IDs.").AsEphemeral());
                        }
                        else
                        {
                            await logger.LogError("Error during Server Disallow Command.", ctx, ex);
                            await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent($"An error occured during the removing of that server ID").AsEphemeral());
                        }
                        return;
                    }

                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("An error occured during the removal of that server ID.").AsEphemeral());
            }
            catch (Exception ex)
            {
                await logger.LogError("Error during Server Disallow Command.", ctx, ex);
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("An error occured during the removing of that server ID.").AsEphemeral());
            }
        }
    }
}
