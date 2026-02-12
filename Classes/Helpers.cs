using System.Data;
using DSharpPlus;
using DSharpPlus.Entities;

namespace ElmerBot
{
    public static class Helpers
    {
        public static ulong adminServerID = 1050765439808053348;
        private static ulong adminServerChannelID = 1050766754260979893;
        
        public static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            dynamic ex = e.ExceptionObject;
            Helpers.log_error("Generically catch error", ex);
        }
        public async static void log_error(string error, Exception ex = null, DSharpPlus.Entities.DiscordChannel channel = null)
        {
            string newline = "\r\n";
            string errormsg = "";

            if (ex != null)
                errormsg = ((ex.InnerException != null) ? newline + newline + "Inner Error Information\nBelow is the inner error code." + newline + newline + ex.InnerException.GetType().FullName + " - " + ex.InnerException.Message + newline + newline + "Stack Trace - " + ex.InnerException.StackTrace.Trim().Substring(3) : "") + newline + newline + " __Main Error Information__" + newline + "Below is the error code." + newline + newline + ex.GetType().FullName + " - " + ex.Message + ((!String.IsNullOrEmpty(ex.StackTrace)) ? newline + newline + "Stack Trace - " + ex.StackTrace.Trim().Substring(3) : "");

            errormsg = $"**{DateTime.Now.ToShortDateString()} {DateTime.Now.ToLongTimeString()}**" + newline + error + newline + errormsg;
            try
            {
                if (channel == null)
                    if (await Program.client?.GetGuildAsync(Helpers.adminServerID) is DSharpPlus.Entities.DiscordGuild adminGuild)
                        if (await Program.client.GetChannelAsync(Helpers.adminServerChannelID) is DSharpPlus.Entities.DiscordChannel errorChannel)
                            channel = errorChannel;
            }
            catch { }
            if (channel != null)
            {
                try
                {
                    bool isFirst = true;
                    errormsg.Split(newline)
                        .Where(s => !String.IsNullOrEmpty(s))
                        .ToList()
                        .ForEach(msg =>
                        {
                            string postMsg = ((isFirst) ? ":\r\n" + msg : msg);
                            isFirst = ((isFirst) ? false : isFirst);
                            channel.SendMessageAsync(msg).Wait();
                        });
                }
                catch (Exception e)
                {
                    string error2 = ((ex.InnerException != null) ? newline + newline + "Inner Error Information\nBelow is the inner error code." + newline + newline + ex.InnerException.GetType().FullName + " - " + ex.InnerException.Message + newline + newline + "Stack Trace - " + ex.InnerException.StackTrace.Trim().Substring(3) : "") + newline + newline + " Main Error Information" + newline + "Below is the error code." + newline + newline + ex.GetType().FullName + " - " + ex.Message + ((!String.IsNullOrEmpty(ex.StackTrace)) ? newline + newline + "Stack Trace - " + ex.StackTrace.Trim().Substring(3) : "");

                    error2 = $"{DateTime.Now.ToShortDateString()} {DateTime.Now.ToLongTimeString()}" + newline + error + newline + errormsg;
                    File.AppendAllText("ErrorLog.txt", newline + newline + newline + newline + "Error while logging error to error channel.\r\n\r\n" + error2 + newline + newline + newline + newline + errormsg);
                }
            }
            else
            {
                File.AppendAllText("ErrorLog.txt", newline + newline + newline + newline + errormsg);
            }
        }
    }
}
