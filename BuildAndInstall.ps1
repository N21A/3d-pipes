$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $Root 'Build.ps1')

$BuiltScreensaver = Join-Path $Root 'dist\3DPipes.scr'
$InstallDirectory = Join-Path $env:LOCALAPPDATA '3D Pipes Screensaver'
$InstalledScreensaver = Join-Path $InstallDirectory '3DPipes.scr'

New-Item -ItemType Directory -Path $InstallDirectory -Force | Out-Null
Copy-Item $BuiltScreensaver $InstalledScreensaver -Force

$DesktopRegistry = 'HKCU:\Control Panel\Desktop'
Set-ItemProperty -Path $DesktopRegistry -Name 'SCRNSAVE.EXE' -Value $InstalledScreensaver
Set-ItemProperty -Path $DesktopRegistry -Name 'ScreenSaveActive' -Value '1'

Start-Process -FilePath 'rundll32.exe' -ArgumentList 'user32.dll,UpdatePerUserSystemParameters' -WindowStyle Hidden

Write-Host ''
Write-Host '3D Pipes is installed for the current Windows user.'
Write-Host "Installed file: $InstalledScreensaver"
Write-Host 'Opening Windows Screen Saver Settings...'

Start-Process -FilePath 'control.exe' -ArgumentList 'desk.cpl,,1'
