using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace CMOMapTilerPlugin
{
    public static class TileHook
    {
        private static readonly List<NativeDetour> _activeDetours = new List<NativeDetour>();
        private static readonly object _hookLock = new object();
        private static bool _isHookInstalled = false;

        private static readonly Regex _virtualEarthRegex = new Regex(@"/tiles/a([0-3]+)\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool IsInstalled => _isHookInstalled;

        public static void Install(Assembly commandAssembly)
        {
            if (_isHookInstalled) return;

            lock (_hookLock)
            {
                if (_isHookInstalled) return;

                try
                {
                    Logger.Info($"Searching for target types in assembly '{commandAssembly.FullName}'...");

                    // 1. Locate DXRenderer.TileCache
                    Type tileCacheType = commandAssembly.GetType("DXRenderer.TileCache", false);
                    if (tileCacheType != null)
                    {
                        MethodInfo loadBingMethod = tileCacheType.GetMethod("LoadBingTile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (loadBingMethod != null)
                        {
                            MethodInfo hookMethod = typeof(TileHook).GetMethod(nameof(Hook_LoadBingTile), BindingFlags.Static | BindingFlags.Public);
                            var detour = NativeDetour.Create(loadBingMethod, hookMethod);
                            _activeDetours.Add(detour);
                            Logger.Info("Successfully hooked 'DXRenderer.TileCache::LoadBingTile'!");
                        }
                        else
                        {
                            Logger.Warn("Method 'DXRenderer.TileCache::LoadBingTile' not found!");
                        }
                    }
                    else
                    {
                        Logger.Warn("Type 'DXRenderer.TileCache' not found in assembly!");
                    }

                    // 2. Locate CSMaterial.HTTPHelper
                    Type httpHelperType = commandAssembly.GetType("CSMaterial.HTTPHelper", false);
                    if (httpHelperType != null)
                    {
                        MethodInfo downloadTileMethod = httpHelperType.GetMethod("DownloadTile", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        if (downloadTileMethod != null)
                        {
                            MethodInfo hookMethod = typeof(TileHook).GetMethod(nameof(Hook_DownloadTile), BindingFlags.Static | BindingFlags.Public);
                            var detour = NativeDetour.Create(downloadTileMethod, hookMethod);
                            _activeDetours.Add(detour);
                            Logger.Info("Successfully hooked 'CSMaterial.HTTPHelper::DownloadTile'!");
                        }
                        else
                        {
                            Logger.Warn("Method 'CSMaterial.HTTPHelper::DownloadTile' not found!");
                        }
                    }
                    else
                    {
                        Logger.Warn("Type 'CSMaterial.HTTPHelper' not found in assembly!");
                    }

                    _isHookInstalled = _activeDetours.Count > 0;
                    if (_isHookInstalled)
                    {
                        Logger.Info($"TileHook installation finished. Installed {_activeDetours.Count} detours.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Exception during TileHook installation: {ex}");
                }
            }
        }

        /// <summary>
        /// Hook replacement for instance method:
        /// byte[] DXRenderer.TileCache::LoadBingTile(string id)
        /// In x64: RCX = tileCacheInstance, RDX = id (quadKey)
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static byte[] Hook_LoadBingTile(object tileCacheInstance, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            var config = PluginMain.Config;
            if (config == null || !config.Enabled)
            {
                return FallbackToBing(id);
            }

            try
            {
                // 1. Convert Bing QuadKey to Web Mercator XYZ coordinates
                TileConverter.QuadKeyToTileXY(id, out int x, out int y, out int z);

                // 2. Check local disk cache
                string cacheFile = null;
                if (config.EnableDiskCache)
                {
                    string cacheRoot = config.GetFullCacheDirectory();
                    cacheFile = Path.Combine(cacheRoot, z.ToString(), x.ToString(), $"{y}.jpg");

                    if (File.Exists(cacheFile))
                    {
                        try
                        {
                            byte[] cachedBytes = File.ReadAllBytes(cacheFile);
                            if (cachedBytes != null && cachedBytes.Length > 0)
                            {
                                Logger.Debug($"Disk cache hit: {z}/{x}/{y} (QuadKey: {id})");
                                return cachedBytes;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn($"Failed to read cache file '{cacheFile}': {ex.Message}");
                        }
                    }
                }

                // 3. Format URL and download from MapTiler
                string url = config.FormatUrl(z, x, y, id);
                byte[] downloadedBytes = HttpDownloader.DownloadBytes(url);

                if (downloadedBytes != null && downloadedBytes.Length > 0)
                {
                    Logger.Debug($"MapTiler tile downloaded: {z}/{x}/{y} ({downloadedBytes.Length} bytes)");

                    // Save to local disk cache
                    if (config.EnableDiskCache && cacheFile != null)
                    {
                        try
                        {
                            string dir = Path.GetDirectoryName(cacheFile);
                            if (!Directory.Exists(dir))
                            {
                                Directory.CreateDirectory(dir);
                            }
                            File.WriteAllBytes(cacheFile, downloadedBytes);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn($"Failed to write cache file '{cacheFile}': {ex.Message}");
                        }
                    }

                    return downloadedBytes;
                }

                // 4. Download failed; fallback if enabled
                if (config.FallbackToBingOnError)
                {
                    Logger.Warn($"MapTiler download failed for {z}/{x}/{y}; falling back to Bing.");
                    return FallbackToBing(id);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in Hook_LoadBingTile for QuadKey '{id}': {ex}");
                if (config != null && config.FallbackToBingOnError)
                {
                    return FallbackToBing(id);
                }
            }

            return null;
        }

        /// <summary>
        /// Hook replacement for static method:
        /// byte[] CSMaterial.HTTPHelper::DownloadTile(string url)
        /// In x64: RCX = url
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static byte[] Hook_DownloadTile(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;

            try
            {
                // Check if this is a Bing tile request: http://ecn.t{0..3}.tiles.virtualearth.net/tiles/a{quadkey}.jpeg
                var match = _virtualEarthRegex.Match(url);
                if (match.Success)
                {
                    string quadKey = match.Groups[1].Value;
                    return Hook_LoadBingTile(null, quadKey);
                }

                // Non-Bing tile: download directly via HttpDownloader
                return HttpDownloader.DownloadBytes(url);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in Hook_DownloadTile for url '{url}': {ex}");
                return HttpDownloader.DownloadBytes(url);
            }
        }

        private static byte[] FallbackToBing(string quadKey)
        {
            if (string.IsNullOrEmpty(quadKey)) return null;

            try
            {
                char subServer = quadKey[quadKey.Length - 1];
                string bingUrl = $"http://ecn.t{subServer}.tiles.virtualearth.net/tiles/a{quadKey}.jpeg?g=0&dir=dir_n";
                return HttpDownloader.DownloadBytes(bingUrl);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Bing fallback failed for '{quadKey}': {ex.Message}");
                return null;
            }
        }
    }
}
