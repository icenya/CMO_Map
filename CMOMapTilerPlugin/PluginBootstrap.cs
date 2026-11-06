using System;
using System.Security;

namespace CMOMapTilerPlugin
{
    /// <summary>
    /// Custom AppDomainManager that the .NET CLR instantiates automatically at process startup
    /// when configured in Command.exe.config.
    /// </summary>
    public class PluginBootstrap : AppDomainManager
    {
        static PluginBootstrap()
        {
            PluginMain.EnsureInitialized();
        }

        [SecuritySafeCritical]
        public override void InitializeNewDomain(AppDomainSetup appDomainInfo)
        {
            base.InitializeNewDomain(appDomainInfo);
            PluginMain.EnsureInitialized();
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
