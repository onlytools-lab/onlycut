param([Parameter(Mandatory)][string]$Directory)
$ErrorActionPreference = 'Stop'
$manifest = Get-Content "$Directory\candidate-manifest.json" -Raw | ConvertFrom-Json
$parameters = @{ Directory=$Directory; Version=$manifest.version; Commit=$manifest.commit; AllowDirty=[bool]$manifest.dirty }
& "$PSScriptRoot\verify-msi-candidate.ps1" @parameters
$testDir = Join-Path (Split-Path -Parent $Directory) ('metadata-negative-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $testDir | Out-Null
Get-ChildItem -LiteralPath $Directory -File | Copy-Item -Destination $testDir
# Forge BOTH adjacent JSON files together. The cheap consistency checker alone
# accepts this, but the Windows verifier must reject it by reading the MSI.
$metadata = Get-Content "$testDir\msi-metadata.json" -Raw | ConvertFrom-Json
$metadata.Properties.ProductCode = '{11111111-1111-1111-1111-111111111111}'
$manifest.productCode = $metadata.Properties.ProductCode
$metadata | ConvertTo-Json -Depth 12 | Set-Content "$testDir\msi-metadata.json" -Encoding utf8
$manifest | ConvertTo-Json -Depth 12 | Set-Content "$testDir\candidate-manifest.json" -Encoding utf8
$parameters.Directory=$testDir
$rejected=$false
try { & "$PSScriptRoot\verify-msi-candidate.ps1" @parameters } catch { $rejected=$true; Write-Host "Expected rejection: $_" }
if (!$rejected) { throw 'Forged sidecar metadata was trusted over actual MSI' }
Write-Host 'PASS: real MSI verification and coordinated JSON forgery rejection (2 cases)'
