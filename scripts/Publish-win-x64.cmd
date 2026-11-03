@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Publish-win-x64.ps1" %*
