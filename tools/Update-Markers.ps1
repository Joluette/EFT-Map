param([string]$SnapshotRoot=(Join-Path $PSScriptRoot ('..\work\marker-source-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))))
$ErrorActionPreference='Stop'
$taskProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
New-Item -ItemType Directory -Path $SnapshotRoot | Out-Null
foreach($taskMode in @('regular','pve','pvp-season')) {
    foreach($taskDataset in @('maps','maps_en','tasks','tasks_en','tasks_pl')) {
        Invoke-WebRequest -Uri ('https://json.tarkov.dev/'+$taskMode+'/'+$taskDataset) -OutFile (Join-Path $SnapshotRoot ($taskMode+'-'+$taskDataset+'.json')) -TimeoutSec 45
    }
}
$taskTarget=Join-Path $taskProject 'assets\markers.json'
$taskTemporary=$taskTarget+'.new'
$taskManifest=Join-Path $taskProject 'assets\markers.source.json'
$taskManifestTemporary=$taskManifest+'.new'
& node (Join-Path $PSScriptRoot 'Prepare-Markers.cjs') $SnapshotRoot $taskTemporary $taskManifestTemporary
if($LASTEXITCODE -ne 0){throw 'Przygotowanie znaczników nie powiodło się.'}
& node (Join-Path $PSScriptRoot 'Audit-Markers.cjs') $SnapshotRoot $taskTemporary
if($LASTEXITCODE -ne 0){throw 'Sprawdzenie współrzędnych nie powiodło się.'}
if(Test-Path -LiteralPath $taskTarget){[IO.File]::Replace($taskTemporary,$taskTarget,(Join-Path ([IO.Path]::GetFullPath($SnapshotRoot)) 'previous-markers.json'))}else{[IO.File]::Move($taskTemporary,$taskTarget)}
if(Test-Path -LiteralPath $taskManifest){[IO.File]::Replace($taskManifestTemporary,$taskManifest,(Join-Path ([IO.Path]::GetFullPath($SnapshotRoot)) 'previous-markers.source.json'))}else{[IO.File]::Move($taskManifestTemporary,$taskManifest)}
Write-Output ('Zaktualizowano dane: '+$taskTarget)
