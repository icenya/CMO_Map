@echo off
chcp 65001 >nul
title CMOMapTilerPlugin 解除安裝程式
echo 正在執行 CMOMapTilerPlugin 解除安裝...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1"
echo.
pause
