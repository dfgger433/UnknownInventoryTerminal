@echo off
rem SPDX-License-Identifier: GPL-3.0-only
setlocal
set "PAUSE_ON_EXIT="
if "%~1"=="" set "PAUSE_ON_EXIT=1"
set "PACKAGE_POWERSHELL=powershell.exe"
where.exe pwsh.exe >nul 2>nul && set "PACKAGE_POWERSHELL=pwsh.exe"
"%PACKAGE_POWERSHELL%" -NoLogo -NoProfile -File "%~dp0Package.ps1" %*
set "PACKAGE_EXIT=%ERRORLEVEL%"
if not "%PACKAGE_EXIT%"=="0" echo Packaging failed ^(exit code %PACKAGE_EXIT%^).
if defined PAUSE_ON_EXIT pause
exit /b %PACKAGE_EXIT%
