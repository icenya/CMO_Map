using System;
using System.IO;

namespace CMOMapTilerPlugin
{
    public static class Logger
    {
        private static readonly object _syncRoot = new object();
        private static string _logFilePath;
        public static bool EnableLogging { get; set; } = true;
        public static bool Verbose { get; set; } = true;

        static Logger()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                _logFilePath = Path.Combine(baseDir, "MapTilerPlugin.log");

                // Clear previous log on new startup if it gets too large (> 5MB)
                if (File.Exists(_logFilePath))
                {
                    var fi = new FileInfo(_logFilePath);
                    if (fi.Length > 5 * 1024 * 1024)
                    {
                        File.Delete(_logFilePath);
                    }
                }
            }
            catch
            {
                // Fallback to current working directory
                _logFilePath = "MapTilerPlugin.log";
            }
        }

        public static void Info(string message) => Log("INFO", message);
        public static void Warn(string message) => Log("WARN", message);
        public static void Error(string message) => Log("ERROR", message);
        public static void Debug(string message)
        {
            if (Verbose) Log("DEBUG", message);
        }

        private static void Log(string level, string message)
        {
            if (!EnableLogging) return;

            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";

            try
            {
                lock (_syncRoot)
                {
                    File.AppendAllText(_logFilePath, line + Environment.NewLine);
                }
            }
            catch
            {
                // Silently ignore logging failures to not disrupt host process
            }
        }
    }
}
