using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using JSQViewer.Application.Abstractions;

namespace JSQViewer.Infrastructure.Platform
{
    public sealed class FileSystemLogger : ILogger
    {
        private static readonly object Sync = new object();
        private readonly IFileSystem _fileSystem;
        private readonly IAppPaths _appPaths;

        public FileSystemLogger(IFileSystem fileSystem, IAppPaths appPaths)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _appPaths = appPaths ?? throw new ArgumentNullException(nameof(appPaths));
        }

        public void LogInfo(string message)
        {
            Write("INFO", message, null);
        }

        public void LogError(string message, Exception exception)
        {
            Write("ERROR", message, exception);
        }

        private void Write(string level, string message, Exception exception)
        {
            try
            {
                _fileSystem.CreateDirectory(_appPaths.LogDirectory);
                string logPath = Path.Combine(_appPaths.LogDirectory, "app.log");

                var builder = new StringBuilder();
                builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                builder.Append(" [");
                builder.Append(level);
                builder.Append("] ");
                builder.Append(message ?? string.Empty);
                if (exception != null)
                {
                    builder.Append(" | ");
                    builder.Append(exception.GetType().Name);
                    builder.Append(": ");
                    builder.Append(exception.Message);
                    if (!string.IsNullOrWhiteSpace(exception.StackTrace))
                    {
                        builder.AppendLine();
                        builder.Append(exception.StackTrace);
                    }
                }
                builder.AppendLine();

                lock (Sync)
                {
                    _fileSystem.AppendAllText(logPath, builder.ToString(), Encoding.UTF8);
                }
            }
            catch (Exception logException)
            {
                Debug.WriteLine("FileSystemLogger.Write failed: " + logException.Message);
            }
        }
    }
}
