using System;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace CMOMapTilerPlugin
{
    public static class HttpDownloader
    {
        private static HttpClient _httpClient;
        private static readonly object _initLock = new object();

        public static void Initialize(int timeoutSeconds, int maxConcurrentConnections)
        {
            if (_httpClient != null) return;

            lock (_initLock)
            {
                if (_httpClient != null) return;

                try
                {
                    // Enable TLS 1.2 and TLS 1.3 (0x3000 / 12288)
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | (SecurityProtocolType)12288;
                    ServicePointManager.DefaultConnectionLimit = Math.Max(maxConcurrentConnections, 16);
                    ServicePointManager.Expect100Continue = false;

                    var handler = new HttpClientHandler
                    {
                        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                        MaxConnectionsPerServer = maxConcurrentConnections
                    };

                    _httpClient = new HttpClient(handler)
                    {
                        Timeout = TimeSpan.FromSeconds(Math.Max(timeoutSeconds, 5))
                    };

                    _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) CMO-MapTilerPlugin/1.0");
                    Logger.Info("HttpDownloader initialized successfully.");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to initialize HttpDownloader: {ex}");
                }
            }
        }

        public static byte[] DownloadBytes(string url)
        {
            if (_httpClient == null)
            {
                Initialize(12, 16);
            }

            int retries = 2;
            for (int attempt = 1; attempt <= retries; attempt++)
            {
                try
                {
                    var response = _httpClient.GetAsync(url).GetAwaiter().GetResult();
                    if (response.IsSuccessStatusCode)
                    {
                        return response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    }

                    if ((int)response.StatusCode == 403 || (int)response.StatusCode == 401)
                    {
                        Logger.Error($"MapTiler API authorization failed (HTTP {(int)response.StatusCode}). Please check your ApiKey in MapTilerPlugin.ini!");
                        return null;
                    }

                    if (attempt < retries)
                    {
                        Thread.Sleep(150);
                    }
                }
                catch (Exception ex)
                {
                    if (attempt >= retries)
                    {
                        Logger.Warn($"Failed to download tile from '{url}' after {attempt} attempts: {ex.Message}");
                        return null;
                    }
                    Thread.Sleep(150);
                }
            }

            return null;
        }
    }
}
