@echo off
rem Spielt die bereits gebaute Version aus Bikestation-Server\app-neu ein, ohne neu zu bauen
rem (Rechtsklick -> Als Administrator ausfuehren). Praktisch, wenn Smart App Control einen neuen Build sperrt
rem und schon ein startfaehiger Build in app-neu liegt.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0update-server.ps1" -SkipBuild %*
pause
