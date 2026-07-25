@echo off
setlocal
if not exist "%~dp0dist\3DPipes.scr" (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1"
    if errorlevel 1 pause & exit /b 1
)
start "3D Pipes" "%~dp0dist\3DPipes.scr" /s
