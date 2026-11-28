param([Parameter(Mandatory)][string]$Path)
$ErrorActionPreference = 'Stop'
$installer = New-Object -ComObject WindowsInstaller.Installer
$database = $installer.OpenDatabase((Resolve-Path -LiteralPath $Path).Path, 0)
function Read-Rows([string]$Sql, [int]$Columns) {
    $view = $database.OpenView($Sql)
    [void]$view.Execute()
    $rows = @()
    while ($record = $view.Fetch()) {
        $values = @()
        for ($i=1; $i -le $Columns; $i++) { $values += $record.StringData($i) }
        $rows += ,$values
    }
    [void]$view.Close()
    return ,$rows
}
$properties = @{}
foreach ($row in (Read-Rows 'SELECT `Property`, `Value` FROM `Property`' 2)) { $properties[$row[0]] = $row[1] }
[pscustomobject]@{
    Properties = $properties
    Components = (Read-Rows 'SELECT `Component`, `ComponentId`, `Directory_`, `KeyPath` FROM `Component`' 4)
    Upgrades = (Read-Rows 'SELECT `UpgradeCode`, `VersionMin`, `VersionMax`, `Attributes`, `ActionProperty` FROM `Upgrade`' 5)
    Directories = (Read-Rows 'SELECT `Directory`, `Directory_Parent`, `DefaultDir` FROM `Directory`' 3)
    Files = (Read-Rows 'SELECT `File`, `Component_`, `FileName`, `Version` FROM `File`' 4)
    LaunchConditions = (Read-Rows 'SELECT `Condition`, `Description` FROM `LaunchCondition`' 2)
    SummaryTemplate = $database.SummaryInformation(0).Property(7)
}
