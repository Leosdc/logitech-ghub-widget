Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

[System.Windows.Forms.Application]::EnableVisualStyles()

$installDirDefault = Join-Path $env:LOCALAPPDATA "Programs\LogitechGHubWidget"
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }

$candidateDirs = @(
    (Join-Path (Split-Path -Parent $scriptDir) "build\widget"),
    $scriptDir,
    (Split-Path -Parent $scriptDir),
    (Join-Path $scriptDir "build\widget"),
    (Join-Path (Split-Path -Parent $scriptDir) "widget")
)

$sourceDir = $null
foreach ($dir in $candidateDirs) {
    if ($dir -and (Test-Path (Join-Path $dir "LogitechGHubWidget.exe"))) {
        $sourceDir = (Resolve-Path $dir).Path
        break
    }
}

# Form Setup
$form = New-Object System.Windows.Forms.Form
$form.Text = "Instalador - Logitech G HUB Widget (Windows 11)"
$form.Size = New-Object System.Drawing.Size(530, 500)
$form.StartPosition = "CenterScreen"
$form.FormBorderStyle = "FixedDialog"
$form.MaximizeBox = $false
$form.MinimizeBox = $true
$form.BackColor = [System.Drawing.Color]::FromArgb(24, 25, 34)
$form.ForeColor = [System.Drawing.Color]::White

# Icon if present
if ($sourceDir) {
    $iconSrc = Join-Path $sourceDir "icon.ico"
    if (Test-Path $iconSrc) {
        try { $form.Icon = New-Object System.Drawing.Icon($iconSrc) } catch {}
    }
}

# Header Panel
$header = New-Object System.Windows.Forms.Panel
$header.Size = New-Object System.Drawing.Size(530, 85)
$header.BackColor = [System.Drawing.Color]::FromArgb(18, 19, 26)
$header.Dock = "Top"
$form.Controls.Add($header)

$lblLogo = New-Object System.Windows.Forms.Label
$lblLogo.Text = "G"
$lblLogo.Font = New-Object System.Drawing.Font("Segoe UI", 24, [System.Drawing.FontStyle]::Bold)
$lblLogo.ForeColor = [System.Drawing.Color]::FromArgb(0, 229, 255)
$lblLogo.Location = New-Object System.Drawing.Point(24, 18)
$lblLogo.Size = New-Object System.Drawing.Size(40, 45)
$header.Controls.Add($lblLogo)

$lblTitle = New-Object System.Windows.Forms.Label
$lblTitle.Text = "Logitech G HUB Widget"
$lblTitle.Font = New-Object System.Drawing.Font("Segoe UI", 14, [System.Drawing.FontStyle]::Bold)
$lblTitle.ForeColor = [System.Drawing.Color]::White
$lblTitle.Location = New-Object System.Drawing.Point(68, 16)
$lblTitle.Size = New-Object System.Drawing.Size(350, 28)
$header.Controls.Add($lblTitle)

$lblSubtitle = New-Object System.Windows.Forms.Label
$lblSubtitle.Text = "Monitor de bateria elegante e nao invasivo para Windows 11"
$lblSubtitle.Font = New-Object System.Drawing.Font("Segoe UI", 9)
$lblSubtitle.ForeColor = [System.Drawing.Color]::FromArgb(140, 150, 170)
$lblSubtitle.Location = New-Object System.Drawing.Point(70, 44)
$lblSubtitle.Size = New-Object System.Drawing.Size(420, 20)
$header.Controls.Add($lblSubtitle)

# Group: Path
$grpPath = New-Object System.Windows.Forms.GroupBox
$grpPath.Text = " Pasta de Instalacao "
$grpPath.Location = New-Object System.Drawing.Point(24, 100)
$grpPath.Size = New-Object System.Drawing.Size(465, 75)
$grpPath.ForeColor = [System.Drawing.Color]::FromArgb(180, 190, 210)
$form.Controls.Add($grpPath)

$txtDest = New-Object System.Windows.Forms.TextBox
$txtDest.Text = $installDirDefault
$txtDest.Location = New-Object System.Drawing.Point(16, 30)
$txtDest.Size = New-Object System.Drawing.Size(330, 24)
$txtDest.BackColor = [System.Drawing.Color]::FromArgb(32, 34, 46)
$txtDest.ForeColor = [System.Drawing.Color]::White
$txtDest.BorderStyle = "FixedSingle"
$grpPath.Controls.Add($txtDest)

$btnBrowse = New-Object System.Windows.Forms.Button
$btnBrowse.Text = "Procurar..."
$btnBrowse.Location = New-Object System.Drawing.Point(355, 28)
$btnBrowse.Size = New-Object System.Drawing.Size(95, 27)
$btnBrowse.BackColor = [System.Drawing.Color]::FromArgb(45, 48, 64)
$btnBrowse.ForeColor = [System.Drawing.Color]::White
$btnBrowse.FlatStyle = "Flat"
$btnBrowse.FlatAppearance.BorderSize = 0
$btnBrowse.Cursor = [System.Windows.Forms.Cursors]::Hand
$btnBrowse.Add_Click({
    $fbd = New-Object System.Windows.Forms.FolderBrowserDialog
    $fbd.Description = "Selecione a pasta onde deseja instalar o widget"
    $fbd.SelectedPath = $txtDest.Text
    if ($fbd.ShowDialog($form) -eq [System.Windows.Forms.DialogResult]::OK) {
        $txtDest.Text = $fbd.SelectedPath
    }
})
$grpPath.Controls.Add($btnBrowse)

# Group: Options
$grpOptions = New-Object System.Windows.Forms.GroupBox
$grpOptions.Text = " Opcoes de Instalacao "
$grpOptions.Location = New-Object System.Drawing.Point(24, 185)
$grpOptions.Size = New-Object System.Drawing.Size(465, 125)
$grpOptions.ForeColor = [System.Drawing.Color]::FromArgb(180, 190, 210)
$form.Controls.Add($grpOptions)

$chkDesktop = New-Object System.Windows.Forms.CheckBox
$chkDesktop.Text = "Criar atalho na Area de Trabalho"
$chkDesktop.Checked = $true
$chkDesktop.Location = New-Object System.Drawing.Point(20, 28)
$chkDesktop.Size = New-Object System.Drawing.Size(380, 22)
$chkDesktop.ForeColor = [System.Drawing.Color]::White
$grpOptions.Controls.Add($chkDesktop)

$chkStartMenu = New-Object System.Windows.Forms.CheckBox
$chkStartMenu.Text = "Criar atalho no Menu Iniciar"
$chkStartMenu.Checked = $true
$chkStartMenu.Location = New-Object System.Drawing.Point(20, 52)
$chkStartMenu.Size = New-Object System.Drawing.Size(380, 22)
$chkStartMenu.ForeColor = [System.Drawing.Color]::White
$grpOptions.Controls.Add($chkStartMenu)

$chkAutoStart = New-Object System.Windows.Forms.CheckBox
$chkAutoStart.Text = "Iniciar automaticamente com o Windows"
$chkAutoStart.Checked = $true
$chkAutoStart.Location = New-Object System.Drawing.Point(20, 76)
$chkAutoStart.Size = New-Object System.Drawing.Size(380, 22)
$chkAutoStart.ForeColor = [System.Drawing.Color]::White
$grpOptions.Controls.Add($chkAutoStart)

$chkLaunchNow = New-Object System.Windows.Forms.CheckBox
$chkLaunchNow.Text = "Iniciar o widget apos concluir a instalacao"
$chkLaunchNow.Checked = $true
$chkLaunchNow.Location = New-Object System.Drawing.Point(20, 100)
$chkLaunchNow.Size = New-Object System.Drawing.Size(380, 22)
$chkLaunchNow.ForeColor = [System.Drawing.Color]::White
$grpOptions.Controls.Add($chkLaunchNow)

# Progress Bar
$progressBar = New-Object System.Windows.Forms.ProgressBar
$progressBar.Location = New-Object System.Drawing.Point(24, 320)
$progressBar.Size = New-Object System.Drawing.Size(465, 18)
$progressBar.Style = "Continuous"
$form.Controls.Add($progressBar)

$lblStatus = New-Object System.Windows.Forms.Label
$lblStatus.Text = "Pronto para instalar."
$lblStatus.Font = New-Object System.Drawing.Font("Segoe UI", 8.5)
$lblStatus.ForeColor = [System.Drawing.Color]::FromArgb(150, 160, 180)
$lblStatus.Location = New-Object System.Drawing.Point(24, 342)
$lblStatus.Size = New-Object System.Drawing.Size(465, 20)
$form.Controls.Add($lblStatus)

# Bottom Buttons
$btnInstall = New-Object System.Windows.Forms.Button
$btnInstall.Text = "Instalar Widget"
$btnInstall.Size = New-Object System.Drawing.Size(140, 38)
$btnInstall.Location = New-Object System.Drawing.Point(215, 380)
$btnInstall.BackColor = [System.Drawing.Color]::FromArgb(0, 184, 252)
$btnInstall.ForeColor = [System.Drawing.Color]::Black
$btnInstall.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)
$btnInstall.FlatStyle = "Flat"
$btnInstall.FlatAppearance.BorderSize = 0
$btnInstall.Cursor = [System.Windows.Forms.Cursors]::Hand
$form.Controls.Add($btnInstall)

$btnCancel = New-Object System.Windows.Forms.Button
$btnCancel.Text = "Cancelar"
$btnCancel.Size = New-Object System.Drawing.Size(110, 38)
$btnCancel.Location = New-Object System.Drawing.Point(379, 380)
$btnCancel.BackColor = [System.Drawing.Color]::FromArgb(40, 42, 56)
$btnCancel.ForeColor = [System.Drawing.Color]::White
$btnCancel.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)
$btnCancel.FlatStyle = "Flat"
$btnCancel.FlatAppearance.BorderSize = 0
$btnCancel.Cursor = [System.Windows.Forms.Cursors]::Hand
$btnCancel.Add_Click({ $form.Close() })
$form.Controls.Add($btnCancel)

# Installation Logic
$btnInstall.Add_Click({
    $btnInstall.Enabled = $false
    $txtDest.Enabled = $false
    $targetDir = $txtDest.Text.Trim()

    # Validação do caminho de instalação
    if ([string]::IsNullOrWhiteSpace($targetDir)) {
        [System.Windows.Forms.MessageBox]::Show($form, "Por favor, informe uma pasta de instalacao valida.", "Caminho Invalido", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning)
        $btnInstall.Enabled = $true
        $txtDest.Enabled = $true
        return
    }

    try {
        if (-not [System.IO.Path]::IsPathRooted($targetDir)) {
            [System.Windows.Forms.MessageBox]::Show($form, "O caminho informado deve ser absoluto (ex: C:\Caminho\Para\Pasta).", "Caminho Invalido", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning)
            $btnInstall.Enabled = $true
            $txtDest.Enabled = $true
            return
        }

        $root = [System.IO.Path]::GetPathRoot($targetDir)
        if ($root.TrimEnd('\', '/') -eq $targetDir.TrimEnd('\', '/')) {
            [System.Windows.Forms.MessageBox]::Show($form, "Por seguranca, nao e permitido instalar diretamente na raiz do disco ($targetDir). Selecione uma subpasta.", "Caminho Invalido", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning)
            $btnInstall.Enabled = $true
            $txtDest.Enabled = $true
            return
        }

        $winDir = [Environment]::GetFolderPath("Windows")
        if ($targetDir.StartsWith($winDir, [System.StringComparison]::OrdinalIgnoreCase)) {
            [System.Windows.Forms.MessageBox]::Show($form, "Nao e permitido instalar dentro das pastas do sistema Windows.", "Caminho Invalido", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning)
            $btnInstall.Enabled = $true
            $txtDest.Enabled = $true
            return
        }
    } catch {
        [System.Windows.Forms.MessageBox]::Show($form, "O caminho informado contem caracteres invalidos.", "Caminho Invalido", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning)
        $btnInstall.Enabled = $true
        $txtDest.Enabled = $true
        return
    }

    if (-not $sourceDir -or -not (Test-Path (Join-Path $sourceDir "LogitechGHubWidget.exe"))) {
        [System.Windows.Forms.MessageBox]::Show($form, "Nao foi possivel localizar 'LogitechGHubWidget.exe'. Verifique se todos os arquivos foram extraidos.", "Erro de Instalacao", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
        $btnInstall.Enabled = $true
        $txtDest.Enabled = $true
        return
    }

    $lblStatus.Text = "Verificando processos e preparando diretorio..."
    $progressBar.Value = 20
    $form.Refresh()

    # Close existing widget if running
    Get-Process -Name "LogitechGHubWidget" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500

    $progressBar.Value = 40
    $lblStatus.Text = "Copiando arquivos do widget..."
    $form.Refresh()

    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    $filesToCopy = Get-ChildItem -Path $sourceDir -File | Where-Object { 
        $_.Extension -ne ".pdb" -and $_.Extension -ne ".bat" -and $_.Extension -ne ".ps1" -and $_.Extension -ne ".md"
    }

    if (-not $filesToCopy -or $filesToCopy.Count -eq 0) {
        [System.Windows.Forms.MessageBox]::Show($form, "Nenhum arquivo para instalacao foi encontrado em: $sourceDir", "Erro de Instalacao", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
        $btnInstall.Enabled = $true
        $txtDest.Enabled = $true
        return
    }

    foreach ($file in $filesToCopy) {
        Copy-Item -Path $file.FullName -Destination $targetDir -Force
    }

    $exePath = Join-Path $targetDir "LogitechGHubWidget.exe"
    if (-not (Test-Path $exePath)) {
        [System.Windows.Forms.MessageBox]::Show($form, "O executavel '$exePath' nao foi encontrado apos a copia.", "Erro de Instalacao", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
        $btnInstall.Enabled = $true
        $txtDest.Enabled = $true
        return
    }

    # Gerar script de desinstalacao limpa associado a pasta escolhida
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
rmdir /s /q "$targetDir" >nul 2>&1
rmdir /s /q "%LOCALAPPDATA%\LogitechGHubWidget" >nul 2>&1

echo.
echo ============================================================
echo   DESINSTALACAO CONCLUIDA COM SUCESSO!
echo ============================================================
pause
"@

    $uninstallBatPath = Join-Path $targetDir "Desinstalar.bat"
    $uninstallBatContent | Set-Content $uninstallBatPath -Encoding UTF8

    # Tambem atualiza o Desinstalar-Widget.bat na pasta do instalador caso exista
    $uninstallLauncher = Join-Path $scriptDir "Desinstalar-Widget.bat"
    if (Test-Path $uninstallLauncher) {
        $uninstallBatContent | Set-Content $uninstallLauncher -Encoding UTF8
    }
    $rootUninstall = Join-Path (Split-Path -Parent $scriptDir) "Desinstalar-Widget.bat"
    if (Test-Path $rootUninstall) {
        $uninstallBatContent | Set-Content $rootUninstall -Encoding UTF8
    }

    $progressBar.Value = 70
    $lblStatus.Text = "Criando atalhos e configuracoes..."
    $form.Refresh()

    $iconPath = Join-Path $targetDir "icon.ico"
    $wsh = New-Object -ComObject WScript.Shell

    if ($chkDesktop.Checked) {
        $desktopPath = [Environment]::GetFolderPath("Desktop")
        $scDesktop = $wsh.CreateShortcut((Join-Path $desktopPath "Logitech G HUB Widget.lnk"))
        $scDesktop.TargetPath = $exePath
        $scDesktop.WorkingDirectory = $targetDir
        if (Test-Path $iconPath) { $scDesktop.IconLocation = "$iconPath,0" }
        $scDesktop.Save()
    }

    if ($chkStartMenu.Checked) {
        $smDir = Join-Path ([Environment]::GetFolderPath("StartMenu")) "Programs"
        $scStart = $wsh.CreateShortcut((Join-Path $smDir "Logitech G HUB Widget.lnk"))
        $scStart.TargetPath = $exePath
        $scStart.WorkingDirectory = $targetDir
        if (Test-Path $iconPath) { $scStart.IconLocation = "$iconPath,0" }
        $scStart.Save()
    }

    if ($chkAutoStart.Checked) {
        Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "LogitechGHubWidget" -Value "`"$exePath`""
    }

    $progressBar.Value = 100
    $lblStatus.Text = "Instalacao concluida com sucesso!"
    $lblStatus.ForeColor = [System.Drawing.Color]::FromArgb(16, 185, 129)
    $form.Refresh()

    if ($chkLaunchNow.Checked) {
        if (Test-Path $exePath) {
            Start-Process -FilePath $exePath
        }
    }

    [System.Windows.Forms.MessageBox]::Show($form, "O Logitech G HUB Widget foi instalado com sucesso!`n`nNenhum arquivo oficial foi modificado.", "Instalacao Concluida", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
    $form.Close()
})

$form.ShowDialog() | Out-Null
