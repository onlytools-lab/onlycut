param([string]$Configuration = "Release")

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$reportDirectory = Join-Path $root "test-results"
$reportPath = Join-Path $reportDirectory "self-test-report.txt"
$publishDirectory = Join-Path $root "artifacts\verify\win-x64"
New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
$previousData = $env:CUTPIC_DATA_DIR
try {
$env:CUTPIC_DATA_DIR = Join-Path $reportDirectory 'isolated-app-data'

dotnet build (Join-Path $root "CutPic.csproj") -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Release build failed." }

dotnet test (Join-Path $root "tests\CutPic.Tests\CutPic.Tests.csproj") -c $Configuration --logger "trx;LogFileName=unit-tests.trx" --results-directory $reportDirectory
if ($LASTEXITCODE -ne 0) { throw "Unit tests failed." }

$application = Join-Path $root "bin\$Configuration\net10.0-windows\CutPic.dll"
[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$expectedVersion = [string]$props.Project.PropertyGroup.Version + '.0'
if ([Reflection.AssemblyName]::GetAssemblyName($application).Version.ToString() -ne $expectedVersion -or
    (Get-Item $application).VersionInfo.FileVersion -ne $expectedVersion) { throw 'Built assembly/file version mismatch' }
dotnet $application --self-test $reportPath
if ($LASTEXITCODE -ne 0) {
    if (Test-Path $reportPath) { Get-Content $reportPath }
    throw "CutPic self-test failed. Report: $reportPath"
}

Get-Content $reportPath

dotnet $application --acceptance (Join-Path $reportDirectory 'acceptance-report.txt')
if ($LASTEXITCODE -ne 0) { throw 'Pipeline acceptance failed; see acceptance-report.txt' }
Get-Content (Join-Path $reportDirectory 'acceptance-report.txt')

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
} finally { $env:CUTPIC_DATA_DIR = $previousData }
