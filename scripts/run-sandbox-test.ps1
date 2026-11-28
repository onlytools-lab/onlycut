param([int]$TimeoutSeconds = 600)
$ErrorActionPreference = 'Stop'
if (Get-Process WindowsSandboxClient -ErrorAction SilentlyContinue) { throw 'An existing Windows Sandbox session is active. Use a dedicated idle sandbox; do not replace another test session.' }
$root = Split-Path -Parent $PSScriptRoot
$runId = Get-Date -Format 'yyyyMMdd-HHmmss'
$inputDir = Join-Path $root "artifacts\sandbox-$runId-input"
$output = Join-Path $root "artifacts\sandbox-$runId-results"
New-Item -ItemType Directory -Force $inputDir,$output | Out-Null
$historical = Join-Path $root 'artifacts\installer\CutPic-Setup-x64.msi'
if ((Get-FileHash $historical).Hash -ne 'E6AE6EBD6050C25257FA27C9229EF358C793DC315C54388DCB29C753A1432862') { throw 'Historical asset checksum mismatch' }
Copy-Item $historical "$inputDir\historical.msi"
Copy-Item "$root\artifacts\candidate\CutPic-Setup-x64.msi" "$inputDir\candidate.msi"
Copy-Item "$PSScriptRoot\sandbox-install-test.ps1" "$inputDir\test.ps1"
$old = & "$PSScriptRoot\inspect-msi.ps1" -Path "$inputDir\historical.msi"
$new = & "$PSScriptRoot\inspect-msi.ps1" -Path "$inputDir\candidate.msi"
if ($old.Properties.ProductCode -eq $new.Properties.ProductCode -or $old.Properties.ProductVersion -ne $new.Properties.ProductVersion) { throw 'This suite requires different ProductCodes and equal versions' }
@{ historical = $old.Properties.ProductCode; candidate = $new.Properties.ProductCode } | ConvertTo-Json | Set-Content "$inputDir\codes.json"
$config = @"
<Configuration>
  <Networking>Disable</Networking>
  <MappedFolders>
    <MappedFolder><HostFolder>$inputDir</HostFolder><SandboxFolder>C:\OnlyCutInput</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>$output</HostFolder><SandboxFolder>C:\OnlyCutResults</SandboxFolder><ReadOnly>false</ReadOnly></MappedFolder>
  </MappedFolders>
  <LogonCommand><Command>powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\OnlyCutInput\test.ps1</Command></LogonCommand>
</Configuration>
"@
# Generated machine-specific launch configuration, not repository source.
[IO.File]::WriteAllText("$inputDir\test.wsb", $config)
Start-Process WindowsSandbox.exe -ArgumentList "`"$inputDir\test.wsb`"" -WindowStyle Hidden
Write-Host "Sandbox launched. Evidence: $output"
$timer = [Diagnostics.Stopwatch]::StartNew()
while (!(Test-Path "$output\completed.txt")) {
    if ($timer.Elapsed.TotalSeconds -gt $TimeoutSeconds) { throw "Sandbox timed out; NOT TESTED/incomplete. Evidence: $output. Existing sandbox is left intact." }
    Start-Sleep -Seconds 2
}
$result = Get-Content "$output\result.json" -Raw | ConvertFrom-Json
Get-Content "$output\result.json"
if ($result.status -ne 'PASS') { throw "Sandbox acceptance FAILED: $output" }
