@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0smoke.ps1"
exit /b %ERRORLEVEL%
