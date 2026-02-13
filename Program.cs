using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using ElmerBot.Classes.Attributes;
using ElmerBot.Commands;
using ElmerBot.Models;
using ElmerBot.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace ElmerBot
{
    class Program
    {
        public static DiscordClient? client;
        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level}{Type}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.Logger(lc =>
                    lc.Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Error)
                        .WriteTo.Async(a =>
                            a.File("Logs\\Errors\\DTCError-.txt",
                                rollingInterval: RollingInterval.Day,
                                retainedFileCountLimit: 30,
                                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level}] {Message:lj}{NewLine}{Exception}"
                            )
                        )
                )
                .CreateLogger();

            Settings settings = null!;

            if (File.Exists("Settings.json"))
            {
                settings = new ConfigurationBuilder()
                        .AddJsonFile("settings.json")
                        .Build()
                        .Get<Settings>()!;
            }
            else
            {
                File.WriteAllText("settings.json", Newtonsoft.Json.JsonConvert.SerializeObject(new Settings()));
                Console.WriteLine("Settings.json file not found. A settings.json file has been created in the same directory as the bot executable. Please update with the proper values");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            DiscordClientBuilder builder = DiscordClientBuilder
                .CreateDefault(settings.Token, DiscordIntents.GuildWebhooks
                    | DiscordIntents.GuildMessages
                    | SlashCommandProcessor.RequiredIntents)
                .ConfigureLogging(l => l.AddProvider(new SerilogLoggerProvider(Log.Logger, true)))
                .UseCommands((IServiceProvider serviceProvider, CommandsExtension extension) =>
                {
                    extension.AddCommands<Glue_Commands>();
                    extension.AddCommands<Customize_Commands>();
                    extension.AddCommands<Admin_Commands>();

                    extension.AddCheck<BasicGuildCheck>();
                    extension.AddCheck<BasicUserCheck>();
                    extension.AddCheck<CustomRequirePermissionsCheck>();
                    extension.AddCheck<RequireAdminCheck>();
                })
                .ConfigureServices(services =>
                {
                    services.Configure<Settings>(new ConfigurationBuilder()
                        .AddJsonFile("settings.json", optional: false, reloadOnChange: true)
                        .Build());

                    services.AddScoped<IGlue_Repository, Glue_Repository>();
                    services.AddScoped<ICustomize_Repository, Customize_Repository>();
                    services.AddScoped<IAdmin_Repository, Admin_Repository>();
                    services.AddSingleton<ILogging_Repository, Logging_Repository>();
                })
                .ConfigureEventHandlers(e =>
                    e.HandleMessageCreated((c, e) => c.ServiceProvider.GetService<IGlue_Repository>()?.ProcessMessageCreated(c, e)!)
                        .HandleGuildAvailable((c, e) => c.ServiceProvider.GetService<IGlue_Repository>()?.ProcessMessageCreated(c, e.Guild)!)
                );

            try 
            {
                client = builder.Build();
                await client.ConnectAsync();
                await Task.Delay(-1);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Dodo Tower Control terminated unexpectedly");
            }
            finally
            {
                client?.Dispose();
                Log.CloseAndFlush();
            }
        }
    }
}
