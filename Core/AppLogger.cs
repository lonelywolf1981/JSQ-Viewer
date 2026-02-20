using System;
using System.IO;
using System.Text;

namespace JSQViewer.Core
{
    public static class AppLogger
    {
        private static readonly object Sync = new object();

        public static void LogInfo(string rootFolder, string message)
        {
            Write(rootFolder, "INFO", message, null);
        }

        public static void LogError(string rootFolder, string message, Exception ex)
        {
            Write(rootFolder, "ERROR", message, ex);
        }

        private static void Write(string rootFolder, string level, string message, Exception ex)
        {
            try
            {
                string baseDir = string.IsNullOrWhiteSpace(rootFolder) ? AppDomain.CurrentDomain.BaseDirectory : rootFolder;
                string logDir = Path.Combine(baseDir, "log");
                Directory.CreateDirectory(logDir);
                string path = Path.Combine(logDir, "app.log");

                var sb = new StringBuilder();
                sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                sb.Append(" [");
                sb.Append(level);
                sb.Append("] ");
                sb.Append(message ?? string.Empty);
                if (ex != null)
                {
                    sb.Append(" | ");
                    sb.Append(ex.GetType().Name);
                    sb.Append(": ");
                    sb.Append(ex.Message);
                    if (!string.IsNullOrWhiteSpace(ex.StackTrace))
                    {
                        sb.AppendLine();
                        sb.Append(ex.StackTrace);
                    }
                }
                sb.AppendLine();

                lock (Sync)
                {
                    File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
                }
            }
            catch (Exception logEx)
            {
                System.Diagnostics.Debug.WriteLine("AppLogger.Write failed: " + logEx.Message);
            }
        }
    }
}
