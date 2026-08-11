param([string]$Configuration = "Release")

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$reportDirectory = Join-Path $root "test-results"
$reportPath = Join-Path $reportDirectory "self-test-report.txt"
New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null

dotnet build (Join-Path $root "CutPic.csproj") -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw "Release build failed." }

$application = Join-Path $root "bin\$Configuration\net10.0-windows\CutPic.dll"
dotnet $application --self-test $reportPath
if ($LASTEXITCODE -ne 0) {
    if (Test-Path $reportPath) { Get-Content $reportPath }
    throw "CutPic self-test failed. Report: $reportPath"
}

Get-Content $reportPath
Write-Host "Verification passed: $reportPath"
