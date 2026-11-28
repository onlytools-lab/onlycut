param([Parameter(Mandatory)][string]$Candidate,[Parameter(Mandatory)][string]$Twin,[int]$TimeoutSeconds=600)
$ErrorActionPreference='Stop'
if(Get-Process WindowsSandboxClient -ErrorAction SilentlyContinue){throw 'Existing sandbox active; do not replace it'}
$root=Split-Path -Parent $PSScriptRoot
$run=Join-Path $root ('artifacts\upgrade-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$inputDir=Join-Path $run 'input';$out=Join-Path $run 'results'
New-Item -ItemType Directory $inputDir,$out -Force | Out-Null
$historical="$root\artifacts\installer\CutPic-Setup-x64.msi"
if((Get-FileHash $historical).Hash -ne 'E6AE6EBD6050C25257FA27C9229EF358C793DC315C54388DCB29C753A1432862'){throw 'Historical hash mismatch'}
$old=& "$PSScriptRoot\inspect-msi.ps1" $historical
$new=& "$PSScriptRoot\inspect-msi.ps1" $Candidate
$other=& "$PSScriptRoot\inspect-msi.ps1" $Twin
if($old.Properties.ProductVersion -ne '1.0.0' -or $new.Properties.ProductVersion -ne '1.0.1' -or $other.Properties.ProductVersion -ne '1.0.1' -or $new.Properties.ProductCode -eq $other.Properties.ProductCode){throw 'Wrong upgrade test identities'}
& "$PSScriptRoot\compare-msi-identity.ps1" -Baseline $historical -Candidate $Candidate | ConvertTo-Json | Set-Content "$out\historical-identity-comparison.json"
& "$PSScriptRoot\compare-msi-identity.ps1" -Baseline $Candidate -Candidate $Twin | ConvertTo-Json | Set-Content "$out\twin-identity-comparison.json"
Copy-Item $historical "$inputDir\historical.msi";Copy-Item $Candidate "$inputDir\candidate.msi";Copy-Item $Twin "$inputDir\twin.msi"
$assets=@(Get-ChildItem "$inputDir\*.msi" | ForEach-Object {@{name=$_.Name;sha256=(Get-FileHash $_.FullName).Hash.ToLowerInvariant()}})
@{oldCode=$old.Properties.ProductCode;candidateCode=$new.Properties.ProductCode;twinCode=$other.Properties.ProductCode;upgradeCode=$new.Properties.UpgradeCode;version='1.0.1';assets=$assets} | ConvertTo-Json -Depth 6 | Set-Content "$inputDir\inputs.json" -Encoding utf8
Copy-Item "$PSScriptRoot\sandbox-upgrade-test.ps1" "$inputDir\test.ps1"
$xml=@"
<Configuration><Networking>Disable</Networking><MappedFolders>
<MappedFolder><HostFolder>$inputDir</HostFolder><SandboxFolder>C:\OnlyCutUpgradeInput</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
<MappedFolder><HostFolder>$out</HostFolder><SandboxFolder>C:\OnlyCutUpgradeResults</SandboxFolder><ReadOnly>false</ReadOnly></MappedFolder>
</MappedFolders><LogonCommand><Command>powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\OnlyCutUpgradeInput\test.ps1</Command></LogonCommand></Configuration>
"@
[IO.File]::WriteAllText("$run\test.wsb",$xml)
Start-Process WindowsSandbox.exe -ArgumentList "`"$run\test.wsb`"" -WindowStyle Hidden
Write-Host "Upgrade sandbox started: $out"
$timer=[Diagnostics.Stopwatch]::StartNew()
while(!(Test-Path "$out\completed.txt")){if($timer.Elapsed.TotalSeconds -gt $TimeoutSeconds){throw "Sandbox incomplete: $out"};Start-Sleep -Seconds 2}
Get-Content "$out\result.json"
if((Get-Content "$out\result.json" -Raw | ConvertFrom-Json).status -ne 'PASS'){throw "Upgrade sandbox FAIL: $out"}
