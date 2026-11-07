@echo off
setlocal
cd /d "%~dp0"
title CMOMapTilerPlugin Installer
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
echo.
pause
