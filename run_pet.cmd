@echo off
rem 雙擊啟動桌寵（會先建置）。結束：系統匣圖示右鍵 →「結束」
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\run_pet.ps1" %*
if errorlevel 1 pause
