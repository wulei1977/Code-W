[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [switch]$BuildIfMissing
)

$ErrorActionPreference = 'Stop'

function Resolve-VsixInstaller {
    $command = Get-Command VSIXInstaller.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $patterns = @(
        'C:\Program Files\Microsoft Visual Studio\2022\*\Common7\IDE\VSIXInstaller.exe',
        'C:\Program Files (x86)\Microsoft Visual Studio\2022\*\Common7\IDE\VSIXInstaller.exe'
    )

    $candidate = Get-ChildItem -Path $patterns -ErrorAction SilentlyContinue |
        Sort-Object FullName |
        Select-Object -First 1

    if ($candidate) {
        return $candidate.FullName
    }

    throw 'VSIXInstaller.exe was not found. Make sure Visual Studio 2022 is installed.'
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$vsixPath = Join-Path $repoRoot "src\CodeW\bin\$Configuration\net8.0-windows\CodeW.vsix"

if (-not (Test-Path -LiteralPath $vsixPath)) {
    if (-not $BuildIfMissing) {
        throw "VSIX package was not found: $vsixPath. Run tools\package-vsix.ps1 first or use -BuildIfMissing."
    }

    & (Join-Path $PSScriptRoot 'package-vsix.ps1') -Configuration $Configuration
}

$installerPath = Resolve-VsixInstaller

Write-Host 'Starting VSIX installer...'
Write-Host "Installer: $installerPath"
Write-Host "Package:   $vsixPath"

Start-Process -FilePath $installerPath -ArgumentList "`"$vsixPath`"" -Wait
