using System;

namespace JSQViewer.Application.Abstractions
{
    public interface ILogger
    {
        void LogInfo(string message);

        void LogError(string message, Exception exception);
    }
}
