@echo off
chcp 65001 >nul
title Gerador de Pacote - Logitech G HUB Widget
cd /d "%~dp0"

if exist "build\widget\*.pdb" del /f /q "build\widget\*.pdb"

echo Compactando pacote instalador em Logitech-GHub-Widget-Setup.zip...
powershell -NoProfile -Command "Compress-Archive -Path 'build\widget\*', 'installer\*', 'Instalar-Widget.bat', 'Instalar-Com-Interface.bat', 'Desinstalar-Widget.bat', 'README.md' -DestinationPath 'Logitech-GHub-Widget-Setup.zip' -Force"

if %errorlevel% equ 0 (
    echo.
    echo ============================================================
    echo   PACOTE ZIP GERADO COM SUCESSO: Logitech-GHub-Widget-Setup.zip
    echo ============================================================
    powershell -NoProfile -Command "Write-Host 'SHA256 do Pacote:' (Get-FileHash 'Logitech-GHub-Widget-Setup.zip' -Algorithm SHA256).Hash"
) else (
    echo Erro ao gerar pacote zip.
)
pause
