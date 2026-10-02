@echo off
rem Double-click to build the Vice installer. The work is done by Publish.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Publish.ps1" %*
pause
