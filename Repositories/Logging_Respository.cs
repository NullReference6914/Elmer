using DSharpPlus.Commands;
using DSharpPlus.Entities;
using ElmerBot.Models;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Context;

namespace ElmerBot.Repositories
{

    internal interface ILogging_Repository
    {
        Task LogBasic(string section, string msg);
        Task LogError(string Error, CommandContext Context = null!, Exception Exception = null!);
        Task LogError(string Error, DiscordGuild Guild, Exception Exception = null!);
    }
    internal class Logging_Repository : ILogging_Repository, IDisposable
    {
        readonly IOptionsSnapshot<Settings> _config;
        Settings Settings => _config.Value;

        readonly List<string> msgs = [];

        bool processingMsgs;

        readonly System.Timers.Timer logTimer;
        private bool disposedValue;

        public Logging_Repository(IOptionsSnapshot<Settings> config)
        {
            this._config = config;

            this.logTimer = new System.Timers.Timer(2000)
            {
                AutoReset = true
            };
            this.logTimer.Elapsed += async (sender, e) => await PostMessages();
            this.logTimer.Start();
        }

        static string GenerateLogType(string type) => " - " + type;

        public async Task LogBasic(string section, string msg)
        {
            using (LogContext.PushProperty("Type", GenerateLogType(section)))
                Log.Information(msg);
            msgs.Add($"{DateTime.Now.ToDiscordDisplay(TimeFormat.LongTime)} **[{section}]** - {msg}");
        }

        async Task PostMessages()
        {
            if (this.msgs.Count > 0 && !this.processingMsgs)
            {
                this.processingMsgs = true;

                try
                {
                    string msg = "";

                    do
                    {
                        string newMsg = msgs.First();
                        msg += ((msg.Length > 0) ? "\r\n" : "") + newMsg;
                        msgs.Remove(newMsg);
                    } while (msgs.Count > 0 && (msg + "\r\n" + msgs.FirstOrDefault()).Length < 2000);

                    await this.SendMessage("Event Logging", msg, this.Settings.Admin.ChannelID);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error posting log message");
                }

                processingMsgs = false;
            }
        }

        async Task SendMessage(string logType, string msg, ulong? chnlID)
        {
            try
            {
                DiscordGuild guild = null!;
                while (guild is null)
                {
                    guild = null!;
                    try
                    {
                        if (this.Settings.Admin.ServerID.HasValue)
                            guild = Program.client?.GetGuildAsync(this.Settings.Admin.ServerID.Value)?.Result!;
                    }
                    catch { }

                    if (guild is null)
                        Thread.Sleep(1000);
                }
                if (chnlID.HasValue)
                {
                    DiscordChannel chnl = guild.GetChannelAsync(chnlID.Value).Result;
                    chnl?.SendMessageAsync(msg).Wait();
                    Thread.Sleep(1000);
                }
            }
            catch (Exception e)
            {
                await this.LogError($"Error duing logging {logType} '{msg}'", Exception: e);
            }
        }

        public async Task LogError(string Error, DiscordGuild guild, Exception ex = null!) => await LogError($"Error Server {guild.Name}, ID: {guild.Id}. {Error}", Exception: ex);
        public async Task LogError(string Error, CommandContext Context = null!, Exception Exception = null!)
        {
            Log.Error(Exception, Error);
            string errormsg = "";

            if (Exception is not null)
                errormsg = ((Exception.InnerException is not null) ? "\r\n### Inner Error Information\r\n\r\n" + Exception.InnerException.GetType().FullName + " - " + Exception.InnerException.Message + "\r\n\r\n**Stack Trace** ```" + Exception.InnerException.StackTrace?.Trim()[3..] + "```" : "") + "\r\n### Main Error Information\r\n\r\n" + Exception.GetType().FullName + " - " + Exception.Message + ((!String.IsNullOrEmpty(Exception.StackTrace)) ? "\r\n\r\n**Stack Trace** ```" + Exception.StackTrace.Trim()[3..] + "```" : "");

            errormsg = $"\r\n{Error}\r\n" + ((Context is not null) ? $"\r\n**Server**: {Context.Guild?.Name}, {Context.Guild?.Id}\r\n**User**: \\@{Context.User?.GlobalName} ({Context.User?.Username}), {Context.User?.Id}\r\n**Channel**: \\#{Context.Channel.Name}, {Context.Channel.Id}\r\n" : "") + errormsg;

            errormsg = $"{DateTime.Now.ToDiscordDisplay(TimeFormat.LongTime)} **[### ERROR ###]**\r\n" + errormsg;

            string msg = "";
            errormsg.Split("\r\n\r\n")
               .ToList()
               .ForEach(m =>
               {
                   if ($"{msg}{m}\r\n\r\n".Length > 2000)
                   {
                       msgs.Add(msg);
                       msg = "";
                   }
                   msg += ((!String.IsNullOrEmpty(msg)) ? "\r\n\r\n" : "") + m;
               });

            if (!String.IsNullOrEmpty(msg))
                msgs.Add(msg);
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    this.logTimer?.Stop();
                    this.logTimer?.Dispose();
                }

                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
