@echo off
rem Drag the original "Plain Craft Launcher 2.exe" onto this file, or double-click and follow the prompt.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0patch.ps1" %1
echo.
pause
