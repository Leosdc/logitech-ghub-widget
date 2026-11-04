using System;
using System.IO;

namespace GHubWidget.Helpers
{
    public static class AppLogger
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LogitechGHubWidget",
            "runtime.log");

        private static readonly object LockObj = new();

        public static void Log(string message)
        {
            try
            {
                lock (LockObj)
                {
                    var dir = Path.GetDirectoryName(LogPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 512 * 1024)
                    {
                        File.Delete(LogPath);
                    }

                    var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                    File.AppendAllText(LogPath, line);
                }
            }
            catch { }
        }
    }
}

