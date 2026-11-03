param([string]$FixtureRoot = (Join-Path $env:TEMP ('EftPosition-tests-' + [Guid]::NewGuid().ToString('N'))))
$ErrorActionPreference = 'Stop'
Push-Location -LiteralPath $PSScriptRoot
try {
    & .\Build.ps1
    & .\tests\EftPosition.Tests.exe $FixtureRoot | Tee-Object -FilePath 'reports\tests.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Testy rdzenia nie przeszły.' }
    & .\tests\AutoNavigationTests.exe (Join-Path $PSScriptRoot 'assets') | Tee-Object -FilePath 'reports\navigation-tests.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Testy automatycznych pięter nie przeszły.' }
    & .\tests\TileMapTests.exe (Join-Path $FixtureRoot 'tiles') | Tee-Object -FilePath 'reports\tile-tests.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Testy ostrych kafelków nie przeszły.' }
    & .\EftPosition.VectorTests.exe (Join-Path $PSScriptRoot 'assets') (Join-Path $PSScriptRoot 'reports') | Tee-Object -FilePath 'reports\vector-tests.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Testy rysowania i długich linii nie przeszły.' }
    & .\EftPosition.UITests.exe (Join-Path $FixtureRoot 'gui') (Join-Path $PSScriptRoot 'reports\preview.png') | Tee-Object -FilePath 'reports\gui-tests.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Testy okna nie przeszły.' }
    & .\EftPosition.ExperienceTests.exe (Join-Path $FixtureRoot 'experience') (Join-Path $PSScriptRoot 'reports') | Tee-Object -FilePath 'reports\experience-tests.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Testy wnętrz i pełnego ekranu nie przeszły.' }
    & .\EftPosition.SmoothViewTests.exe (Join-Path $PSScriptRoot 'assets') | Tee-Object -FilePath 'reports\smooth-view-tests.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Testy płynnego przybliżania i przesuwania nie przeszły.' }
    & .\EftPosition.MarkerTests.exe (Join-Path $PSScriptRoot 'assets') (Join-Path $FixtureRoot 'markers') (Join-Path $PSScriptRoot 'reports\markers') | Tee-Object -FilePath 'reports\marker-tests.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Testy znaczników i filtrów nie przeszły.' }
    & .\EftPosition.PolishTests.exe (Join-Path $FixtureRoot 'polish') (Join-Path $PSScriptRoot 'reports\polish') | Tee-Object -FilePath 'reports\polish-tests.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Testy ustawień, usuwania zrzutów i pięter nie przeszły.' }
    & .\EftPosition.AppearanceTests.exe (Join-Path $FixtureRoot 'appearance') (Join-Path $PSScriptRoot 'reports\appearance') | Tee-Object -FilePath 'reports\appearance-tests.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Testy wyglądu i personalizacji nie przeszły.' }
    & .\EftPosition.CaptionTests.exe (Join-Path $FixtureRoot 'caption') (Join-Path $PSScriptRoot 'reports\caption') | Tee-Object -FilePath 'reports\caption-tests.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Testy paska okna nie przeszły.' }
    & .\EftPosition.RasterTests.exe (Join-Path $FixtureRoot 'raster') (Join-Path $PSScriptRoot 'reports\raster') | Tee-Object -FilePath 'reports\raster-tests.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Testy map PNG nie przeszły.' }
    & .\EftPosition.LanguageTests.exe (Join-Path $FixtureRoot 'language') (Join-Path $PSScriptRoot 'reports\language') | Tee-Object -FilePath 'reports\language-tests.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Testy języka i nazw ekstraktów nie przeszły.' }
    & .\EftPosition.OverlayTests.exe (Join-Path $FixtureRoot 'overlay') (Join-Path $PSScriptRoot 'reports\overlay') | Tee-Object -FilePath 'reports\overlay-tests.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Testy minimapy nie przeszły.' }
    & .\EftPosition.MapDetectionTests.exe (Join-Path $FixtureRoot 'map-detection') (Join-Path $PSScriptRoot 'reports\map-detection') | Tee-Object -FilePath 'reports\map-detection-tests.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Testy automatycznej mapy i skrótów nie przeszły.' }
    & .\EftPosition.UiRefreshTests.exe (Join-Path $FixtureRoot 'ui-refresh') (Join-Path $PSScriptRoot 'reports\ui-refresh') | Tee-Object -FilePath 'reports\ui-refresh-tests.txt'
    if ($LASTEXITCODE -ne 0) { throw 'UI refresh tests failed.' }
    & .\EftPosition.UiInteractionTests.exe (Join-Path $FixtureRoot 'ui-interaction') (Join-Path $PSScriptRoot 'reports\ui-interaction') | Tee-Object -FilePath 'reports\ui-interaction-tests.txt'
    if ($LASTEXITCODE -ne 0) { throw 'UI interaction tests failed.' }
    & .\EftPosition.ZoomPreviewTests.exe (Join-Path $PSScriptRoot 'assets') (Join-Path $FixtureRoot 'zoom-preview') | Tee-Object -FilePath 'reports\zoom-preview-tests.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Zoom preview tests failed.' }
    & .\EftPosition.RasterTests.exe (Join-Path $FixtureRoot 'raster') (Join-Path $FixtureRoot 'raster-reports') | Tee-Object -FilePath 'reports\raster-tests.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Raster tests failed.' }
    & .\EftPosition.LootTests.exe (Join-Path $PSScriptRoot 'assets') (Join-Path $FixtureRoot 'loot') | Tee-Object -FilePath 'reports\loot-tests.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Loot tests failed.' }
    & .\EftPosition.MapIconTests.exe (Join-Path $PSScriptRoot 'assets') (Join-Path $FixtureRoot 'map-icons') | Tee-Object -FilePath 'reports\map-icon-tests.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Map icon release tests failed.' }
} finally { Pop-Location }
