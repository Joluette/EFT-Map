$ErrorActionPreference = 'Stop'
Push-Location -LiteralPath $PSScriptRoot
try {
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Potrzebny jest Windows x64 z .NET Framework 4.8.' }
$sources = @(Get-ChildItem -LiteralPath 'src' -Filter '*.cs' | ForEach-Object FullName)
$libraryRefs=@(Get-ChildItem -LiteralPath 'lib' -Filter '*.dll' | ForEach-Object { '/reference:'+ $_.FullName })
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /warn:4 /utf8output /codepage:65001 /out:EftPosition.exe /win32manifest:src\app.manifest /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll $libraryRefs $sources
if ($LASTEXITCODE -ne 0) { throw 'Kompilacja aplikacji nie powiodła się.' }
& $compiler /nologo /target:exe /platform:x64 /optimize+ /warn:4 /utf8output /codepage:65001 /out:tests\EftPosition.Tests.exe /reference:System.Web.Extensions.dll src\Core.cs src\FolderMonitor.cs src\Maps.cs tests\Tests.cs
if ($LASTEXITCODE -ne 0) { throw 'Kompilacja testów nie powiodła się.' }
& $compiler /nologo /target:exe /platform:x64 /optimize+ /warn:4 /utf8output /codepage:65001 /main:EftPosition.UITests /out:EftPosition.UITests.exe /win32manifest:src\app.manifest /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll $libraryRefs $sources tests\UITests.cs
if ($LASTEXITCODE -ne 0) { throw 'Kompilacja testów okna nie powiodła się.' }
& $compiler /nologo /target:exe /platform:x64 /optimize+ /warn:4 /utf8output /codepage:65001 /out:tests\MapAssetTests.exe /reference:System.Web.Extensions.dll /reference:System.Drawing.dll src\Core.cs src\Maps.cs src\MapAssets.cs tests\MapAssetTests.cs
if ($LASTEXITCODE -ne 0) { throw 'Kompilacja testów obrazów map nie powiodła się.' }
& $compiler /nologo /target:exe /platform:x64 /optimize+ /warn:4 /utf8output /codepage:65001 /out:tests\AutoNavigationTests.exe /reference:System.Web.Extensions.dll src\Core.cs src\Maps.cs src\AutoNavigation.cs tests\AutoNavigationTests.cs
if ($LASTEXITCODE -ne 0) { throw 'Kompilacja testów nawigacji nie powiodła się.' }
& $compiler /nologo /target:exe /platform:x64 /optimize+ /warn:4 /utf8output /codepage:65001 /out:tests\TileMapTests.exe /reference:System.Drawing.dll /reference:System.Web.Extensions.dll src\Core.cs src\Maps.cs src\TileMap.cs tests\TileMapTests.cs
if ($LASTEXITCODE -ne 0) { throw 'Kompilacja testów kafelków nie powiodła się.' }
Copy-Item -LiteralPath 'EftPosition.exe.config' -Destination 'EftPosition.UITests.exe.config' -Force
& $compiler /nologo /target:exe /platform:x64 /optimize+ /warn:4 /utf8output /codepage:65001 /out:EftPosition.VectorTests.exe /reference:System.Drawing.dll $libraryRefs src\VectorMap.cs src\SvgPathCanonicalizer.cs tests\VectorMapTests.cs
if ($LASTEXITCODE -ne 0) { throw 'Kompilacja testów geometrii wektorowej nie powiodła się.' }
Copy-Item -LiteralPath 'EftPosition.exe.config' -Destination 'EftPosition.VectorTests.exe.config' -Force
if(Test-Path -LiteralPath 'tests\ExperienceTests.cs') {
  & $compiler /nologo /target:exe /platform:x64 /optimize+ /warn:4 /utf8output /codepage:65001 /main:EftPosition.ExperienceTests /out:EftPosition.ExperienceTests.exe /win32manifest:src\app.manifest /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll $libraryRefs $sources tests\ExperienceTests.cs
  if ($LASTEXITCODE -ne 0) { throw 'Kompilacja testów nowych funkcji nie powiodła się.' }
  Copy-Item -LiteralPath 'EftPosition.exe.config' -Destination 'EftPosition.ExperienceTests.exe.config' -Force
}
Write-Host 'Gotowe: EftPosition.exe oraz tests\EftPosition.Tests.exe'
} finally { Pop-Location }
