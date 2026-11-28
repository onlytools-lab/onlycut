param([Parameter(Mandatory)][string]$Baseline,[Parameter(Mandatory)][string]$Candidate)
$ErrorActionPreference='Stop'
$old=& "$PSScriptRoot\inspect-msi.ps1" $Baseline
$new=& "$PSScriptRoot\inspect-msi.ps1" $Candidate
foreach($field in @('ProductName','UpgradeCode','ALLUSERS')){if($old.Properties[$field] -cne $new.Properties[$field]){throw "Compatibility identity changed: $field"}}
$map=@{};foreach($row in $old.Components){$map[$row[0]]=$row}
$common=0;$changed=@()
foreach($row in $new.Components){if($map.ContainsKey($row[0])){$common++;if(($map[$row[0]] -join '|') -cne ($row -join '|')){$changed+=,$row}}}
if($changed.Count){throw 'Stable component GUID/Directory/KeyPath changed; STOP for compatibility review'}
foreach($id in @('INSTALLFOLDER','ProgramMenuCutPicFolder')){
 $a=@($old.Directories | Where-Object {$_[0] -eq $id});$b=@($new.Directories | Where-Object {$_[0] -eq $id})
 if($a.Count -ne 1 -or $b.Count -ne 1 -or ($a[0] -join '|') -cne ($b[0] -join '|')){throw "Installation directory changed: $id"}
}
[pscustomobject]@{status='PASS';baselineSha256=(Get-FileHash $Baseline).Hash;candidateSha256=(Get-FileHash $Candidate).Hash;oldComponents=$old.Components.Count;newComponents=$new.Components.Count;unchangedCommonComponents=$common;changedCommonComponents=$changed.Count}
