@echo off
setlocal
pwsh -NoProfile -File "%~dp0kamuit.ps1" %*
exit /b %ERRORLEVEL%
