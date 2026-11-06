using System;
using System.IO;
using System.Text;

namespace CMOMapTilerPlugin
{
    public class PluginConfig
    {
        public bool Enabled { get; set; } = true;
        public string ApiKey { get; set; } = "";
        public string UrlTemplate { get; set; } = "https://api.maptiler.com/maps/hybrid/{z}/{x}/{y}.jpg?key={key}";
        public bool EnableDiskCache { get; set; } = true;
        public string CacheDirectory { get; set; } = "Cache\\MapTilerHybrid";
        public bool FallbackToBingOnError { get; set; } = true;
        public bool EnableLogging { get; set; } = true;
        public bool VerboseLogging { get; set; } = false;
        public int TimeoutSeconds { get; set; } = 12;
        public int MaxConcurrentDownloads { get; set; } = 16;

        public string GetFullCacheDirectory()
        {
            if (Path.IsPathRooted(CacheDirectory))
            {
                return CacheDirectory;
            }
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CacheDirectory);
        }

        public string FormatUrl(int z, int x, int y, string quadKey)
        {
            string url = UrlTemplate
                .Replace("{z}", z.ToString())
                .Replace("{x}", x.ToString())
                .Replace("{y}", y.ToString())
                .Replace("{key}", ApiKey)
                .Replace("{quadkey}", quadKey);
            return url;
        }

        public static PluginConfig Load(string path)
        {
            var config = new PluginConfig();

            if (!File.Exists(path))
            {
                config.SaveDefault(path);
                return config;
            }

            try
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                string currentSection = "";

                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#"))
                        continue;

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }

                    int equalsIndex = line.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        string key = line.Substring(0, equalsIndex).Trim();
                        string val = line.Substring(equalsIndex + 1).Trim();

                        switch (key.ToLowerInvariant())
                        {
                            case "enabled":
                                config.Enabled = ParseBool(val, true);
                                break;
                            case "apikey":
                                config.ApiKey = val;
                                break;
                            case "urltemplate":
                                config.UrlTemplate = val;
                                break;
                            case "enablediskcache":
                                config.EnableDiskCache = ParseBool(val, true);
                                break;
                            case "cachedirectory":
                                config.CacheDirectory = val;
                                break;
                            case "fallbacktobingonerror":
                                config.FallbackToBingOnError = ParseBool(val, true);
                                break;
                            case "enablelogging":
                                config.EnableLogging = ParseBool(val, true);
                                break;
                            case "verboselogging":
                                config.VerboseLogging = ParseBool(val, false);
                                break;
                            case "timeoutseconds":
                                if (int.TryParse(val, out int timeout)) config.TimeoutSeconds = timeout;
                                break;
                            case "maxconcurrentdownloads":
                                if (int.TryParse(val, out int maxDls)) config.MaxConcurrentDownloads = maxDls;
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to parse config file '{path}': {ex.Message}");
            }

            return config;
        }

        private static bool ParseBool(string val, bool defaultValue)
        {
            if (string.Equals(val, "true", StringComparison.OrdinalIgnoreCase) || val == "1") return true;
            if (string.Equals(val, "false", StringComparison.OrdinalIgnoreCase) || val == "0") return false;
            return defaultValue;
        }

        public void SaveDefault(string path)
        {
            try
            {
                string defaultContent =
@"; =====================================================================
; CMOMapTilerPlugin Configuration File
; Command: Modern Operations - MapTiler Satellite Hybrid Tile Plugin
; =====================================================================

[MapTiler]
; 是否啟用外掛攔截 Bing Maps 衛星圖層 (true / false)
Enabled=true

; 您的 MapTiler API Key (請至 https://cloud.maptiler.com/account/keys/ 免費註冊取得)
ApiKey=YOUR_MAPTILER_API_KEY_HERE

; 圖磚下載 URL 模板:
; 支援變數: {z}=縮放層級, {x}=X座標, {y}=Y座標, {key}=ApiKey, {quadkey}=Bing網格字串
; 1) MapTiler Satellite Hybrid (512x512 高解析度 - 推薦):
;    https://api.maptiler.com/maps/hybrid/{z}/{x}/{y}.jpg?key={key}
; 2) MapTiler Satellite Hybrid (256x256 標準解析度):
;    https://api.maptiler.com/maps/hybrid/256/{z}/{x}/{y}.jpg?key={key}
; 3) 自訂 XYZ 伺服器 (如 Google Hybrid / Esri 等):
;    https://mt1.google.com/vt/lyrs=y&x={x}&y={y}&z={z}
UrlTemplate=https://api.maptiler.com/maps/hybrid/{z}/{x}/{y}.jpg?key={key}

; 是否啟用本機磁碟快取 (大幅降低 API 請求配額並顯著提高載入速度)
EnableDiskCache=true

; 快取資料夾路徑 (相對路徑或絕對路徑，例如 Cache\MapTilerHybrid)
CacheDirectory=Cache\MapTilerHybrid

; 當 MapTiler 圖磚下載失敗或 API 配額超限時，是否自動回退至原始 Bing Maps 圖層
FallbackToBingOnError=true

; 網路連線逾時秒數 (預設 12 秒)
TimeoutSeconds=12

; 最大同時連線下載數 (預設 16)
MaxConcurrentDownloads=16

[Logging]
; 是否啟用日誌記錄 (寫入 MapTilerPlugin.log)
EnableLogging=true

; 是否輸出詳細除錯日誌 (每一張圖磚的快取命中與下載狀態)
VerboseLogging=false
";
                File.WriteAllText(path, defaultContent, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to create default config file at '{path}': {ex.Message}");
            }
        }
    }
}
