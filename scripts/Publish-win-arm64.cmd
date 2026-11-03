@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Publish-win-arm64.ps1" %*
