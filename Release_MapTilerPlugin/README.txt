================================================================================
  CMOMapTilerPlugin - Command: Modern Operations (CMO) 高畫質衛星外掛
  版本: v1.0.0 (x64)
================================================================================

【功能說明】
本外掛透過 CLR 動態攔截技術，在不修改 Command.exe 原程式二進位檔案的前提下，
將 CMO 內建的「Bing Maps」衛星圖層無縫替換為 MapTiler 官方最新 Satellite Hybrid
(衛星影像 + 道路地名向量混合) 高清圖磚，支援高達 Zoom 18 的精細縮放層級與 512x512 Retina
高解析度貼圖！

【安裝方式】(二選一)

◆ 方法一：一鍵自動安裝 (推薦)
1. 將本資料夾內的所有檔案複製到 CMO 遊戲目錄 (包含 Command.exe 的資料夾)。
2. 滑鼠雙擊執行 `install.bat`。
3. 腳本會自動備份原始設定檔並完成配置。

◆ 方法二：手動安裝
1. 將 `CMOMapTilerPlugin.dll` 與 `MapTilerPlugin.ini` 複製至遊戲目錄。
2. 用文字編輯器開啟遊戲目錄下的 `Command.exe.config` (若無則直接使用本資料夾提供的)。
3. 確認 `<runtime>` 區塊內包含以下兩行設定：
   <configuration>
     <runtime>
       <appDomainManagerAssembly value="CMOMapTilerPlugin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null" />
       <appDomainManagerType value="CMOMapTilerPlugin.PluginBootstrap" />
     </runtime>
   </configuration>

【設定 MapTiler API Key】
1. 請至 https://cloud.maptiler.com/account/keys/ 免費註冊帳號並複製您的 API Key。
2. 以記事本開啟遊戲目錄下的 `MapTilerPlugin.ini`。
3. 將 `ApiKey=YOUR_MAPTILER_API_KEY_HERE` 替換為您的實際 Key，存檔即可。

【遊戲中啟用】
1. 啟動 CMO 遊戲。
2. 在主畫面或地球視窗中，打開圖層選單 (View -> Layers)。
3. 勾選「Bing Maps」。
4. 地球即會自動下載並顯示 MapTiler Satellite Hybrid 高清衛星圖磚！
   所有下載的圖磚會自動保存在 `Cache\MapTilerHybrid\` 資料夾中，
   後續開啟將直接自本機快取秒讀，不耗費額外網路配額。

【自訂與進階選項 (MapTilerPlugin.ini)】
- `UrlTemplate`:
  預設為 512x512 高清版：https://api.maptiler.com/maps/hybrid/{z}/{x}/{y}.jpg?key={key}
  若想使用 256x256 標準版：https://api.maptiler.com/maps/hybrid/256/{z}/{x}/{y}.jpg?key={key}
  亦可自訂為 Google Hybrid、Esri 等標準 Web Mercator (XYZ) 圖磚來源！
- `FallbackToBingOnError=true`: 當網路中斷或 API 配額超限時，自動切回原始 Bing 圖層。
- `EnableDiskCache=true`: 本機磁碟快取開關。

【解除安裝】
- 執行 `uninstall.bat` 即可一鍵還原原始設定，完全不留痕跡。
================================================================================
