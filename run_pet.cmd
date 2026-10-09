@echo off
rem Double-click to build and start the desk pet. Quit: tray icon > right-click > quit.
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\run_pet.ps1" %*
if errorlevel 1 pause
