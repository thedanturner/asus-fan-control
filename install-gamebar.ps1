$ErrorActionPreference = "Stop"

& (Join-Path $PSScriptRoot "build-gamebar.ps1") -Configuration Debug

$manifest = Join-Path $PSScriptRoot `
    "src\AsusFanProfileSwitcher.GameBar\bin\x64\Debug\AppX\AppxManifest.xml"
if (-not (Test-Path $manifest)) {
    $manifest = Get-ChildItem `
        (Join-Path $PSScriptRoot "src\AsusFanProfileSwitcher.GameBar\bin\x64\Debug") `
        -Filter AppxManifest.xml `
        -Recurse |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not $manifest) {
    throw "The built AppxManifest.xml could not be found."
}

Add-AppxPackage -Register $manifest

Write-Host ""
Write-Host "Installed the ASUS Fan Profiles widget."
Write-Host "Start the desktop controller as administrator, press Win+G, then open Widget Menu."
