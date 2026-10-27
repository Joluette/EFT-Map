param([string]$Label='after',[string]$CacheDirectory,[string]$Output=(Join-Path $PSScriptRoot '..\reports\raster-performance'))
$ErrorActionPreference='Stop'
$taskProject=Split-Path -Parent $PSScriptRoot
if(!$CacheDirectory){throw 'Choose a cache containing the Labs, Labyrinth and Icebreaker base images'}
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskExecutable=Join-Path $taskProject 'EftPosition.RasterBenchmark.exe'
& $taskCompiler /nologo /target:exe /platform:x64 /optimize+ /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Core.dll ('/reference:'+(Join-Path $taskProject 'EftPosition.exe')) ('/out:'+$taskExecutable) (Join-Path $taskProject 'tests\RasterBenchmark.cs')
if($LASTEXITCODE -ne 0){throw 'Raster benchmark compilation failed'}
Copy-Item -LiteralPath (Join-Path $taskProject 'EftPosition.exe.config') -Destination ($taskExecutable+'.config')
& $taskExecutable (Join-Path $taskProject 'assets') $CacheDirectory $Output $Label
if($LASTEXITCODE -ne 0){throw 'Raster benchmark failed'}
