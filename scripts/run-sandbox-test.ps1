$ErrorActionPreference = 'Stop'
if (Get-Process WindowsSandboxClient -ErrorAction SilentlyContinue) { throw 'An existing Windows Sandbox session is active. Use a dedicated idle sandbox; do not replace another test session.' }
$root = Split-Path -Parent $PSScriptRoot
$inputDir = Join-Path $root 'artifacts\sandbox-input'
$output = Join-Path $root 'artifacts\sandbox-results'
New-Item -ItemType Directory -Force $inputDir,$output | Out-Null
$historical = Join-Path $root 'artifacts\installer\CutPic-Setup-x64.msi'
if ((Get-FileHash $historical).Hash -ne 'E6AE6EBD6050C25257FA27C9229EF358C793DC315C54388DCB29C753A1432862') { throw 'Historical asset checksum mismatch' }
Copy-Item $historical "$inputDir\historical.msi"
Copy-Item "$root\artifacts\candidate\CutPic-Setup-x64.msi" "$inputDir\candidate.msi"
Copy-Item "$PSScriptRoot\sandbox-install-test.ps1" "$inputDir\test.ps1"
$old = & "$PSScriptRoot\inspect-msi.ps1" -Path "$inputDir\historical.msi"
$new = & "$PSScriptRoot\inspect-msi.ps1" -Path "$inputDir\candidate.msi"
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
