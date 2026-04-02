[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Publisher,

    [Parameter(Mandatory = $true)]
    [string]$PersonalAccessToken,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$PublishManifestPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'marketplace\publishmanifest.json'),

    [switch]$BuildFirst
)

$ErrorActionPreference = 'Stop'

function Resolve-VsixPublisher {
    $command = Get-Command VsixPublisher.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $patterns = @(
        'C:\Program Files\Microsoft Visual Studio\2022\*\VSSDK\VisualStudioIntegration\Tools\Bin\VsixPublisher.exe',
        'C:\Program Files (x86)\Microsoft Visual Studio\2022\*\VSSDK\VisualStudioIntegration\Tools\Bin\VsixPublisher.exe'
    )

    $candidate = Get-ChildItem -Path $patterns -ErrorAction SilentlyContinue |
        Sort-Object FullName |
        Select-Object -First 1

    if ($candidate) {
        return $candidate.FullName
    }

    throw 'VsixPublisher.exe was not found. Make sure the Visual Studio 2022 extension development tools are installed.'
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$vsixPath = Join-Path $repoRoot "src\CodeW\bin\$Configuration\net8.0-windows\CodeW.vsix"

if ($BuildFirst -or -not (Test-Path -LiteralPath $vsixPath)) {
    & (Join-Path $PSScriptRoot 'package-vsix.ps1') -Configuration $Configuration
}

if (-not (Test-Path -LiteralPath $PublishManifestPath)) {
    throw "Publish manifest was not found: $PublishManifestPath"
}

$publishManifest = Get-Content -LiteralPath $PublishManifestPath -Raw | ConvertFrom-Json
if ($publishManifest.publisher -eq 'YOUR_PUBLISHER') {
    throw 'Please update marketplace\publishmanifest.json and replace YOUR_PUBLISHER with the real publisher name.'
}

if ($publishManifest.publisher -ne $Publisher) {
    throw "Publisher mismatch. CLI: $Publisher. Manifest: $($publishManifest.publisher)."
}

$publisherExe = Resolve-VsixPublisher

Write-Host 'Publishing Code-W to Visual Studio Marketplace...'
Write-Host "Publisher: $Publisher"
Write-Host "Package:   $vsixPath"
Write-Host "Manifest:  $PublishManifestPath"

$manifestDirectory = Split-Path -Path $PublishManifestPath -Parent
Push-Location $manifestDirectory
try {
    & $publisherExe publish `
        -payload $vsixPath `
        -publishManifest $PublishManifestPath `
        -personalAccessToken $PersonalAccessToken
}
finally {
    Pop-Location
}
