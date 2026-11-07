# CMOMapTilerPlugin 一鍵解除安裝腳本 (PowerShell)
[CmdletBinding()]
param(
    [string]$TargetDir = ""
)

$ErrorActionPreference = "Stop"

try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    $OutputEncoding = [System.Text.Encoding]::UTF8
} catch {}

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "    CMOMapTilerPlugin - MapTiler 外掛解除安裝程式" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# 解析外掛所在目錄
$scriptDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptDir) -and $MyInvocation.MyCommand.Path) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = (Get-Location).Path
}

if ([string]::IsNullOrWhiteSpace($TargetDir)) {
    $TargetDir = $scriptDir
}

$TargetDir = $TargetDir.TrimEnd('\').TrimEnd('/')
$scriptDir = $scriptDir.TrimEnd('\').TrimEnd('/')

$cmdExePath = Join-Path $TargetDir "Command.exe"
if (-not (Test-Path $cmdExePath)) {
    Write-Host "[提示] 在目前目錄找不到 Command.exe ($cmdExePath)" -ForegroundColor Yellow
    $userInput = Read-Host "請輸入 Command: Modern Operations 遊戲安裝目錄"
    if ([string]::IsNullOrWhiteSpace($userInput)) {
        Write-Host "[錯誤] 未輸入目錄，解除安裝中止。" -ForegroundColor Red
        return
    }
    $userInput = $userInput.Trim().Trim('"').TrimEnd('\').TrimEnd('/')
    if (-not (Test-Path (Join-Path $userInput "Command.exe"))) {
        Write-Host "[錯誤] 指定目錄未找到 Command.exe，解除安裝中止。" -ForegroundColor Red
        return
    }
    $TargetDir = $userInput
}

$configPath = Join-Path $TargetDir "Command.exe.config"
$backupPath = Join-Path $TargetDir "Command.exe.config.bak"
$hasLocalizer = Test-Path (Join-Path $TargetDir "CMOLocalizer.dll")

if ($hasLocalizer -and (Test-Path $configPath)) {
    # 若存在漢化模組，將 AppDomainManager 自動改回指向 CMOLocalizer 以保留漢化功能
    [xml]$xml = Get-Content -Path $configPath -Raw -Encoding UTF8
    $runtime = $xml.configuration.runtime
    if ($null -ne $runtime) {
        $nodeAsm = $runtime.SelectSingleNode("appDomainManagerAssembly")
        if ($null -ne $nodeAsm) { $nodeAsm.SetAttribute("value", "CMOLocalizer, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null") }

        $nodeType = $runtime.SelectSingleNode("appDomainManagerType")
        if ($null -ne $nodeType) { $nodeType.SetAttribute("value", "CMOLocalizer.LocalizerDomainManager") }

        $xml.Save($configPath)
        Write-Host "[OK] 偵測到 CMOLocalizer.dll，已將 Command.exe.config 還原為漢化模組配置。" -ForegroundColor Green
    }
} elseif (Test-Path $backupPath) {
    Copy-Item -Path $backupPath -Destination $configPath -Force
    Remove-Item -Path $backupPath -Force
    Write-Host "[OK] 已從備份還原原始 Command.exe.config。" -ForegroundColor Green
} elseif (Test-Path $configPath) {
    [xml]$xml = Get-Content -Path $configPath -Raw -Encoding UTF8
    $runtime = $xml.configuration.runtime
    if ($null -ne $runtime) {
        $nodeAsm = $runtime.SelectSingleNode("appDomainManagerAssembly")
        if ($null -ne $nodeAsm) { $runtime.RemoveChild($nodeAsm) | Out-Null }

        $nodeType = $runtime.SelectSingleNode("appDomainManagerType")
        if ($null -ne $nodeType) { $runtime.RemoveChild($nodeType) | Out-Null }

        $xml.Save($configPath)
        Write-Host "[OK] 已自 Command.exe.config 移除外掛設定。" -ForegroundColor Green
    }
}

Write-Host "解除安裝完成！遊戲已恢復原始狀態。" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Cyan
