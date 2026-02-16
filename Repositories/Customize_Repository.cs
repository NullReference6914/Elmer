using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using ElmerBot.Classes;
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
        StickyVault vault => glueRepo.GetMessages();

        public async Task SetProfilePicture(SlashCommandContext ctx, ulong channelId, string? url)
        {
            try
            {
                if (await vault.TryGetValue($"{ctx.Guild!.Id}_{channelId}") is (bool getSuccess, GluedMessage msg) && getSuccess)
                {
                    await vault.TryUpdate($"{ctx.Guild!.Id}_{channelId}", new(msg) { Avatar_Url = url }, msg);
                    await Task.WhenAll(
                        ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("The pfp has been set.").AsEphemeral()).AsTask(),
                        glueRepo.Process_Sticky(ctx.Client, ctx.Guild!, ctx.Channel)
                    );
                }
                else
                {
                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("There is currently no sticky for the provided channel.").AsEphemeral());
                }
            }
            catch (Exception ex)
            {
                await Task.WhenAll(
                    ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("An error occurred while setting the profile picture.").AsEphemeral()).AsTask(),
                    logger.LogError($"Error in Set Profile Picture Command", ctx, ex)
                );
            }
        }

        public async Task SetUsername(SlashCommandContext ctx, ulong channelId, string? username)
        {
            try
            {
                if (await vault.TryGetValue($"{ctx.Guild!.Id}_{channelId}") is (bool getSuccess, GluedMessage msg) && getSuccess)
                {
                    await vault.TryUpdate($"{ctx.Guild!.Id}_{channelId}", new(msg) { Username = username }, msg); 
                    await Task.WhenAll(
                        ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("The username has been set.").AsEphemeral()).AsTask(),
                        glueRepo.Process_Sticky(ctx.Client, ctx.Guild!, ctx.Channel)
                    );
                }
                else
                {
                    await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("There is currently no sticky for the provided channel.").AsEphemeral());
                }
            }
            catch (Exception ex)
            {
                await Task.WhenAll(
                    ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent("An error occurred while setting the username.").AsEphemeral()).AsTask(),
                    logger.LogError($"Error in Set Username Command", ctx, ex)
                );
            }
        }
    }
}
