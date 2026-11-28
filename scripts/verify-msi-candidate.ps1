param(
    [Parameter(Mandatory)][string]$Directory,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$Commit,
    [switch]$AllowDirty
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$argsList = @("$PSScriptRoot\validate_candidate.py", $Directory, '--version', $Version, '--commit', $Commit)
if ($AllowDirty) { $argsList += '--allow-dirty' }
python @argsList
if ($LASTEXITCODE -ne 0) { throw 'Candidate file integrity validation failed' }
$manifest = Get-Content (Join-Path $Directory 'candidate-manifest.json') -Raw | ConvertFrom-Json
$stored = Get-Content (Join-Path $Directory 'msi-metadata.json') -Raw | ConvertFrom-Json
$msi = Join-Path $Directory 'CutPic-Setup-x64.msi'
$before = (Get-FileHash -LiteralPath $msi -Algorithm SHA256).Hash
# Reopen the actual MSI database. The adjacent JSON is a report, not a trust anchor.
$actual = & "$PSScriptRoot\inspect-msi.ps1" -Path $msi
$normalized = $actual | ConvertTo-Json -Depth 12 -Compress | ConvertFrom-Json
foreach ($name in @('ProductVersion','ProductCode','UpgradeCode','ProductName','ALLUSERS')) {
    if ($stored.Properties.$name -cne $actual.Properties[$name]) { throw "MSI/JSON property mismatch: $name" }
}
if ($actual.Properties.ProductVersion -cne $Version -or
    $actual.Properties.ProductCode -cne $manifest.productCode -or
    $actual.Properties.UpgradeCode -cne $manifest.upgradeCode -or
    $actual.Properties.UpgradeCode -ine '{6E02C1AA-9A9D-4CB1-8F6F-37AD338F316A}' -or
    $actual.Properties.ProductName -cne 'CutPic' -or $actual.Properties.ALLUSERS -ne '1' -or
    $actual.SummaryTemplate -notmatch '^x64;') { throw 'Actual MSI identity/scope/architecture mismatch' }
foreach ($name in @('Components','Upgrades','Directories','Files','LaunchConditions','SummaryTemplate')) {
    $left = ConvertTo-Json -InputObject $stored.$name -Depth 12 -Compress
    $right = ConvertTo-Json -InputObject $normalized.$name -Depth 12 -Compress
    if ($left -cne $right) { throw "MSI/JSON table mismatch: $name" }
}
if ($before -cne (Get-FileHash -LiteralPath $msi -Algorithm SHA256).Hash) { throw 'MSI changed during verification' }
Write-Host "PASS: actual MSI database matches version, identity, architecture, tables and manifest SHA-256 ($before)"
