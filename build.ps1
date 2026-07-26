$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "src\AsusFanProfileSwitcher\AsusFanProfileSwitcher.csproj"
$smokeTests = Join-Path $PSScriptRoot "tests\ProfileCatalog.SmokeTests\ProfileCatalog.SmokeTests.csproj"
$output = Join-Path $PSScriptRoot "dist"

dotnet run --project $smokeTests --configuration Release

dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $output

Write-Host ""
Write-Host "Built: $output\AsusFanProfileSwitcher.exe"
