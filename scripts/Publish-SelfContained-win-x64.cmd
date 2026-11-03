@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Publish-SelfContained-win-x64.ps1" %*
