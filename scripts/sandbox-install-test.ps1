# Execute only in a dedicated Windows Sandbox. Never on the host.
$ErrorActionPreference = 'Stop'
$out = 'C:\OnlyCutResults'
$inputDir = 'C:\OnlyCutInput'
if ($env:USERNAME -ne 'WDAGUtilityAccount' -or !(Test-Path "$inputDir\codes.json") -or !(Test-Path $out)) { throw 'Refusing to run outside the dedicated mapped Windows Sandbox' }
$stages = @('historical-install','historical-launch','same-version-attempt','historical-uninstall','candidate-install','candidate-launch','candidate-uninstall','data-retention')
$results = [ordered]@{}
foreach ($stage in $stages) { $results[$stage] = @{ status='NOT TESTED'; exitCode=$null; actual=$null } }
$failed = $false
$installer = New-Object -ComObject WindowsInstaller.Installer
function State([string]$code) { return [int]$installer.ProductState($code) }
function Snapshot {
    $dll = Join-Path (Split-Path -Parent $exe) 'CutPic.dll'
    return @{ historical=(State $meta.historical); candidate=(State $meta.candidate); exeExists=(Test-Path $exe); exeSha256=$(if(Test-Path $exe){(Get-FileHash $exe).Hash}else{$null}); dllSha256=$(if(Test-Path $dll){(Get-FileHash $dll).Hash}else{$null}) }
}
function Process-Result([string]$file, [string[]]$arguments) {
    $p = Start-Process $file -ArgumentList $arguments -PassThru
    if (!$p.WaitForExit(120000)) { $p.Kill(); throw "Timed out: $file" }
    return $p.ExitCode
}
function Run-Stage([string]$name, [scriptblock]$action) {
    try { & $action; if ($results[$name].status -eq 'NOT TESTED') { throw 'Stage returned without a verdict' } }
    catch { $script:failed=$true; $results[$name].status='FAIL'; $results[$name].error=$_.ToString() }
    $results | ConvertTo-Json -Depth 10 | Set-Content "$out\stages.json" -Encoding utf8
}
Start-Transcript -Path "$out\transcript.txt" -Force
try {
    $meta = Get-Content "$inputDir\codes.json" -Raw | ConvertFrom-Json
    $exe = Join-Path $env:ProgramFiles 'CutPic\CutPic.exe'
    if ((State $meta.historical) -ne -1 -or (State $meta.candidate) -ne -1 -or (Test-Path $exe)) { throw 'Sandbox is not a clean install target' }
    $data = Join-Path $env:LOCALAPPDATA 'CutPic'
    New-Item -ItemType Directory -Force $data | Out-Null
    '{"SchemaVersion":4,"AutoRestoreSession":false,"NamingSuffix":"sandbox-retention"}' | Set-Content "$data\settings.json" -Encoding utf8
    '{"Version":4,"Tasks":[]}' | Set-Content "$data\fixture.cutpic" -Encoding utf8
    'OnlyCut retention fixture' | Set-Content "$data\retention-marker.txt"
    Copy-Item -LiteralPath $data -Destination "$out\data-backup" -Recurse
    $hashes = @{}
    Get-ChildItem $data -File | ForEach-Object { $hashes[$_.Name]=(Get-FileHash $_.FullName).Hash }
    $hashes | ConvertTo-Json | Set-Content "$out\data-before.json"
    Run-Stage 'historical-install' {
        $r=$results['historical-install']; $r.exitCode=Process-Result msiexec.exe @('/i',"`"$inputDir\historical.msi`"",'/qn','/norestart','/L*v',"`"$out\historical-install.log`"")
        $r.actual=Snapshot
        if ($r.exitCode -notin @(0,3010) -or $r.actual.historical -ne 5 -or $r.actual.candidate -ne -1 -or !$r.actual.exeExists) { throw 'Historical install state mismatch' }
        $r.status='PASS'
    }
    if ($results['historical-install'].status -eq 'PASS') {
        Run-Stage 'historical-launch' {
            $r=$results['historical-launch']; $r.exitCode=Process-Result $exe @('--smoke-main'); $r.actual=Snapshot
            if ($r.exitCode -ne 0 -or $r.actual.historical -ne 5) { throw 'Historical launch failed' }; $r.status='PASS'
        }
        Run-Stage 'same-version-attempt' {
            $before=Snapshot; $r=$results['same-version-attempt']
            $r.exitCode=Process-Result msiexec.exe @('/i',"`"$inputDir\candidate.msi`"",'/qn','/norestart','/L*v',"`"$out\same-version.log`"")
            $r.actual=Snapshot
            $r.behavior=if($r.actual.historical -eq 5 -and $r.actual.candidate -eq 5){'COEXISTENCE'}elseif($r.actual.candidate -eq 5){'REPLACEMENT'}else{'REJECTED_OR_FAILED'}
            # Safe rejection is the only PASS for a different-ProductCode, same-version package.
            # MSI may allow coexistence; report it as FAIL, never as upgrade success.
            if ($r.exitCode -ne 1638 -or $r.actual.historical -ne 5 -or $r.actual.candidate -ne -1 -or $before.exeSha256 -ne $r.actual.exeSha256 -or $before.dllSha256 -ne $r.actual.dllSha256) { throw 'Same-version candidate did not safely reject without coexistence/replacement' }
            $r.status='PASS'; $r.behavior='SAFE_REJECTION_NOT_AN_UPGRADE'
        }
        Run-Stage 'historical-uninstall' {
            $r=$results['historical-uninstall']; $r.exitCode=Process-Result msiexec.exe @('/x',$meta.historical,'/qn','/norestart','/L*v',"`"$out\historical-uninstall.log`""); $r.actual=Snapshot
            if ($r.exitCode -notin @(0,3010) -or $r.actual.historical -ne -1) { throw 'Historical uninstall failed' }; $r.status='PASS'
        }
        # If same-version installation registered the candidate, clean it inside this sandbox
        # before the separate clean-install case. This never changes the failed overall verdict.
        if ((State $meta.candidate) -eq 5) {
            $code=Process-Result msiexec.exe @('/x',$meta.candidate,'/qn','/norestart','/L*v',"`"$out\coexistence-cleanup.log`"")
            if ($code -notin @(0,3010) -or (State $meta.candidate) -ne -1) { throw 'Coexistence cleanup failed' }
        }
    }
    if ((State $meta.historical) -eq -1 -and (State $meta.candidate) -eq -1) {
        Run-Stage 'candidate-install' {
            $r=$results['candidate-install']; $r.exitCode=Process-Result msiexec.exe @('/i',"`"$inputDir\candidate.msi`"",'/qn','/norestart','/L*v',"`"$out\candidate-install.log`""); $r.actual=Snapshot
            if ($r.exitCode -notin @(0,3010) -or $r.actual.candidate -ne 5 -or $r.actual.historical -ne -1 -or !$r.actual.exeExists) { throw 'Candidate install failed' }; $r.status='PASS'
        }
        if ($results['candidate-install'].status -eq 'PASS') {
            Run-Stage 'candidate-launch' {
                $r=$results['candidate-launch']; $r.exitCode=Process-Result $exe @('--smoke-main'); $r.actual=Snapshot
                if ($r.exitCode -ne 0 -or $r.actual.candidate -ne 5) { throw 'Candidate launch failed' }; $r.status='PASS'
            }
            Run-Stage 'candidate-uninstall' {
                $r=$results['candidate-uninstall']; $r.exitCode=Process-Result msiexec.exe @('/x',$meta.candidate,'/qn','/norestart','/L*v',"`"$out\candidate-uninstall.log`""); $r.actual=Snapshot
                if ($r.exitCode -notin @(0,3010) -or $r.actual.candidate -ne -1 -or $r.actual.historical -ne -1 -or $r.actual.exeExists) { throw 'Candidate uninstall left registration/executable' }; $r.status='PASS'
            }
        }
    }
    Run-Stage 'data-retention' {
        $r=$results['data-retention']; $r.exitCode=$null
        $r.actual=@{}
        foreach($name in @('fixture.cutpic','retention-marker.txt')) { $r.actual[$name]=(Get-FileHash "$data\$name").Hash; if($r.actual[$name] -ne $hashes[$name]){throw "Data changed: $name"} }
        $settings=Get-Content "$data\settings.json" -Raw | ConvertFrom-Json
        $r.actual.namingSuffix=$settings.NamingSuffix
        if ($settings.NamingSuffix -ne 'sandbox-retention') { throw 'User configuration value lost' }
        $r.status='PASS'
    }
} catch { $failed=$true; $_ | Out-String | Set-Content "$out\error.txt" }
finally {
    if (@($stages | Where-Object {$results[$_].status -ne 'PASS'}).Count) { $failed=$true }
    @{ status=$(if($failed){'FAIL'}else{'PASS'}); stages=$results; manualGUI='NOT TESTED'; crossVersionUpgrade='NOT TESTED' } | ConvertTo-Json -Depth 12 | Set-Content "$out\result.json" -Encoding utf8
    Stop-Transcript
    Set-Content "$out\completed.txt" 'completed'
}
if($failed){exit 1}else{exit 0}
