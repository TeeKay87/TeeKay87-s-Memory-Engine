@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Invoke-SourcePreflight.ps1" %*
exit /b %ERRORLEVEL%
