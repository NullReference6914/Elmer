using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using DSharpPlus;
using DSharpPlus.Commands;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using DSharpPlus.Entities;
using System.Collections.Concurrent;
using ElmerBot.Commands;

//https://discord.com/api/oauth2/authorize?client_id=963613397478424596&permissions=536881168&scope=bot%20applications.commands

namespace ElmerBot
{
    class Program
    {
        public static List<ulong> adminUsers = new List<ulong> {
            342268654404239361,
            //Bee
            805452416056819732
        };
        public static DiscordClient client;
        public static List<GluedMessage> msgs = new List<GluedMessage>();
        public static ConcurrentDictionary<ulong, List<DiscordMessage>> DeleteMessages = new ConcurrentDictionary<ulong, List<DiscordMessage>>();

        private static bool DeleteRunning = false;

        public static List<ulong> AllowedServers = new List<ulong>();

        static void Main(string[] args)
        {
            MainAsync(args).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        static async Task MainAsync(string[] args)
        {
            string queueFolder = Program.GetQueueFilePath(),
                allowedServers = Program.GetAllowedServersFilePath();

            if (System.IO.Directory.Exists(queueFolder))
                if (System.IO.File.Exists(queueFolder + "list.json"))
                    if (Newtonsoft.Json.JsonConvert.DeserializeObject<List<GluedMessage>>(await System.IO.File.ReadAllTextAsync(queueFolder + "list.json")) is List<GluedMessage> m)
                        Program.msgs = m;

            if (System.IO.Directory.Exists(allowedServers))
                if (System.IO.File.Exists(allowedServers + "allowedServers.json"))
                    if (Newtonsoft.Json.JsonConvert.DeserializeObject<List<ulong>>(await System.IO.File.ReadAllTextAsync(allowedServers + "allowedServers.json")) is List<ulong> l)
                        Program.AllowedServers = l;

            if (Program.msgs == null)
                Program.msgs = new List<GluedMessage>();


            string botToken = "",
                tokenLocation = AppDomain.CurrentDomain.BaseDirectory + "token.txt";

            if (System.IO.File.Exists(tokenLocation))
                botToken = System.IO.File.ReadAllText(tokenLocation);

            DiscordClientBuilder builder = DiscordClientBuilder.CreateDefault(botToken, DiscordIntents.AllUnprivileged | DiscordIntents.All);

            builder.UseCommands((IServiceProvider serviceProvider, CommandsExtension extension) =>
            {
                extension.AddCommands<GlueMessageCmd>();
                extension.AddCommands<CustomizeMessage>();
                extension.AddCommands<AdminCommands>();
            });

            builder.ConfigureEventHandlers(b =>
            {
                b.HandleMessageCreated(async (c, e) =>
                {
                    Commands.GlueMessageCmd.ProcessMessageCreated(c, e);
                });
                b.HandleGuildAvailable(async (c, e) =>
                {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    Task.Run(() =>
                    {
                        Program.msgs?
                            .Where(m => m.Server_ID == e.Guild.Id)
                            .ToList()
                            .ForEach(m =>
                            {
                                DiscordChannel channel = null;

                                try { channel = e.Guild.GetChannelAsync(m.Channel_ID).Result; }
                                catch { }

                                if (channel != null)
                                {
                                    DiscordMessage msg = null;
                                    try { msg = channel.GetMessagesAsync(1).ToBlockingEnumerable().ToList()[0]; } catch { }
                                    if ((msg?.Id ?? 0) != m.Message_ID)
                                        Commands.GlueMessageCmd.ProcessMessageCreated(c, e.Guild, channel);
                                }
                                System.Threading.Thread.Sleep(1000);
                            });
                    });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                });
            });


            System.AppDomain.CurrentDomain.UnhandledException += Helpers.CurrentDomain_UnhandledException;
            #region Old Code
            //            Program.client = new DiscordClient(new DiscordConfiguration
            //            {
            //                Token = botToken,
            //                //#endif
            //                TokenType = TokenType.Bot,
            //                Intents = DiscordIntents.All,
            //#if DEBUG
            //                //Dev Output
            //                MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Debug
            //#else
            //                //Live Output
            //                MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Information
            //#endif
            //            });
            //            var slash = Program.client.UseSlashCommands();

            //            new List<Type>
            //            {
            //                typeof(Commands.GlueMessageCmd),
            //                typeof(Commands.CustomizeMessage),
            //                typeof(Commands.AdminCommands),
            //                typeof(Commands.ContextMenu)
            //            }.ForEach(c =>
            //            {
            //#if DEBUG
            //                //To register them for a single server, recommended for testing
            //                slash.RegisterCommands(c, Helpers.adminServerID);
            //#else
            //                //To register them globally, once you're confident that they're ready to be used by everyone
            //                slash.RegisterCommands(c);
            //#endif
            //            });

            //            Program.client.MessageCreated += async (c, e) =>
            //            {
            //                Commands.GlueMessageCmd.ProcessMessageCreated(c, e);
            //            };
            //            Program.client.GuildAvailable += async (c, e) =>
            //            {
            //#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            //                Task.Run(() =>
            //                {
            //                    Program.msgs?
            //                        .Where(m => m.Server_ID == e.Guild.Id)
            //                        .ToList()
            //                        .ForEach(m =>
            //                        {
            //                            DiscordChannel channel = null;

            //                            try { channel = e.Guild.GetChannel(m.Channel_ID); }
            //                            catch { }

            //                            if (channel != null)
            //                            {
            //                                DiscordMessage msg = null;
            //                                try { msg = channel.GetMessagesAsync(1).Result[0]; } catch { }
            //                                if ((msg?.Id ?? 0) != m.Message_ID)
            //                                    Commands.GlueMessageCmd.ProcessMessageCreated(c, e.Guild, channel);
            //                            }
            //                            System.Threading.Thread.Sleep(1000);
            //                        });
            //                });
            //#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            //            };
            #endregion
            await Program.client.ConnectAsync();

            System.Timers.Timer deleteMsg_Timer = new System.Timers.Timer(5000);
            deleteMsg_Timer.AutoReset = true;
            deleteMsg_Timer.Elapsed += DeleteMsg_Timer_Elapsed;
            deleteMsg_Timer.Start();

            await Task.Delay(-1);
        }

        private static void DeleteMsg_Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (!Program.DeleteRunning)
                if (Program.DeleteMessages.Count() > 0)
                {
                    Program.DeleteRunning = true;
                    List<ulong> channelIds = Program.DeleteMessages.Keys.ToList();

                    channelIds.ForEach(k =>
                    {
                        if (Program.DeleteMessages.TryGetValue(k, out List<DiscordMessage> m))
                            if (m?.Count() > 0)
                            {
                                try
                                {
                                    ulong id = m[0].Id;

                                    Console.WriteLine($"[{DateTime.Now}][Message Deletion] Msg {id} from #{m[0].Channel.Name} in {m[0].Channel.Guild.Name}");
                                    m[0].DeleteAsync();

                                    m.RemoveAt(0);

                                    Program.DeleteMessages.AddOrUpdate(
                                        k,
                                        m,
                                        (k, v) =>
                                        {
                                            return v.Where(msg => msg.Id != id).ToList();
                                        }
                                    );
                                }
                                catch { }
                            }
                            else
                            {
                                Program.DeleteMessages.TryRemove(k, out List<DiscordMessage> msgs);
                            }
                    });
                    
                    Program.DeleteRunning = false;
                }
        }

        internal static string GetQueueFilePath()
        {
            string folder = AppDomain.CurrentDomain.BaseDirectory;

            if (!System.IO.Directory.Exists(folder + "/Settings"))
                System.IO.Directory.CreateDirectory(folder + "/Settings");

            folder += "/Settings/";

            return folder;
        }
        internal static string GetAllowedServersFilePath()
        {
            string folder = AppDomain.CurrentDomain.BaseDirectory;

            if (!System.IO.Directory.Exists(folder + "/Servers"))
                System.IO.Directory.CreateDirectory(folder + "/Servers");

            folder += "/Servers/";

            return folder;
        }

        internal static async void Save()
        {
            string queueFolder = Program.GetQueueFilePath();

            if (System.IO.Directory.Exists(queueFolder))
            {
                int counter = 0;
                bool success = false;
                do
                {
                    try
                    {
                        await System.IO.File.WriteAllTextAsync(queueFolder + "list.json", Newtonsoft.Json.JsonConvert.SerializeObject(Program.msgs));
                        success = true;
                    }
                    catch (Exception e)
                    {
                        counter++;
                    }
                } while (!success && counter < 10);
            }
        }
    }
}
