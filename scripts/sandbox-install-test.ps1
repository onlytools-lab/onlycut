# Executed ONLY inside Windows Sandbox. Input share read-only; output share writable.
$ErrorActionPreference = 'Stop'
$out = 'C:\OnlyCutResults'
$inputDir = 'C:\OnlyCutInput'
Start-Transcript -Path "$out\transcript.txt" -Force
try {
    $data = Join-Path $env:LOCALAPPDATA 'CutPic'
    New-Item -ItemType Directory -Force $data | Out-Null
    Set-Content "$data\retention-marker.txt" 'OnlyCut sandbox retention fixture'
    Copy-Item -LiteralPath $data -Destination "$out\data-backup" -Recurse
    $before = (Get-FileHash "$data\retention-marker.txt").Hash
    function Install([string]$Name) {
        $p = Start-Process msiexec.exe -ArgumentList @('/i', "`"$inputDir\$Name.msi`"", '/qn', '/norestart', '/L*v', "`"$out\$Name-install.log`"") -Wait -PassThru
        return $p.ExitCode
    }
    function Smoke([string]$Name) {
        $exe = Join-Path $env:ProgramFiles 'CutPic\CutPic.exe'
        $p = Start-Process $exe -ArgumentList '--smoke-main' -Wait -PassThru
        if ($p.ExitCode -ne 0) { throw "$Name launch failed: $($p.ExitCode)" }
    }
    function Uninstall([string]$Code, [string]$Name) {
        $p = Start-Process msiexec.exe -ArgumentList @('/x', $Code, '/qn', '/norestart', '/L*v', "`"$out\$Name-uninstall.log`"") -Wait -PassThru
        if ($p.ExitCode -notin @(0,3010)) { throw "Uninstall failed: $($p.ExitCode)" }
    }
    $meta = Get-Content "$inputDir\codes.json" | ConvertFrom-Json
    $old = Install 'historical'
    if ($old -notin @(0,3010)) { throw "Historical install failed: $old" }
    Smoke 'historical'
    $same = Install 'candidate'
    # A same-version candidate is not an approved cross-version upgrade.
    Uninstall $meta.historical 'historical'
    $fresh = Install 'candidate'
    if ($fresh -notin @(0,3010)) { throw "Candidate fresh install failed: $fresh" }
    Smoke 'candidate'
    Uninstall $meta.candidate 'candidate'
    $after = (Get-FileHash "$data\retention-marker.txt").Hash
    if ($before -ne $after) { throw 'User-data marker changed' }
    @{ historicalInstall = $old; sameVersionAttempt = $same; candidateInstall = $fresh; launches = 'passed'; uninstalls = 'passed'; markerPreserved = $true; crossVersionUpgrade = 'NOT TESTED' } |
        ConvertTo-Json | Set-Content "$out\result.json"
} catch {
    $_ | Out-String | Set-Content "$out\error.txt"
} finally {
    Stop-Transcript
    Set-Content "$out\completed.txt" 'completed'
}
