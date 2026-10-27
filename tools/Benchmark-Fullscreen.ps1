param([string]$Label='after',[string]$Output=(Join-Path $PSScriptRoot '..\reports\fullscreen-pan'))
$ErrorActionPreference='Stop'
$taskProject=Split-Path -Parent $PSScriptRoot
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskExecutable=Join-Path $taskProject 'EftPosition.FullscreenBenchmark.exe'
& $taskCompiler /nologo /target:exe /platform:x64 /optimize+ /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Core.dll ('/reference:'+(Join-Path $taskProject 'EftPosition.exe')) ('/out:'+$taskExecutable) (Join-Path $taskProject 'tests\FullscreenBenchmark.cs')
if($LASTEXITCODE -ne 0){throw 'Fullscreen benchmark compilation failed'}
Copy-Item -LiteralPath (Join-Path $taskProject 'EftPosition.exe.config') -Destination ($taskExecutable+'.config')
& $taskExecutable $Output $Label
if($LASTEXITCODE -ne 0){throw 'Fullscreen benchmark failed'}
