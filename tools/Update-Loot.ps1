param([string]$SnapshotRoot=(Join-Path $PSScriptRoot ('..\work\loot-source-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))))
$ErrorActionPreference='Stop'
$eftProject=Split-Path -Parent $PSScriptRoot
New-Item -ItemType Directory -Path $SnapshotRoot -Force|Out-Null
foreach($eftMode in @('regular','pve','pvp-season')){foreach($eftDataset in @('maps','maps_en')){Invoke-WebRequest -Uri ('https://json.tarkov.dev/'+$eftMode+'/'+$eftDataset) -OutFile (Join-Path $SnapshotRoot ($eftMode+'-'+$eftDataset+'.json')) -TimeoutSec 45}}
foreach($eftDataset in @('items','items_en','items_pl')){Invoke-WebRequest -Uri ('https://json.tarkov.dev/regular/'+$eftDataset) -OutFile (Join-Path $SnapshotRoot ('regular-'+$eftDataset+'.json')) -TimeoutSec 45}
& node (Join-Path $PSScriptRoot 'Prepare-Loot.cjs') $SnapshotRoot (Join-Path $SnapshotRoot 'loot.json')
if($LASTEXITCODE -ne 0){throw 'Loot import failed'}
& node (Join-Path $PSScriptRoot 'Audit-Loot.cjs') $SnapshotRoot (Join-Path $SnapshotRoot 'loot.json')
if($LASTEXITCODE -ne 0){throw 'Loot audit failed'}
Copy-Item -LiteralPath (Join-Path $SnapshotRoot 'loot.json'),(Join-Path $SnapshotRoot 'loot.source.json') -Destination (Join-Path $eftProject 'assets') -Force
