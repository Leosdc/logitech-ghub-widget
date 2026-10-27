@echo off
chcp 65001 >nul
title Instalador - Logitech G HUB Widget
cd /d "%~dp0"

set "GUI_PATH="
if exist "%~dp0installer\Instalar-GUI.ps1" (
    set "GUI_PATH=%~dp0installer\Instalar-GUI.ps1"
) else if exist "%~dp0Instalar-GUI.ps1" (
    set "GUI_PATH=%~dp0Instalar-GUI.ps1"
) else (
    echo [ERRO] O script 'Instalar-GUI.ps1' nao foi encontrado.
    pause
    exit /b 1
)

start "" powershell -NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File "%GUI_PATH%"
