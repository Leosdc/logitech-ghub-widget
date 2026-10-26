@echo off
chcp 65001 >nul
title Compilador Multiplataforma - Logitech G HUB CLI
cd /d "%~dp0\.."

powershell -NoProfile -ExecutionPolicy Bypass -File "cli\build-cross-platform.ps1"

pause
