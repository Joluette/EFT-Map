param([string]$Output=(Join-Path $PSScriptRoot '..\reports\loot-performance'))
$ErrorActionPreference='Stop'
$eftProject=Split-Path -Parent $PSScriptRoot
$eftCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$eftExe=Join-Path $eftProject 'EftPosition.LootBenchmark.exe'
& $eftCompiler /nologo /target:exe /platform:x64 /optimize+ /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Core.dll ('/reference:'+(Join-Path $eftProject 'EftPosition.exe')) ('/out:'+$eftExe) (Join-Path $eftProject 'tests\LootBenchmark.cs')
if($LASTEXITCODE -ne 0){throw 'Loot benchmark compilation failed'}
Copy-Item -LiteralPath (Join-Path $eftProject 'EftPosition.exe.config') -Destination ($eftExe+'.config') -Force
& $eftExe $Output
if($LASTEXITCODE -ne 0){throw 'Loot benchmark failed'}
