param([string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root "artifacts\publish\win-x64-self-contained"
$output = Join-Path $root "artifacts\installer"
New-Item -ItemType Directory -Force -Path $publish, $output | Out-Null
& (Join-Path $root "scripts\verify-release.ps1") -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { throw "Release verification failed." }
dotnet publish (Join-Path $root "CutPic.csproj") -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=false -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }
wix build (Join-Path $PSScriptRoot "Package.wxs") -arch x64 -d "RootDir=$root" -o (Join-Path $output "CutPic-Setup-x64.msi")
if ($LASTEXITCODE -ne 0) { throw "WiX build failed." }
Write-Host "Installer: $(Join-Path $output 'CutPic-Setup-x64.msi')"
