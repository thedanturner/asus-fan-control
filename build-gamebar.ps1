param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    throw "Visual Studio 2022 with the Universal Windows Platform development workload is required."
}

$msbuild = & $vswhere `
    -latest `
    -products * `
    -requires Microsoft.Component.MSBuild `
    -find "MSBuild\**\Bin\MSBuild.exe" |
    Select-Object -First 1
if (-not $msbuild) {
    throw "MSBuild was not found. Install Visual Studio 2022 and the UWP development workload."
}

$project = Join-Path $PSScriptRoot `
    "src\AsusFanProfileSwitcher.GameBar\AsusFanProfileSwitcher.GameBar.csproj"

& $msbuild $project `
    /restore `
    /target:Build `
    /property:Configuration=$Configuration `
    /property:Platform=x64 `
    /property:AppxPackageSigningEnabled=false

if ($LASTEXITCODE -ne 0) {
    throw "The Game Bar widget build failed."
}

$layout = Join-Path $PSScriptRoot `
    "src\AsusFanProfileSwitcher.GameBar\bin\x64\$Configuration\AppX"
Write-Host ""
Write-Host "Built Game Bar widget layout: $layout"
