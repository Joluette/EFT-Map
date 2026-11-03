param([string]$Label='after',[Parameter(Mandatory=$true)][string]$CacheDirectory,[string]$Output=(Join-Path $PSScriptRoot '..\reports\zoom'),[switch]$AllMaps,[switch]$Cadence)
$ErrorActionPreference='Stop'
$eftProject=Split-Path -Parent $PSScriptRoot
$eftCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$eftName=if($Cadence){'ZoomCadenceBenchmark'}else{'ZoomWindowBenchmark'}
$eftExecutable=Join-Path $eftProject ('EftPosition.'+$eftName+'.exe')
& $eftCompiler /nologo /target:exe /platform:x64 /optimize+ /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Core.dll ('/reference:'+(Join-Path $eftProject 'EftPosition.exe')) ('/out:'+$eftExecutable) (Join-Path $eftProject ('tests\'+$eftName+'.cs'))
if($LASTEXITCODE -ne 0){throw 'Zoom benchmark compilation failed'}
Copy-Item -LiteralPath (Join-Path $eftProject 'EftPosition.exe.config') -Destination ($eftExecutable+'.config') -Force
$eftAssets=Join-Path $eftProject 'assets'
if($Cadence){& $eftExecutable $Output $Label $eftAssets $CacheDirectory}
elseif($AllMaps){& $eftExecutable $CacheDirectory $Output $Label all $eftAssets}
else{& $eftExecutable $CacheDirectory $Output $Label}
if($LASTEXITCODE -ne 0){throw 'Zoom benchmark failed'}
