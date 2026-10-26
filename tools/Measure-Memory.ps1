param(
    [int]$Updates = 12000,
    [int]$DurationSeconds = 120,
    [string]$DataRoot = (Join-Path $env:TEMP ('EftPosition-benchmark-' + [Guid]::NewGuid().ToString('N'))),
    [string]$ReportRoot = (Join-Path $PSScriptRoot '..\reports'),
    [switch]$CycleMaps,
    [string]$CacheDirectory,
    [switch]$FullScreen
)
$ErrorActionPreference = 'Stop'
$appRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$ReportRoot = [IO.Path]::GetFullPath($ReportRoot)
$DataRoot = [IO.Path]::GetFullPath($DataRoot)
if (Test-Path -LiteralPath $DataRoot) { throw 'Dla bezpieczeństwa wybierz nowy, nieistniejący folder danych testowych.' }
New-Item -ItemType Directory -Path $DataRoot | Out-Null
New-Item -ItemType Directory -Force -Path $ReportRoot | Out-Null
$telemetry = Join-Path $ReportRoot 'memory-app.csv'
$external = Join-Path $ReportRoot 'memory-process-tree.csv'
$summary = Join-Path $ReportRoot 'memory-summary.json'
$format = [Globalization.CultureInfo]::InvariantCulture
$arguments = '--demo --folder "' + $DataRoot + '" --telemetry "' + $telemetry + '" --exercise-view --exit-after ' + $DurationSeconds
if ($CycleMaps) { $arguments += ' --cycle-maps' }
if ($FullScreen) { $arguments += ' --fullscreen' }
if ($CacheDirectory) { $arguments += ' --cache-dir "' + [IO.Path]::GetFullPath($CacheDirectory) + '"' }
# This is the actual interactive application window, not a background helper.
$app = Start-Process -FilePath (Join-Path $appRoot 'EftPosition.exe') -ArgumentList $arguments -WindowStyle Normal -PassThru
$clock = [Diagnostics.Stopwatch]::StartNew()
$samples = [Collections.Generic.List[object]]::new()
$made = 0
$lastSample = -1.0
$lastProcessTree = -1.0
$descendants = @()
$allChildren = [Collections.Generic.HashSet[int]]::new()
$inventoryOk = $true
$peakTreeWs = 0L
$peakTreePrivate = 0L
try {
    while (!$app.HasExited) {
        $seconds = $clock.Elapsed.TotalSeconds
        # 15 s warmup, 75 s of updates, 30 s cooldown for the default run.
        $writeSpan = [Math]::Max(1, $DurationSeconds - 45)
        $target = [Math]::Min($Updates, [Math]::Max(0, [int]([Math]::Floor(($seconds - 15) / $writeSpan * $Updates))))
        while ($made -lt $target) {
            $x = -300 + ($made % 900)
            $z = -240 + ($made % 450)
            $name = [string]::Format($format, '2026-10-26[12-34]_{0:F2}, 2.30, {1:F2}_0.00000, 0.70711, 0.00000, 0.70711 ({2}).png', $x, $z, $made)
            [IO.File]::WriteAllBytes((Join-Path $DataRoot $name), [byte[]]@())
            if ($made % 100 -eq 0) { [IO.File]::WriteAllText((Join-Path $DataRoot ('invalid-' + $made + '.png')), 'invalid fixture') }
            $made++
        }
        if ($seconds - $lastProcessTree -ge 2) {
            $lastProcessTree = $seconds
            try {
                $inventory = @(Get-CimInstance Win32_Process -Property ProcessId,ParentProcessId -ErrorAction Stop)
                $tree = [Collections.Generic.HashSet[int]]::new()
                $null = $tree.Add($app.Id)
                do {
                    $added = $false
                    foreach ($proc in $inventory) {
                        if ($tree.Contains([int]$proc.ParentProcessId) -and $tree.Add([int]$proc.ProcessId)) { $added = $true }
                    }
                } while ($added)
                $descendants = @($tree | Where-Object { $_ -ne $app.Id })
                foreach ($childId in $descendants) { $null = $allChildren.Add([int]$childId) }
            } catch { $inventoryOk = $false }
        }
        if ($seconds - $lastSample -ge 0.25) {
            $lastSample = $seconds
            $ws = 0L; $private = 0L; $handles = 0; $count = 0
            foreach ($procId in @($app.Id) + $descendants) {
                $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
                if ($proc) { $ws += $proc.WorkingSet64; $private += $proc.PrivateMemorySize64; $handles += $proc.HandleCount; $count++ }
            }
            $peakTreeWs = [Math]::Max($peakTreeWs, $ws)
            $peakTreePrivate = [Math]::Max($peakTreePrivate, $private)
            $samples.Add([pscustomobject]@{ elapsedSeconds = $seconds.ToString('F3',$format); totalWorkingSetBytes = $ws; totalPrivateBytes = $private; processCount = $count; totalHandles = $handles; filesCreated = $made })
        }
        Start-Sleep -Milliseconds 25
        $app.Refresh()
    }
    $samples | Export-Csv -LiteralPath $external -NoTypeInformation -Encoding UTF8
    $rows = @(Import-Csv -LiteralPath $telemetry)
    $last = $rows[-1]
    $expectedX = -300 + (($Updates - 1) % 900)
    $expectedZ = -240 + (($Updates - 1) % 450)
    $lastX = [double]::Parse($last.latestX, $format)
    $lastZ = [double]::Parse($last.latestZ, $format)
    $expectedName = [string]::Format($format, '2026-10-26[12-34]_{0:F2}, 2.30, {1:F2}_0.00000, 0.70711, 0.00000, 0.70711 ({2}).png', $expectedX, $expectedZ, ($Updates - 1))
    $result = [ordered]@{
        dateUtc = [DateTime]::UtcNow.ToString('o'); os = [Environment]::OSVersion.VersionString
        architecture = 'x64'; clr = '.NET Framework 4.8'; durationSeconds = [Math]::Round($clock.Elapsed.TotalSeconds,2)
        requestedUpdates = $Updates; createdValidFiles = $made; retainedPngFiles = @(Get-ChildItem -LiteralPath $DataRoot -Filter '*.png' -File).Count
        latestPositionCorrect = ($lastX -eq $expectedX -and $lastZ -eq $expectedZ -and $last.latestName -eq $expectedName)
        lastReportedX = $lastX; lastReportedZ = $lastZ; finalAcceptedUpdates = [long]$last.accepted
        finalIgnoredNames = [long]$last.ignored; finalDuplicateCacheEntries = [int]$last.cacheEntries
        peakTreeWorkingSetBytes = $peakTreeWs; peakTreePrivateBytes = $peakTreePrivate
        processPeakWorkingSetBytes = [long](($rows | Measure-Object peakWorkingSetBytes -Maximum).Maximum)
        firstWorkingSetBytes = [long]$rows[0].workingSetBytes; finalWorkingSetBytes = [long]$last.workingSetBytes
        firstPrivateBytes = [long]$rows[0].privateBytes; finalPrivateBytes = [long]$last.privateBytes
        finalHandles = [int]$last.handles; firstHandles = [int]$rows[0].handles
        processTreeInventorySucceeded = $inventoryOk; observedChildProcessIds = @($allChildren)
        sampledProcessCountMaximum = ($samples | Measure-Object processCount -Maximum).Maximum
        limitBytes = 400000000; below400MB = ($inventoryOk -and $peakTreeWs -lt 400000000 -and $peakTreePrivate -lt 400000000 -and [long](($rows | Measure-Object peakWorkingSetBytes -Maximum).Maximum) -lt 400000000)
        exitCode = $app.ExitCode; fixtureDirectory = $DataRoot
        applicationSha256 = (Get-FileHash -LiteralPath (Join-Path $appRoot 'EftPosition.exe') -Algorithm SHA256).Hash
        mapCyclingEnabled = [bool]$CycleMaps
        fullScreenRequested = [bool]$FullScreen
        fullscreenObserved = @($rows.fullscreen | Sort-Object -Unique)
        vectorObserved = @($rows.vector | Sort-Object -Unique)
        observedMapIds = @($rows.mapId | Where-Object { $_ } | Sort-Object -Unique)
        completedMapLoads = [int](($rows | Measure-Object mapLoads -Maximum).Maximum)
        method = 'Actual WinForms process; external WorkingSet64/PrivateMemorySize64 sum every ~250ms; CIM descendant inventory every ~2s; in-app counters and OS peak WorkingSet every 1s; view zoom/pan stress every 100ms; optional map switching every 1.5s; no forced GC or working-set trimming. Harness is external QA, not a runtime dependency.'
    }
    $result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $summary -Encoding UTF8
    $result | ConvertTo-Json -Depth 4 | Write-Output
    if ($app.ExitCode -ne 0 -or !$result.latestPositionCorrect -or !$result.below400MB -or ($CycleMaps -and $result.observedMapIds.Count -ne 13)) { throw 'Pomiar nie potwierdził wszystkich kryteriów. Sprawdź raport JSON.' }
} finally {
    if (!$app.HasExited) { $null = $app.CloseMainWindow(); if (!$app.WaitForExit(3000)) { $app.Kill() } }
    $app.Dispose()
}
