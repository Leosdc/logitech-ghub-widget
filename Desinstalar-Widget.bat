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
rmdir /s /q "C:\Users\PC\AppData\Local\Programs\LogitechGHubWidget" >nul 2>&1
rmdir /s /q "%LOCALAPPDATA%\LogitechGHubWidget" >nul 2>&1

echo.
echo ============================================================
echo   DESINSTALACAO CONCLUIDA COM SUCESSO!
echo ============================================================
pause
