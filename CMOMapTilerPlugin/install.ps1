# CMOMapTilerPlugin 一鍵安裝腳本 (PowerShell)
# 作用: 將 CMOMapTilerPlugin 自動配置至 Command: Modern Operations
[CmdletBinding()]
param(
    [string]$TargetDir = ""
)

$ErrorActionPreference = "Stop"

# 確保輸出支援 UTF-8
try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    $OutputEncoding = [System.Text.Encoding]::UTF8
} catch {}

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "    CMOMapTilerPlugin - MapTiler 外掛自動安裝程式" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# 解析外掛所在目錄 (相容 PowerShell 2.0 ~ 7.x)
$scriptDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptDir) -and $MyInvocation.MyCommand.Path) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = (Get-Location).Path
}

# 若未指定 TargetDir，預設使用腳本所在目錄
if ([string]::IsNullOrWhiteSpace($TargetDir)) {
    $TargetDir = $scriptDir
}

# 去除結尾斜線
$TargetDir = $TargetDir.TrimEnd('\').TrimEnd('/')
$scriptDir = $scriptDir.TrimEnd('\').TrimEnd('/')

# 1. 檢查目錄中是否存在 Command.exe
$cmdExePath = Join-Path $TargetDir "Command.exe"
if (-not (Test-Path $cmdExePath)) {
    Write-Host "[提示] 在目前目錄找不到 Command.exe ($cmdExePath)" -ForegroundColor Yellow
    $userInput = Read-Host "請輸入 Command: Modern Operations 遊戲安裝目錄 (包含 Command.exe 的資料夾)"
    if ([string]::IsNullOrWhiteSpace($userInput)) {
        Write-Host "[錯誤] 未輸入目錄，安裝中止。" -ForegroundColor Red
        return
    }
    $userInput = $userInput.Trim().Trim('"').TrimEnd('\').TrimEnd('/')
    if (-not (Test-Path (Join-Path $userInput "Command.exe"))) {
        Write-Host "[錯誤] 指定目錄未找到 Command.exe，安裝中止。" -ForegroundColor Red
        return
    }
    $TargetDir = $userInput
    $cmdExePath = Join-Path $TargetDir "Command.exe"
}

Write-Host "[OK] 目標遊戲目錄: $TargetDir" -ForegroundColor Green

# 2. 複製外掛檔案 (若解壓在同目錄則跳過複製自身)
$srcDll = Join-Path $scriptDir "CMOMapTilerPlugin.dll"
if (-not (Test-Path $srcDll)) {
    $srcDll = Join-Path $scriptDir "bin\Release\net48\CMOMapTilerPlugin.dll"
}
$dstDll = Join-Path $TargetDir "CMOMapTilerPlugin.dll"

if ((Test-Path $srcDll) -and ($srcDll -ne $dstDll)) {
    Copy-Item -Path $srcDll -Destination $dstDll -Force
    Write-Host "[OK] 已複製 CMOMapTilerPlugin.dll 到遊戲目錄。" -ForegroundColor Green
} else {
    if (-not (Test-Path $dstDll)) {
        Write-Host "[錯誤] 找不到 CMOMapTilerPlugin.dll！" -ForegroundColor Red
        return
    }
    Write-Host "[OK] CMOMapTilerPlugin.dll 已就緒。" -ForegroundColor Green
}

$srcIni = Join-Path $scriptDir "MapTilerPlugin.ini"
$dstIni = Join-Path $TargetDir "MapTilerPlugin.ini"
if ((Test-Path $srcIni) -and ($srcIni -ne $dstIni) -and (-not (Test-Path $dstIni))) {
    Copy-Item -Path $srcIni -Destination $dstIni -Force
    Write-Host "[OK] 已複製 MapTilerPlugin.ini 到遊戲目錄。" -ForegroundColor Green
} elseif (Test-Path $dstIni) {
    Write-Host "[OK] MapTilerPlugin.ini 設定檔已存在。" -ForegroundColor Green
}

# 3. 配置 Command.exe.config
$configPath = Join-Path $TargetDir "Command.exe.config"
$backupPath = Join-Path $TargetDir "Command.exe.config.bak"

if (Test-Path $configPath) {
    if (-not (Test-Path $backupPath)) {
        Copy-Item -Path $configPath -Destination $backupPath -Force
        Write-Host "[OK] 已備份原始 Command.exe.config -> Command.exe.config.bak" -ForegroundColor Gray
    }

    [xml]$xml = Get-Content -Path $configPath -Raw -Encoding UTF8

    if ($null -eq $xml.configuration) {
        $xml = New-Object System.Xml.XmlDocument
        $root = $xml.CreateElement("configuration")
        $xml.AppendChild($root) | Out-Null
    }

    $runtime = $xml.configuration.runtime
    if ($null -eq $runtime) {
        $runtime = $xml.CreateElement("runtime")
        $xml.configuration.AppendChild($runtime) | Out-Null
    }

    # 檢查是否已存在設定
    $existingAsm = $runtime.SelectSingleNode("appDomainManagerAssembly")
    if ($null -eq $existingAsm) {
        $elemAsm = $xml.CreateElement("appDomainManagerAssembly")
        $elemAsm.SetAttribute("value", "CMOMapTilerPlugin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null")
        $runtime.AppendChild($elemAsm) | Out-Null
    } else {
        $existingAsm.SetAttribute("value", "CMOMapTilerPlugin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null")
    }

    $existingType = $runtime.SelectSingleNode("appDomainManagerType")
    if ($null -eq $existingType) {
        $elemType = $xml.CreateElement("appDomainManagerType")
        $elemType.SetAttribute("value", "CMOMapTilerPlugin.PluginBootstrap")
        $runtime.AppendChild($elemType) | Out-Null
    } else {
        $existingType.SetAttribute("value", "CMOMapTilerPlugin.PluginBootstrap")
    }

    $xml.Save($configPath)
    Write-Host "[OK] 已成功更新 Command.exe.config (注入 AppDomainManager)" -ForegroundColor Green
} else {
    # 建立全新 config
    $newConfigContent = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <startup>
    <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" />
  </startup>
  <runtime>
    <appDomainManagerAssembly value="CMOMapTilerPlugin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null" />
    <appDomainManagerType value="CMOMapTilerPlugin.PluginBootstrap" />
    <gcServer enabled="true" />
  </runtime>
</configuration>
"@
    Set-Content -Path $configPath -Value $newConfigContent -Encoding UTF8
    Write-Host "[OK] 已建立全新 Command.exe.config 並注入外掛設定。" -ForegroundColor Green
}

Write-Host "------------------------------------------------------------" -ForegroundColor Cyan
Write-Host "安裝完成！" -ForegroundColor Green
Write-Host "重要提醒:" -ForegroundColor Yellow
Write-Host "1. 請使用文字編輯器打開遊戲目錄下的 'MapTilerPlugin.ini'" -ForegroundColor Yellow
Write-Host "2. 將 ApiKey=YOUR_MAPTILER_API_KEY_HERE 替換為您的 MapTiler 專屬 Key" -ForegroundColor Yellow
Write-Host "3. 啟動遊戲後，於主視窗圖層勾選『Bing Maps』，即可享受 MapTiler 衛星混合高清圖磚！" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan
