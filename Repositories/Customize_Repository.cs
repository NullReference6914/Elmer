using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using ElmerBot.Models;
using Microsoft.Extensions.Options;

namespace ElmerBot.Repositories
{
    internal interface ICustomize_Repository
    {
        Task SetProfilePicture(SlashCommandContext ctx, ulong channelId, string? url);
        Task SetUsername(SlashCommandContext ctx, ulong channelId, string? username);
    }
    internal class Customize_Repository(ILogging_Repository logger, IGlue_Repository glueRepo) : ICustomize_Repository
    {
        public async Task SetProfilePicture(SlashCommandContext ctx, ulong channelId, string? url)
        {
            try
            {
                if (glueRepo.GetMessages().TryGetValue($"{ctx.Guild!.Id}_{channelId}", out var msg))
                {
                    msg.Avatar_Url = url;
                    glueRepo.Save();

                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("The pfp has been set.").AsEphemeral());

                    await glueRepo.ProcessMessageCreated(ctx.Client, ctx.Guild!, ctx.Channel);
                }
                else
                {
                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("There is currently no glued message for the provided channel.").AsEphemeral());
                }
            }
            catch (Exception ex)
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("An error occurred while setting the profile picture.").AsEphemeral());
                await logger.LogError($"Error in Set Profile Picture Command", ctx, ex);
            }
        }

        public async Task SetUsername(SlashCommandContext ctx, ulong channelId, string? username)
        {
            try
            {
                if (glueRepo.GetMessages().TryGetValue($"{ctx.Guild!.Id}_{channelId}", out var msg))
                {
                    msg.Username = username;
                    glueRepo.Save();

                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("The username has been set.").AsEphemeral());

                    await glueRepo.ProcessMessageCreated(ctx.Client, ctx.Guild!, ctx.Channel);
                }
                else
                {
                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("There is currently no glued message for the provided channel.").AsEphemeral());
                }
            }
            catch (Exception ex)
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("An error occurred while setting the username.").AsEphemeral());
                await logger.LogError($"Error in Set Username Command", ctx, ex);
            }
        }
    }
}
