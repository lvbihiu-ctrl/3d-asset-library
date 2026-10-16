@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0tools\扫描并打开.ps1"
if errorlevel 1 (
 echo.
 echo 扫描出现问题，请将上面的错误截图发给我。
 pause
)
