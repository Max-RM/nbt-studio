@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Publish-SelfContained-win-x86.ps1" %*
