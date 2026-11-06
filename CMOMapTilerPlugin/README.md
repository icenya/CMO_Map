# CMOMapTilerPlugin - Command: Modern Operations (CMO) MapTiler 衛星外掛

本專案為 **Command: Modern Operations (CMO)** 開發之外掛 DLL，用於將遊戲主視窗 3D 地球內建的 **Bing Maps** 圖層替換為 **MapTiler Satellite Hybrid API**（高畫質衛星影像 + 道路地名混合圖磚），支援最高 **Zoom 18** 與 **512x512 Retina** 解析度。

---

## 專案特色

1. **零改動原程式二進位**：透過 .NET CLR 原生支援的 `AppDomainManager` 載入機制，無須修改 [Command.exe](file:///home/yue/cmo_map/program/Command.exe) 或 [shaders.hlsl](file:///home/yue/cmo_map/program/shaders.hlsl)。
2. **突破 `.NET Reactor (NecroBit)` 保護**：利用 `RuntimeHelpers.PrepareMethod` 強制觸發 JIT 編譯與 Reactor 解密，搭配 x64 Native Detour 攔截 `DXRenderer.TileCache::LoadBingTile` 與 `CSMaterial.HTTPHelper::DownloadTile`。
3. **無損坐標轉換**：原生支援 Bing `QuadKey` 與 Web Mercator `(Z, X, Y)` 雙向精準轉換。
4. **本機獨立磁碟快取**：圖磚快取獨立存放於 `Cache\MapTilerHybrid\{z}\{x}\{y}.jpg`，避免與遊戲舊快取混淆，提升二次載入速度並節省 API 流量。
5. **高度自訂設定檔**：提供 [MapTilerPlugin.ini](file:///home/yue/cmo_map/CMOMapTilerPlugin/MapTilerPlugin.ini)，可自由切換 512x512 / 256x256 解析度、設定 API Key，甚至替換為 Google Satellite、Esri 等標準 XYZ 圖磚來源。
6. **優雅回退機制**：若遇網路中斷或 API 配額耗盡，可自動回退至原始 Bing Maps 伺服器，保證遊戲不崩潰、不黑屏。

---

## 專案結構

- [CMOMapTilerPlugin.csproj](file:///home/yue/cmo_map/CMOMapTilerPlugin/CMOMapTilerPlugin.csproj): 專案檔（.NET Framework 4.8 / x64）。
- [PluginBootstrap.cs](file:///home/yue/cmo_map/CMOMapTilerPlugin/PluginBootstrap.cs): CLR `AppDomainManager` 入口點，支援自載入與通用注入器。
- [PluginMain.cs](file:///home/yue/cmo_map/CMOMapTilerPlugin/PluginMain.cs): 外掛主生命週期管理、組件載入監視與非同步偵測。
- [TileHook.cs](file:///home/yue/cmo_map/CMOMapTilerPlugin/TileHook.cs): `DXRenderer.TileCache::LoadBingTile` 與 `CSMaterial.HTTPHelper::DownloadTile` 攔截器。
- [NativeDetour.cs](file:///home/yue/cmo_map/CMOMapTilerPlugin/NativeDetour.cs): Windows x64 原生記憶體跳轉與 JIT Precode / Native 解析工具。
- [TileConverter.cs](file:///home/yue/cmo_map/CMOMapTilerPlugin/TileConverter.cs): Bing QuadKey $\leftrightarrow$ Web Mercator XYZ 高效能位元運算轉換器。
- [HttpDownloader.cs](file:///home/yue/cmo_map/CMOMapTilerPlugin/HttpDownloader.cs): TLS 1.2/1.3、GZip/Deflate 壓縮與非同步圖磚下載器。
- [PluginConfig.cs](file:///home/yue/cmo_map/CMOMapTilerPlugin/PluginConfig.cs): INI 設定檔讀取器與路徑管理。
- [Logger.cs](file:///home/yue/cmo_map/CMOMapTilerPlugin/Logger.cs): 執行期日誌寫入器（輸出至 `MapTilerPlugin.log`）。
- [MapTilerPlugin.ini](file:///home/yue/cmo_map/CMOMapTilerPlugin/MapTilerPlugin.ini): 外掛設定檔範本。
- [install.ps1](file:///home/yue/cmo_map/CMOMapTilerPlugin/install.ps1) / [install.bat](file:///home/yue/cmo_map/CMOMapTilerPlugin/install.bat): 一鍵自動安裝腳本。
- [uninstall.ps1](file:///home/yue/cmo_map/CMOMapTilerPlugin/uninstall.ps1) / [uninstall.bat](file:///home/yue/cmo_map/CMOMapTilerPlugin/uninstall.bat): 一鍵解除安裝腳本。

---

## 安裝步驟

### 方法一：一鍵安裝（推薦）
1. 將 [Release_MapTilerPlugin](file:///home/yue/cmo_map/Release_MapTilerPlugin) 目錄下的所有檔案複製至 CMO 遊戲目錄（包含 `Command.exe` 的目錄）。
2. 滑鼠雙擊執行 `install.bat`。

### 方法二：手動安裝
1. 將 `CMOMapTilerPlugin.dll` 與 `MapTilerPlugin.ini` 放入遊戲目錄。
2. 編輯遊戲目錄下的 `Command.exe.config`，在 `<runtime>` 節點中加入：
   ```xml
   <configuration>
     <runtime>
       <appDomainManagerAssembly value="CMOMapTilerPlugin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null" />
       <appDomainManagerType value="CMOMapTilerPlugin.PluginBootstrap" />
     </runtime>
   </configuration>
   ```

---

## 設定與使用

1. 前往 [MapTiler Cloud](https://cloud.maptiler.com/account/keys/) 免費註冊並取得 API Key。
2. 開啟遊戲目錄下的 `MapTilerPlugin.ini`，設定：
   ```ini
   ApiKey=您的MapTiler專屬Key
   ```
3. 啟動 CMO 遊戲，於主視窗圖層選單（**View $\rightarrow$ Layers**）勾選 **Bing Maps**。
4. 3D 地球即會自動套用 MapTiler Satellite Hybrid 高畫質衛星圖磚！
