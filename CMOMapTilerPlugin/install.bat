@echo off
chcp 65001 >nul
title CMOMapTilerPlugin 安裝程式
echo 正在執行 CMOMapTilerPlugin 自動安裝...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
echo.
pause
