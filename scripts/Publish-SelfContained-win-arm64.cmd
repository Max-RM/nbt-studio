@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Publish-SelfContained-win-arm64.ps1" %*
