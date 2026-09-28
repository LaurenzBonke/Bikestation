@echo off
rem Doppelklick startet den Bikestation-Server
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0start-server.ps1"
pause
