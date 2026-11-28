param([string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root 'artifacts\candidate'
$publish = Join-Path $output 'payload'
# Only this explicitly scoped, generated candidate directory is cleaned.
$expected = [IO.Path]::GetFullPath((Join-Path $root 'artifacts\candidate'))
if ([IO.Path]::GetFullPath($output) -ne $expected) { throw 'Invalid candidate output path' }
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }
New-Item -ItemType Directory -Force -Path $publish | Out-Null
[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = [string]$props.Project.PropertyGroup.Version
python (Join-Path $root 'scripts/check_product_version.py') --version $version
if ($LASTEXITCODE -ne 0) { throw 'Version validation failed' }
& (Join-Path $root "scripts\verify-release.ps1") -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { throw "Release verification failed." }
dotnet publish (Join-Path $root "CutPic.csproj") -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=false -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }
wix build (Join-Path $PSScriptRoot "Package.wxs") -arch x64 -d "RootDir=$root" -d "PublishDir=$publish" -d "ProductVersion=$version" -o (Join-Path $output "CutPic-Setup-x64.msi")
if ($LASTEXITCODE -ne 0) { throw "WiX build failed." }
$msi = Join-Path $output 'CutPic-Setup-x64.msi'
$metadata = & (Join-Path $root 'scripts/inspect-msi.ps1') -Path $msi
if ($metadata.Properties.ProductVersion -ne $version -or
    $metadata.Properties.UpgradeCode -ne '{6E02C1AA-9A9D-4CB1-8F6F-37AD338F316A}' -or
    $metadata.Properties.ProductName -ne 'CutPic' -or
    $metadata.SummaryTemplate -notmatch '^x64;' -or
    !$metadata.Properties.ProductCode -or !$metadata.Components.Count -or !$metadata.Upgrades.Count) { throw 'MSI metadata validation failed' }
if ($metadata.Properties.ALLUSERS -ne '1' -or
    !@($metadata.LaunchConditions | Where-Object { $_[0] -match 'WIX_DOWNGRADE_DETECTED' }).Count) { throw 'MSI scope/downgrade guard mismatch' }
$metadata | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $output 'msi-metadata.json') -Encoding utf8
$sha = (Get-FileHash -LiteralPath $msi -Algorithm SHA256).Hash.ToLowerInvariant()
$commit = git -C $root rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve source commit' }
$dirty = @(git -C $root status --porcelain).Count -gt 0
[ordered]@{
    product = 'OnlyCut'; version = $version; commit = $commit; dirty = $dirty
    assets = @(@{ name = 'CutPic-Setup-x64.msi'; size = (Get-Item $msi).Length; sha256 = $sha })
    productCode = $metadata.Properties.ProductCode
    upgradeCode = $metadata.Properties.UpgradeCode
} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $output 'candidate-manifest.json') -Encoding utf8
"$sha  CutPic-Setup-x64.msi" | Set-Content (Join-Path $output 'SHA256SUMS.txt') -Encoding ascii
# Payload is build input, not a second downloadable product.
Remove-Item -LiteralPath $publish -Recurse -Force
$pdb = Join-Path $output 'CutPic-Setup-x64.wixpdb'
if (Test-Path -LiteralPath $pdb) { Remove-Item -LiteralPath $pdb }
$validationArgs = @((Join-Path $root 'scripts/validate_candidate.py'), $output, '--version', $version, '--commit', $commit)
if ($dirty) { $validationArgs += '--allow-dirty' }
python @validationArgs
if ($LASTEXITCODE -ne 0) { throw 'Final candidate integrity validation failed' }
$verifyArgs = @{ Directory=$output; Version=$version; Commit=$commit; AllowDirty=$dirty }
& (Join-Path $root 'scripts/verify-msi-candidate.ps1') @verifyArgs
Write-Host "Validated candidate: $msi"
