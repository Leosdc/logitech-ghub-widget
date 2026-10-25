@echo off
chcp 65001 >nul
title Instalador - Logitech G HUB Widget
cd /d "%~dp0"

start "" powershell -NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File "%~dp0installer\Instalar-GUI.ps1"
