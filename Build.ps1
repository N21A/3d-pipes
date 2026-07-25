$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Sources = @(
    (Join-Path $Root 'src\ThreeDPipes.cs'),
    (Join-Path $Root 'src\PipeWorld.cs'),
    (Join-Path $Root 'src\OpenGlRenderer.cs')
)
$Manifest = Join-Path $Root 'app.manifest'
$Icon = Join-Path $Root 'assets\3dpipes.ico'
$Dist = Join-Path $Root 'dist'
$Exe = Join-Path $Dist '3DPipes.exe'
$Scr = Join-Path $Dist '3DPipes.scr'

$candidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)

$csc = $null
foreach ($candidate in $candidates) {
    if (Test-Path $candidate) {
        $csc = $candidate
        break
    }
}

if (-not $csc) {
    throw 'The built-in .NET Framework C# compiler was not found. Enable .NET Framework 4.x in Windows Features, then run this script again.'
}

foreach ($source in $Sources) {
    if (-not (Test-Path $source)) {
        throw "Required source file was not found: $source"
    }
}

New-Item -ItemType Directory -Path $Dist -Force | Out-Null
Remove-Item $Exe, $Scr -Force -ErrorAction SilentlyContinue

$compilerArguments = @(
    '/nologo',
    '/target:winexe',
    '/optimize+',
    '/platform:anycpu',
    "/out:$Exe",
    "/win32manifest:$Manifest",
    "/win32icon:$Icon",
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll'
) + $Sources

Write-Host 'Building 3D Pipes screensaver...'
& $csc $compilerArguments
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE."
}

Copy-Item $Exe $Scr -Force
Write-Host "Built successfully: $Scr"
