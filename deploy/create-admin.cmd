@echo off
rem Legt einen weiteren Admin an. Aufruf: create-admin.cmd <benutzername>
if "%~1"=="" (
  echo Aufruf: create-admin.cmd ^<benutzername^>
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -Command ". '%~dp0config.ps1'; Set-Location '%~dp0app'; dotnet bikestation.dll create-admin %1"
