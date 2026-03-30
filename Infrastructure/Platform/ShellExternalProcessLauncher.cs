using System.Diagnostics;
using JSQViewer.Application.Abstractions;

namespace JSQViewer.Infrastructure.Platform
{
    public sealed class ShellExternalProcessLauncher : IExternalProcessLauncher
    {
        public void Open(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }
}
