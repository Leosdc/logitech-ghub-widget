@echo off
chcp 65001 >nul
title Instalador - Logitech G HUB Widget
cd /d "%~dp0"

echo ============================================================
echo   LOGITECH G HUB WIDGET - INSTALADOR WINDOWS 11
echo ============================================================
echo.

set "SCRIPT_PATH="
if exist "%~dp0installer\Instalar-Widget.ps1" (
    set "SCRIPT_PATH=%~dp0installer\Instalar-Widget.ps1"
) else if exist "%~dp0Instalar-Widget.ps1" (
    set "SCRIPT_PATH=%~dp0Instalar-Widget.ps1"
) else (
    echo [ERRO] O script 'Instalar-Widget.ps1' nao foi encontrado.
    echo Certifique-se de que todos os arquivos do pacote foram extraidos.
    echo.
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_PATH%"

if %errorlevel% neq 0 (
    echo.
    echo Ocorreu um erro durante a instalacao.
)
echo.
pause
