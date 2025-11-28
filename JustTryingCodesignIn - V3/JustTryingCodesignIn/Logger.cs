using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JustTryingCodesignIn
{
    public static class Logger
    {
        private static readonly string LogFilePath =
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CodesignApp.log");

        public static void Log(string message)
        {
            try
            {
                var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                System.IO.File.AppendAllText(LogFilePath, logLine + Environment.NewLine);
            }
            catch
            {
                // Swallow logging errors to avoid crashes.
            }
        }

        public static void LogError(Exception ex, string context = "")
        {
            Log($"{context} ERROR: {ex.GetType().Name} - {ex.Message} | Stack: {ex.StackTrace}");
        }
    }
}
