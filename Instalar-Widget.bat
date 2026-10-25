@echo off
chcp 65001 >nul
title Instalador - Logitech G HUB Widget
cd /d "%~dp0"

echo ============================================================
echo   LOGITECH G HUB WIDGET - INSTALADOR WINDOWS 11
echo ============================================================
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0installer\Instalar-Widget.ps1"

if %errorlevel% neq 0 (
    echo.
    echo Ocorreu um erro durante a instalacao.
)
echo.
pause
