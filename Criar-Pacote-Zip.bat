@echo off
chcp 65001 >nul
title Gerador de Pacote - Logitech G HUB Widget
cd /d "%~dp0"

echo ============================================================
echo   PREPARANDO E COMPACTANDO PACOTE DE INSTALACAO
echo ============================================================
echo.

if exist "build\widget\*.pdb" del /f /q "build\widget\*.pdb"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$pkg = 'build\package'; if (Test-Path $pkg) { Remove-Item $pkg -Recurse -Force }; New-Item -ItemType Directory -Path (Join-Path $pkg 'installer') -Force | Out-Null; New-Item -ItemType Directory -Path (Join-Path $pkg 'build\widget') -Force | Out-Null; Get-ChildItem -Path 'build\widget' -File | Where-Object { $_.Extension -ne '.pdb' } | ForEach-Object { Copy-Item $_.FullName -Destination (Join-Path $pkg 'build\widget') -Force; Copy-Item $_.FullName -Destination $pkg -Force }; Copy-Item 'installer\*' -Destination (Join-Path $pkg 'installer') -Force; Copy-Item 'installer\Instalar-Widget.ps1' -Destination $pkg -Force; Copy-Item 'installer\Instalar-GUI.ps1' -Destination $pkg -Force; Copy-Item 'Instalar-Widget.bat' -Destination $pkg -Force; Copy-Item 'Instalar-Com-Interface.bat' -Destination $pkg -Force; Copy-Item 'Desinstalar-Widget.bat' -Destination $pkg -Force; if (Test-Path 'README.md') { Copy-Item 'README.md' -Destination $pkg -Force }; if (Test-Path 'CHANGELOG.md') { Copy-Item 'CHANGELOG.md' -Destination $pkg -Force }; if (Test-Path 'SECURITY.md') { Copy-Item 'SECURITY.md' -Destination $pkg -Force }; if (Test-Path 'LICENSE') { Copy-Item 'LICENSE' -Destination $pkg -Force }; if (Test-Path 'Logitech-GHub-Widget-Setup.zip') { Remove-Item 'Logitech-GHub-Widget-Setup.zip' -Force }; Compress-Archive -Path (Join-Path $pkg '*') -DestinationPath 'Logitech-GHub-Widget-Setup.zip' -Force; Remove-Item $pkg -Recurse -Force"

if %errorlevel% equ 0 (
    echo.
    echo ============================================================
    echo   PACOTE ZIP GERADO COM SUCESSO: Logitech-GHub-Widget-Setup.zip
    echo ============================================================
    powershell -NoProfile -ExecutionPolicy Bypass -Command "$h = (certutil -hashfile 'Logitech-GHub-Widget-Setup.zip' SHA256)[1].Trim(); Write-Host 'SHA256 do Pacote:' $h; \"$h *Logitech-GHub-Widget-Setup.zip\" | Set-Content 'SHA256SUMS.txt' -Encoding ASCII"
) else (
    echo Erro ao gerar pacote zip.
)
pause
