[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$solutionPath = Join-Path $repoRoot 'Code-W.slnx'
$vsixPath = Join-Path $repoRoot "src\CodeW\bin\$Configuration\net8.0-windows\CodeW.vsix"

Write-Host "正在构建 Code-W ($Configuration)..."
& dotnet build $solutionPath -c $Configuration

if (-not (Test-Path -LiteralPath $vsixPath)) {
    throw "构建完成，但未找到 VSIX 包：$vsixPath"
}

Write-Host ""
Write-Host "VSIX 已生成："
Write-Host $vsixPath
