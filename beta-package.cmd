@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0beta-package.ps1"
exit /b %ERRORLEVEL%
