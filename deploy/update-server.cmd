@echo off
rem Aktualisiert den Bikestation-Server (Rechtsklick -> Als Administrator ausfuehren)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0update-server.ps1" %*
pause
