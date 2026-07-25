@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Uninstall.ps1"
if errorlevel 1 (
    echo.
    echo Uninstall failed. See the error above.
    pause
    exit /b 1
)
pause
