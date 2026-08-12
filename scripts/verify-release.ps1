param([string]$Configuration = "Release")

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$reportDirectory = Join-Path $root "test-results"
$reportPath = Join-Path $reportDirectory "self-test-report.txt"
$publishDirectory = Join-Path $root "artifacts\verify\win-x64"
New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null

dotnet build (Join-Path $root "CutPic.csproj") -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw "Release build failed." }

dotnet test (Join-Path $root "tests\CutPic.Tests\CutPic.Tests.csproj") -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw "Unit tests failed." }

$application = Join-Path $root "bin\$Configuration\net10.0-windows\CutPic.dll"
dotnet $application --self-test $reportPath
if ($LASTEXITCODE -ne 0) {
    if (Test-Path $reportPath) { Get-Content $reportPath }
    throw "CutPic self-test failed. Report: $reportPath"
}

Get-Content $reportPath

dotnet publish (Join-Path $root "CutPic.csproj") -c $Configuration -r win-x64 --self-contained false -o $publishDirectory
if ($LASTEXITCODE -ne 0) { throw "Release publish failed." }

$requiredFiles = @("CutPic.exe", "CutPic.dll", "CutPic.deps.json", "CutPic.runtimeconfig.json", "MaterialDesignThemes.Wpf.dll")
foreach ($file in $requiredFiles) {
    $path = Join-Path $publishDirectory $file
    if (!(Test-Path $path) -or (Get-Item $path).Length -eq 0) { throw "Published file missing or empty: $file" }
}

Write-Host "Verification passed."
Write-Host "Self-test report: $reportPath"
Write-Host "Verified publish: $publishDirectory"
