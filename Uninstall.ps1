$ErrorActionPreference = 'Stop'

$InstallDirectory = Join-Path $env:LOCALAPPDATA '3D Pipes Screensaver'
$InstalledScreensaver = Join-Path $InstallDirectory '3DPipes.scr'
$DesktopRegistry = 'HKCU:\Control Panel\Desktop'

$current = (Get-ItemProperty -Path $DesktopRegistry -Name 'SCRNSAVE.EXE' -ErrorAction SilentlyContinue).'SCRNSAVE.EXE'
if ($current -and ([string]::Equals($current, $InstalledScreensaver, [System.StringComparison]::OrdinalIgnoreCase))) {
    Remove-ItemProperty -Path $DesktopRegistry -Name 'SCRNSAVE.EXE' -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $DesktopRegistry -Name 'ScreenSaveActive' -Value '0'
}

Remove-Item $InstallDirectory -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item 'HKCU:\Software\ThreeDPipesScreensaver' -Recurse -Force -ErrorAction SilentlyContinue
Start-Process -FilePath 'rundll32.exe' -ArgumentList 'user32.dll,UpdatePerUserSystemParameters' -WindowStyle Hidden

Write-Host '3D Pipes has been uninstalled for the current Windows user.'
