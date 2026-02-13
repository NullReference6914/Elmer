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
        Task LogError(string Error, CommandContext Context = null!, Exception Exception = null!, List<string>? FormatFixes = null);
        Task LogError(string Error, DiscordGuild Guild, Exception Exception = null!);
    }
    internal class Logging_Repository : ILogging_Repository, IDisposable
    {
        IOptionsSnapshot<Settings> _config;
        Settings settings => _config.Value ?? _settings;
        Settings _settings;

        List<string> msgs = [],
            errorMsgs = [];

        bool processingMsgs,
            processingErrorMsgs;

        System.Timers.Timer logTimer;
        private bool disposedValue;

        public Logging_Repository(IOptionsSnapshot<Settings> config)
        {
            this._config = config;

            this.logTimer = new System.Timers.Timer(2000);
            this.logTimer.AutoReset = true;
            this.logTimer.Elapsed += async (sender, e) => await Task.WhenAll(
                [
                    PostMessages()
                    , PostErrorMessages()
                ]
            );
            this.logTimer.Start();
        }

        string GenerateLogType(string type) => " - " + type;

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

                string msg = "";

                List<string> msgs_copy = new List<string>(this.msgs);

                for (int i = 0; i < msgs_copy.Count; i++)
                {
                    string message = msgs_copy[i];

                    string newMsg = ((!String.IsNullOrEmpty(msg)) ? "\r\n" : "") + message;
                    if (msg.Length + newMsg.Length > 2000)
                    {
                        i = msgs_copy.Count;
                    }
                    else
                    {
                        msgs.Remove(message);
                        msg += newMsg;
                    }
                }

                if (msg.Length > 0)
                    await this.SendMessage("Event Logging", msg, this.settings.Admin.ChannelID);

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
                        if (this.settings.Admin.ServerID.HasValue)
                            guild = Program.client?.GetGuildAsync(this.settings.Admin.ServerID.Value)?.Result!;
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
        public async Task LogError(string Error, CommandContext Context = null!, Exception Exception = null!, List<string>? FormatFixes = null)
        {
            FormatFixes ??= [];
            FormatFixes = [.. FormatFixes, "### Main Error Information", "**Stack Trace**", "**Error**"];

            Log.Error(Exception, Error);
            string newline = "\r\n";
            string errormsg = "";

            if (Exception is not null)
                errormsg = ((Exception.InnerException is not null) ? newline + "### Inner Error Information" + newline + newline + Exception.InnerException.GetType().FullName + " - " + Exception.InnerException.Message + newline + newline + "**Stack Trace** - " + Exception.InnerException.StackTrace?.Trim().Substring(3) : "") + newline + "### Main Error Information" + newline + newline + Exception.GetType().FullName + " - " + Exception.Message + ((!String.IsNullOrEmpty(Exception.StackTrace)) ? newline + newline + "**Stack Trace** - " + Exception.StackTrace.Trim().Substring(3) : "");

            errormsg = newline + Error + newline + ((Context is not null) ? $"Server: {Context.Guild?.Name}, {Context.Guild?.Id}" + newline + $"User: {Context.User?.GlobalName}, {Context.User?.Id}" + newline : "") + errormsg;

            List<string> prefixFormat = [.. FormatFixes];

            new List<string>(["_", "*", "#", "-", "`", ">"])
                .ForEach(c =>
                {
                    errormsg = errormsg.Replace(c, $"\\{c}");
                    prefixFormat = [.. prefixFormat.Select(f => f.Replace(c, $"\\{c}"))];
                });

            for (int i = 0; i < prefixFormat.Count; i++)
                if (errormsg.Contains(prefixFormat[i]))
                    errormsg = errormsg.Replace(prefixFormat[i], FormatFixes[i]);

            errormsg = $"## <t:{((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds()}>\r\n" + errormsg;

            string msg = "";
            errormsg.Split(newline)
               .ToList()
               .ForEach(m =>
               {
                   if (msg.Length + m.Length + newline.Length > 2000)
                   {
                       errorMsgs.Add(msg);
                       msg = "";
                   }
                   msg += ((!String.IsNullOrEmpty(msg)) ? newline : "") + m;
               });

            if (!String.IsNullOrEmpty(msg))
                errorMsgs.Add(msg);
        }

        async Task PostErrorMessages()
        {
            if (!this.processingErrorMsgs)
            {
                this.processingErrorMsgs = true;

                if (errorMsgs.Count > 0)
                {
                    try
                    {
                        string msg1 = errorMsgs.First();
                        errorMsgs.Remove(msg1);

                        await this.SendMessage("Error Message", msg1, this.settings.Admin.ChannelID);
                        await Task.Delay(1000);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error posting error log message");
                    }
                }

                this.processingErrorMsgs = false;
            }
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
