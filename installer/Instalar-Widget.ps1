# ==============================================================================
# Logitech G HUB Widget - Instalador do Windows 11
# ==============================================================================
# Este instalador NÃO altera nem modifica NENHUM arquivo da pasta oficial do G HUB.
# Ele instala o widget de forma totalmente isolada em %LOCALAPPDATA%\Programs\LogitechGHubWidget.
# ==============================================================================

[CmdletBinding()]
param(
    [switch]$Silent,
    [switch]$AutoStart
)

$ErrorActionPreference = "Stop"
$Host.UI.RawUI.WindowTitle = "Instalador - Logitech G HUB Widget para Windows 11"

Write-Host ""
Write-Host " ============================================================" -ForegroundColor Cyan
Write-Host "   LOGITECH G HUB WIDGET - INSTALADOR PARA WINDOWS 11" -ForegroundColor White
Write-Host " ============================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Verificar integridade do Logitech G HUB Oficial
$officialGHubPath = "C:\Program Files\LGHUB\lghub.exe"
if (Test-Path $officialGHubPath) {
    Write-Host " [OK] Logitech G HUB oficial detectado em: $officialGHubPath" -ForegroundColor Green
    Write-Host "      (Nenhum arquivo desta pasta sera modificado)" -ForegroundColor DarkGray
} else {
    Write-Host " [AVISO] Logitech G HUB oficial nao encontrado em $officialGHubPath." -ForegroundColor Yellow
    Write-Host "         O widget ainda funcionara se o G HUB estiver instalado em outro local." -ForegroundColor DarkGray
}

# 2. Definir pastas de origem e destino
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceDir = Join-Path (Split-Path -Parent $scriptDir) "build\widget"

if (-not (Test-Path $sourceDir)) {
    # Tenta usar pasta local se compilado no mesmo diretorio
    $sourceDir = $scriptDir
}

$installDir = Join-Path $env:LOCALAPPDATA "Programs\LogitechGHubWidget"
Write-Host ""
Write-Host " [*] Diretorio de instalacao: $installDir" -ForegroundColor Cyan

# 3. Encerrar processo se ja estiver rodando
$runningProc = Get-Process -Name "LogitechGHubWidget" -ErrorAction SilentlyContinue
if ($runningProc) {
    Write-Host " [*] Encerrando versao anterior em execucao..." -ForegroundColor Yellow
    $runningProc | Stop-Process -Force
    Start-Sleep -Seconds 1
}

# 4. Criar diretorio e copiar arquivos
Write-Host " [*] Copiando arquivos do widget..." -ForegroundColor Cyan
New-Item -ItemType Directory -Path $installDir -Force | Out-Null

Get-ChildItem -Path $sourceDir -File -Exclude "*.pdb" | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination $installDir -Force
    Write-Host "     + Copiado: $($_.Name)" -ForegroundColor DarkGray
}

$installedExe = Join-Path $installDir "LogitechGHubWidget.exe"
$iconPath = Join-Path $installDir "icon.ico"

# 5. Criar Atalho na Area de Trabalho
Write-Host " [*] Criando atalho na Area de Trabalho..." -ForegroundColor Cyan
$wsh = New-Object -ComObject WScript.Shell
$desktopShortcutPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "Logitech G HUB Widget.lnk"
$shortcut = $wsh.CreateShortcut($desktopShortcutPath)
$shortcut.TargetPath = $installedExe
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = "Widget de Bateria e Monitor de Dispositivos Logitech G HUB"
if (Test-Path $iconPath) {
    $shortcut.IconLocation = "$iconPath,0"
}
$shortcut.Save()
Write-Host " [OK] Atalho criado na Area de Trabalho!" -ForegroundColor Green

# 6. Criar Atalho no Menu Iniciar
Write-Host " [*] Criando atalho no Menu Iniciar..." -ForegroundColor Cyan
$startMenuDir = Join-Path ([Environment]::GetFolderPath("StartMenu")) "Programs"
$startShortcutPath = Join-Path $startMenuDir "Logitech G HUB Widget.lnk"
$startShortcut = $wsh.CreateShortcut($startShortcutPath)
$startShortcut.TargetPath = $installedExe
$startShortcut.WorkingDirectory = $installDir
$startShortcut.Description = "Widget de Bateria e Monitor de Dispositivos Logitech G HUB"
if (Test-Path $iconPath) {
    $startShortcut.IconLocation = "$iconPath,0"
}
$startShortcut.Save()
Write-Host " [OK] Atalho criado no Menu Iniciar!" -ForegroundColor Green

# 7. Criar script de desinstalacao limpa
Write-Host " [*] Criando desinstalador..." -ForegroundColor Cyan
$uninstallBatContent = @"
@echo off
chcp 65001 >nul
title Desinstalador - Logitech G HUB Widget
echo ============================================================
echo   DESINSTALAR LOGITECH G HUB WIDGET
echo ============================================================
echo.
echo Encerrando o widget se estiver aberto...
taskkill /f /im LogitechGHubWidget.exe >nul 2>&1

echo Removendo atalhos...
del /f /q "%USERPROFILE%\Desktop\Logitech G HUB Widget.lnk" >nul 2>&1
del /f /q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Logitech G HUB Widget.lnk" >nul 2>&1

echo Removendo inicializacao com o Windows...
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "LogitechGHubWidget" /f >nul 2>&1

echo Removendo arquivos do programa e configuracoes...
cd /d "%TEMP%"
rmdir /s /q "$installDir" >nul 2>&1
rmdir /s /q "%LOCALAPPDATA%\LogitechGHubWidget" >nul 2>&1

echo.
echo ============================================================
echo   DESINSTALACAO CONCLUIDA COM SUCESSO!
echo ============================================================
pause
"@

$uninstallBatPath = Join-Path $installDir "Desinstalar.bat"
$uninstallBatContent | Set-Content $uninstallBatPath -Encoding UTF8

# Tambem cria/atualiza o desinstalador nas pastas locais
$uninstallLauncher = Join-Path $scriptDir "Desinstalar-Widget.bat"
$uninstallBatContent | Set-Content $uninstallLauncher -Encoding UTF8

$rootUninstall = Join-Path (Split-Path -Parent $scriptDir) "Desinstalar-Widget.bat"
if (Test-Path $rootUninstall) {
    $uninstallBatContent | Set-Content $rootUninstall -Encoding UTF8
}

# 8. Perguntar ou configurar Inicializacao com o Windows
if ($AutoStart) {
    Write-Host " [*] Configurando inicializacao automatica com o Windows..." -ForegroundColor Cyan
    $regKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
    Set-ItemProperty -Path $regKey -Name "LogitechGHubWidget" -Value "`"$installedExe`""
    Write-Host " [OK] Inicializacao configurada!" -ForegroundColor Green
} elseif (-not $Silent) {
    Write-Host ""
    $resp = Read-Host "Deseja que o widget inicie automaticamente com o Windows? (S/N) [Padrao: S]"
    if ($resp -eq "" -or $resp -match "^[sSyY]") {
        $regKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
        Set-ItemProperty -Path $regKey -Name "LogitechGHubWidget" -Value "`"$installedExe`""
        Write-Host " [OK] Inicializacao com o Windows configurada!" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host " ============================================================" -ForegroundColor Green
Write-Host "   INSTALACAO CONCLUIDA COM SUCESSO!" -ForegroundColor White
Write-Host " ============================================================" -ForegroundColor Green
Write-Host " Arquivos instalados em: $installDir" -ForegroundColor DarkGray
Write-Host " Atalho criado na sua Area de Trabalho!" -ForegroundColor DarkGray
Write-Host ""

if (-not $Silent) {
    $openNow = Read-Host "Deseja iniciar o widget agora? (S/N) [Padrao: S]"
    if ($openNow -eq "" -or $openNow -match "^[sSyY]") {
        Start-Process -FilePath $installedExe
        Write-Host " [*] Widget iniciado!" -ForegroundColor Cyan
    }
}

