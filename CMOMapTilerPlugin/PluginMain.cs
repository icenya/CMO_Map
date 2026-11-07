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
                string iniPath = ResolveAndMigrateConfigPath(baseDir);

                // 1. Load configuration
                Config = PluginConfig.Load(iniPath);
                Logger.EnableLogging = Config.EnableLogging;
                Logger.Verbose = Config.VerboseLogging;

                Logger.Info("==================================================================");
                Logger.Info("  CMOMapTilerPlugin v1.0.2 (x64) Initialized");
                Logger.Info($"  Base Directory: {baseDir}");
                Logger.Info($"  Config File: {iniPath}");
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

        private static string ResolveAndMigrateConfigPath(string baseDir)
        {
            string rootIni = Path.Combine(baseDir, "MapTilerPlugin.ini");
            string configSubdirIni = Path.Combine(baseDir, "Config", "MapTilerPlugin.ini");

            try
            {
                // Ensure root ini is not read-only so it can be read and updated freely
                if (File.Exists(rootIni))
                {
                    try
                    {
                        FileAttributes attrs = File.GetAttributes(rootIni);
                        if ((attrs & FileAttributes.ReadOnly) != 0)
                        {
                            File.SetAttributes(rootIni, attrs & ~FileAttributes.ReadOnly);
                        }
                    }
                    catch { }
                }

                // Check if user placed MapTilerPlugin.ini inside the Config/ subdirectory
                // (which triggers CMO's Command.Client.ConsolidateConfigFiles() to delete it or crash if read-only)
                if (File.Exists(configSubdirIni))
                {
                    try
                    {
                        // 1. Remove Read-Only flag immediately in case user marked it read-only
                        FileAttributes attrs = File.GetAttributes(configSubdirIni);
                        if ((attrs & FileAttributes.ReadOnly) != 0)
                        {
                            File.SetAttributes(configSubdirIni, attrs & ~FileAttributes.ReadOnly);
                        }

                        // 2. Inspect if config in Config/ has a configured API key
                        bool rootNeedsConfig = !File.Exists(rootIni);
                        if (!rootNeedsConfig)
                        {
                            var existingConfig = PluginConfig.Load(rootIni);
                            if (string.IsNullOrEmpty(existingConfig.ApiKey) || existingConfig.ApiKey.Contains("YOUR_MAPTILER_API_KEY"))
                            {
                                rootNeedsConfig = true;
                            }
                        }

                        // 3. Migrate to root if needed
                        if (rootNeedsConfig)
                        {
                            File.Copy(configSubdirIni, rootIni, true);
                            Logger.Info($"Auto-migrated config from '{configSubdirIni}' to root '{rootIni}'.");
                        }

                        // 4. Safely delete from Config/ so CMO ConsolidateConfigFiles() will not encounter it
                        File.Delete(configSubdirIni);
                        Logger.Info($"Removed '{configSubdirIni}' from CMO Config directory to prevent startup deletion/crash conflicts.");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Failed to migrate/cleanup config in Config folder: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in ResolveAndMigrateConfigPath: {ex.Message}");
            }

            return rootIni;
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
            if (TileHook.IsInstalled) return true;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (IsCommandAssembly(asm))
                {
                    Logger.Info($"Found pre-loaded Command assembly: {asm.FullName}");
                    TileHook.Install(asm);
                    return TileHook.IsInstalled;
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
