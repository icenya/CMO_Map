@echo off
setlocal
cd /d "%~dp0"
title CMOMapTilerPlugin Uninstaller
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1"
echo.
pause
