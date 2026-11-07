using System;
using System.IO;
using System.Reflection;
using System.Security;

namespace CMOMapTilerPlugin
{
    /// <summary>
    /// Custom AppDomainManager that the .NET CLR instantiates automatically at process startup
    /// when configured in Command.exe.config.
    /// Supports automatic chaining with other mods (e.g. CMOLocalizer Chinese localization mod).
    /// </summary>
    public class PluginBootstrap : AppDomainManager
    {
        private static AppDomainManager _chainedLocalizer;

        static PluginBootstrap()
        {
            PluginMain.EnsureInitialized();
        }

        [SecuritySafeCritical]
        public override void InitializeNewDomain(AppDomainSetup appDomainInfo)
        {
            base.InitializeNewDomain(appDomainInfo);
            PluginMain.EnsureInitialized();

            // Automatic mod chaining: check if CMOLocalizer.dll is present
            TryChainLocalizer(appDomainInfo);
        }

        private static void TryChainLocalizer(AppDomainSetup appDomainInfo)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string localizerDll = Path.Combine(baseDir, "CMOLocalizer.dll");

                if (File.Exists(localizerDll))
                {
                    Logger.Info("Detected CMOLocalizer.dll in application directory. Initializing mod chaining...");
                    var asm = Assembly.LoadFrom(localizerDll);
                    var managerType = asm.GetType("CMOLocalizer.LocalizerDomainManager")
                                   ?? asm.GetType("CMOLocalizer.CustomAppDomainManager");

                    if (managerType != null)
                    {
                        _chainedLocalizer = (AppDomainManager)Activator.CreateInstance(managerType);
                        _chainedLocalizer.InitializeNewDomain(appDomainInfo);
                        Logger.Info("Successfully chained and activated CMOLocalizer Chinese localization engine!");
                    }
                    else
                    {
                        Logger.Warn("Found CMOLocalizer.dll but could not find LocalizerDomainManager or CustomAppDomainManager type.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception while chaining CMOLocalizer: {ex}");
            }
        }

        /// <summary>
        /// Public static entry point for generic DLL injectors / proxy DLLs (e.g. SharpCrafter, Doorstop, etc.)
        /// </summary>
        public static int Init(string args)
        {
            PluginMain.EnsureInitialized();
            return 0;
        }

        /// <summary>
        /// Parameterless entry point.
        /// </summary>
        public static void Initialize()
        {
            PluginMain.EnsureInitialized();
        }
    }
}
