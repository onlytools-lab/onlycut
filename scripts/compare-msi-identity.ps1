param([Parameter(Mandatory)][string]$Baseline,[Parameter(Mandatory)][string]$Candidate)
$ErrorActionPreference='Stop'
$old=& "$PSScriptRoot\inspect-msi.ps1" $Baseline
$new=& "$PSScriptRoot\inspect-msi.ps1" $Candidate
foreach($field in @('ProductName','UpgradeCode','ALLUSERS')){if($old.Properties[$field] -cne $new.Properties[$field]){throw "Compatibility identity changed: $field"}}
$map=@{};foreach($row in $old.Components){$map[$row[0]]=$row}
$common=0;$changed=@()
foreach($row in $new.Components){if($map.ContainsKey($row[0])){$common++;if(($map[$row[0]] -join '|') -cne ($row -join '|')){$changed+=,$row}}}
if($changed.Count){throw 'Stable component GUID/Directory/KeyPath changed; STOP for compatibility review'}
# Also match by installed filename, so renamed generated IDs cannot hide a
# change to a shared file's component identity by dropping out of the intersection.
$oldFiles=@{};foreach($file in $old.Files){$name=($file[2] -split '\|')[-1];$key=$map[$file[1]][2] + '/' + $name;$oldFiles[$key]=$file}
$newComponents=@{};foreach($row in $new.Components){$newComponents[$row[0]]=$row}
foreach($file in $new.Files){
 $name=($file[2] -split '\|')[-1];$key=$newComponents[$file[1]][2] + '/' + $name
 if($oldFiles.ContainsKey($key)){
  $a=$map[$oldFiles[$key][1]];$b=$newComponents[$file[1]]
  if(($a -join '|') -cne ($b -join '|')){throw "Shared file component identity changed: $name"}
 }
}
foreach($id in @('INSTALLFOLDER','ProgramMenuCutPicFolder')){
 $a=@($old.Directories | Where-Object {$_[0] -eq $id});$b=@($new.Directories | Where-Object {$_[0] -eq $id})
 if($a.Count -ne 1 -or $b.Count -ne 1 -or ($a[0] -join '|') -cne ($b[0] -join '|')){throw "Installation directory changed: $id"}
}
[pscustomobject]@{status='PASS';baselineSha256=(Get-FileHash $Baseline).Hash;candidateSha256=(Get-FileHash $Candidate).Hash;oldComponents=$old.Components.Count;newComponents=$new.Components.Count;unchangedCommonComponents=$common;changedCommonComponents=$changed.Count}
