# Run only in a dedicated, disposable Windows Sandbox with mapped input/results.
$ErrorActionPreference='Stop'
$inputDir='C:\OnlyCutUpgradeInput';$out='C:\OnlyCutUpgradeResults'
if($env:USERNAME -ne 'WDAGUtilityAccount' -or !(Test-Path "$inputDir\inputs.json") -or !(Test-Path $out)){throw 'Not the dedicated upgrade sandbox'}
$spec=Get-Content "$inputDir\inputs.json" -Raw | ConvertFrom-Json
$wi=New-Object -ComObject WindowsInstaller.Installer
$exe=Join-Path $env:ProgramFiles 'CutPic\CutPic.exe';$dll=Join-Path $env:ProgramFiles 'CutPic\CutPic.dll'
$data=Join-Path $env:LOCALAPPDATA 'CutPic'
$results=[ordered]@{}
foreach($name in @('historical-install','historical-launch','upgrade','upgraded-launch','data-after-upgrade','functional-smoke','maintenance','repair','same-version-rejection','downgrade-rejection','post-rejection-launch','uninstall','data-retention')){$results[$name]=@{status='NOT TESTED';exitCode=$null;actual=$null}}
$failed=$false
function State([string]$code){return [int]$wi.ProductState($code)}
function Hash([string]$path){if(Test-Path $path){return (Get-FileHash $path -Algorithm SHA256).Hash}else{return $null}}
function Related { $list=@();foreach($code in $wi.RelatedProducts($spec.upgradeCode)){$list += [string]$code};return ,$list }
function Snapshot {
 return @{old=(State $spec.oldCode);candidate=(State $spec.candidateCode);twin=(State $spec.twinCode);related=(Related);exeExists=(Test-Path $exe);dllExists=(Test-Path $dll);exeHash=(Hash $exe);dllHash=(Hash $dll);fileVersion=$(if(Test-Path $dll){(Get-Item $dll).VersionInfo.FileVersion}else{$null});projectHash=(Hash "$data\retained.cutpic");markerHash=(Hash "$data\marker.txt")}
}
function Run([string]$name,[string]$file,[string[]]$arguments){
 $p=Start-Process $file -ArgumentList $arguments -PassThru
 if(!$p.WaitForExit(120000)){$p.Kill();throw "Timeout: $name"};$results[$name].exitCode=$p.ExitCode;return $p.ExitCode
}
function Msi([string]$name,[string[]]$arguments){return (Run $name msiexec.exe ($arguments + @('/qn','/norestart','/L*v',"`"$out\$name.log`"")))}
function Assert-Target {
 $s=Snapshot
 if($s.old -ne -1 -or $s.candidate -ne 5 -or $s.twin -ne -1 -or $s.fileVersion -ne '1.0.1.0' -or !$s.exeExists -or @($s.related).Count -ne 1 -or $s.related[0] -ne $spec.candidateCode){throw 'Wrong product registration/version after operation'}
 return $s
}
function Assert-Data {
 if((Hash "$data\retained.cutpic") -ne $script:projectHash -or (Hash "$data\marker.txt") -ne $script:markerHash){throw 'User project/marker changed'}
 $settings=Get-Content "$data\settings.json" -Raw | ConvertFrom-Json
 if($settings.NamingSuffix -ne 'upgrade-retention' -or $settings.NamingStartNumber -ne '77'){throw 'User setting values lost'}
}
function Stage([string]$name,[scriptblock]$action){
 try{& $action;Assert-Data;$results[$name].status='PASS'}catch{$script:failed=$true;$results[$name].status='FAIL';$results[$name].error=$_.ToString()}
 $results[$name].actual=Snapshot
 $results | ConvertTo-Json -Depth 12 | Set-Content "$out\stages.json" -Encoding utf8
}
function Require([string]$name){if($results[$name].status -ne 'PASS'){throw "Required stage failed: $name"}}
Start-Transcript "$out\transcript.txt" -Force
try{
 if((Related).Count -ne 0 -or (Test-Path $exe) -or (Test-Path $data)){throw 'Sandbox is not a clean target'}
 foreach($asset in $spec.assets){if((Hash "$inputDir\$($asset.name)").ToLowerInvariant() -ne $asset.sha256){throw "Input asset hash mismatch: $($asset.name)"}}
 New-Item -ItemType Directory $data | Out-Null
 '{"SchemaVersion":4,"AutoRestoreSession":false,"NamingSuffix":"upgrade-retention","NamingStartNumber":"77"}' | Set-Content "$data\settings.json" -Encoding utf8
 '{"Version":4,"Tasks":[]}' | Set-Content "$data\retained.cutpic" -Encoding utf8
 'OnlyCut upgrade retention fixture' | Set-Content "$data\marker.txt"
 $script:projectHash=Hash "$data\retained.cutpic";$script:markerHash=Hash "$data\marker.txt"
 Copy-Item -LiteralPath $data -Destination "$out\data-backup" -Recurse
 Get-ChildItem $data -File | ForEach-Object {@{name=$_.Name;sha256=(Hash $_.FullName)}} | ConvertTo-Json | Set-Content "$out\data-before.json"
 Stage 'historical-install' {
  $code=Msi 'historical-install' @('/i',"`"$inputDir\historical.msi`"");$s=Snapshot
  if($code -notin @(0,3010) -or $s.old -ne 5 -or $s.candidate -ne -1 -or $s.fileVersion -ne '1.0.0.0'){throw 'Historical install failed'}
 };Require 'historical-install'
 Stage 'historical-launch' {if((Run 'historical-launch' $exe @('--smoke-main')) -ne 0){throw 'Historical launch failed'}};Require 'historical-launch'
 Stage 'upgrade' {
  if((Msi 'upgrade' @('/i',"`"$inputDir\candidate.msi`"")) -notin @(0,3010)){throw 'Upgrade process failed'}
  [void](Assert-Target)
 };Require 'upgrade'
 Stage 'upgraded-launch' {if((Run 'upgraded-launch' $exe @('--smoke-main')) -ne 0){throw 'Upgraded app failed'};[void](Assert-Target)}
 Stage 'data-after-upgrade' {Assert-Data}
 Stage 'functional-smoke' {
  $env:CUTPIC_DATA_DIR="$out\functional-data"
  try {
   $selfCode=Run 'functional-smoke' $exe @('--self-test',"`"$out\installed-self-tests.txt`"")
   $acceptCode=Run 'functional-smoke' $exe @('--acceptance',"`"$out\installed-acceptance.txt`"")
   $results['functional-smoke'].selfTestExitCode=$selfCode;$results['functional-smoke'].acceptanceExitCode=$acceptCode
   if($selfCode -ne 0 -or $acceptCode -ne 0 -or @(Select-String -Path "$out\installed-self-tests.txt" -Pattern '^PASS ').Count -ne 13 -or @(Select-String -Path "$out\installed-acceptance.txt" -Pattern '^PASS ').Count -ne 6){throw 'Installed app functional tests failed'}
  }finally{Remove-Item Env:CUTPIC_DATA_DIR -ErrorAction SilentlyContinue}
 }
 Stage 'maintenance' {
  $before=Snapshot
  if((Msi 'maintenance' @('/i',"`"$inputDir\candidate.msi`"")) -notin @(0,3010)){throw 'Same-product maintenance failed'}
  $after=Assert-Target;if($before.dllHash -ne $after.dllHash){throw 'Maintenance unexpectedly changed binary'}
 }
 Stage 'repair' {
  $before=Assert-Target
  Copy-Item $dll "$out\before-repair-CutPic.dll"
  Remove-Item -LiteralPath $dll
  if(Test-Path $dll){throw 'Repair fixture was not removed'}
  if((Msi 'repair' @('/fa',"`"$inputDir\candidate.msi`"")) -notin @(0,3010)){throw 'Repair process failed'}
  $after=Assert-Target;if($before.dllHash -ne $after.dllHash){throw 'Repair did not restore original DLL hash'}
 }
 Stage 'same-version-rejection' {
  $before=Assert-Target
  $code=Msi 'same-version-rejection' @('/i',"`"$inputDir\twin.msi`"")
  $log=Get-Content "$out\same-version-rejection.log" -Raw
  if($code -ne 1603 -or !$log.Contains('ONLYCUT_SAME_VERSION_BLOCKED') -or !$log.Contains($spec.candidateCode)){throw 'Expected same-version launch rejection was not observed'}
  $after=Assert-Target;if($before.dllHash -ne $after.dllHash -or $before.exeHash -ne $after.exeHash){throw 'Rejected installer changed files'}
 }
 Stage 'downgrade-rejection' {
  $before=Assert-Target
  $code=Msi 'downgrade-rejection' @('/i',"`"$inputDir\historical.msi`"")
  $log=Get-Content "$out\downgrade-rejection.log" -Raw
  if($code -ne 1603 -or !$log.Contains('Adding WIX_DOWNGRADE_DETECTED property') -or !$log.Contains($spec.candidateCode)){throw 'Expected downgrade detection was not observed'}
  $after=Assert-Target;if($before.dllHash -ne $after.dllHash -or $before.exeHash -ne $after.exeHash){throw 'Downgrade changed files'}
 }
 Stage 'post-rejection-launch' {if((Run 'post-rejection-launch' $exe @('--smoke-main')) -ne 0){throw 'App failed after rejected installs'};[void](Assert-Target)}
 Stage 'uninstall' {
  if((Msi 'uninstall' @('/x',$spec.candidateCode)) -notin @(0,3010)){throw 'Uninstall process failed'}
  $s=Snapshot;if((Related).Count -ne 0 -or $s.old -ne -1 -or $s.candidate -ne -1 -or $s.twin -ne -1 -or $s.exeExists -or $s.dllExists){throw 'Uninstall left product registration or main files'}
 }
 Stage 'data-retention' {Assert-Data}
}catch{$failed=$true;$_ | Out-String | Set-Content "$out\error.txt"}
finally{
 if(@($results.Values | Where-Object {$_.status -ne 'PASS'}).Count){$failed=$true}
 @{status=$(if($failed){'FAIL'}else{'PASS'});inputs=$spec;stages=$results;manualGUI='NOT TESTED';dpi='NOT TESTED';realUserMigration='NOT TESTED'} | ConvertTo-Json -Depth 15 | Set-Content "$out\result.json" -Encoding utf8
 Stop-Transcript;Set-Content "$out\completed.txt" 'completed'
}
if($failed){exit 1}else{exit 0}
