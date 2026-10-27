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
} finally { Pop-Location }
