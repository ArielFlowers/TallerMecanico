@echo off
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\entorno.ps1" %*
exit /b %ERRORLEVEL%
