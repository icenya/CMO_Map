using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace CMOMapTilerPlugin
{
    public static class PluginMain
    {
        private static int _initialized = 0;
        public static PluginConfig Config { get; private set; }

        public static void EnsureInitialized()
        {
            if (Interlocked.Exchange(ref _initialized, 1) != 0)
                return;

            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string iniPath = Path.Combine(baseDir, "MapTilerPlugin.ini");

                // 1. Load configuration
                Config = PluginConfig.Load(iniPath);
                Logger.EnableLogging = Config.EnableLogging;
                Logger.Verbose = Config.VerboseLogging;

                Logger.Info("==================================================================");
                Logger.Info("  CMOMapTilerPlugin v1.0.1 (x64) Initialized");
                Logger.Info($"  Base Directory: {baseDir}");
                Logger.Info($"  Enabled: {Config.Enabled}");
                Logger.Info($"  API Key: {(string.IsNullOrEmpty(Config.ApiKey) || Config.ApiKey.Contains("YOUR_MAPTILER_API_KEY") ? "[NOT SET - PLEASE CONFIGURE IN MapTilerPlugin.ini]" : "[CONFIGURED]")}");
                Logger.Info($"  URL Template: {Config.UrlTemplate}");
                Logger.Info($"  Disk Cache: {Config.EnableDiskCache} -> {Config.GetFullCacheDirectory()}");
                Logger.Info("==================================================================");

                // 2. Initialize HTTP Downloader
                HttpDownloader.Initialize(Config.TimeoutSeconds, Config.MaxConcurrentDownloads);

                // 3. Try to hook immediately if Command assembly is already loaded
                if (!TryHookLoadedAssemblies())
                {
                    // Listen to assembly load events
                    AppDomain.CurrentDomain.AssemblyLoad += (sender, args) =>
                    {
                        if (IsCommandAssembly(args.LoadedAssembly))
                        {
                            Logger.Info($"AssemblyLoad event detected Command assembly: {args.LoadedAssembly.FullName}");
                            TileHook.Install(args.LoadedAssembly);
                        }
                    };

                    // Also spawn a short-lived watchdog thread to check periodically for up to 30 seconds
                    var watchdog = new Thread(WatchdogLoop)
                    {
                        IsBackground = true,
                        Name = "CMOMapTilerWatchdog"
                    };
                    watchdog.Start();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Critical initialization exception in PluginMain: {ex}");
            }
        }

        private static bool IsCommandAssembly(Assembly asm)
        {
            if (asm == null) return false;
            try
            {
                string name = asm.GetName().Name;
                return string.Equals(name, "Command", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryHookLoadedAssemblies()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (IsCommandAssembly(asm))
                {
                    Logger.Info($"Found pre-loaded Command assembly: {asm.FullName}");
                    TileHook.Install(asm);
                    return true;
                }
            }
            return false;
        }

        private static void WatchdogLoop()
        {
            for (int i = 0; i < 60; i++)
            {
                if (TileHook.IsInstalled) break;

                Thread.Sleep(500);

                if (TryHookLoadedAssemblies())
                {
                    break;
                }
            }
        }
    }
}
