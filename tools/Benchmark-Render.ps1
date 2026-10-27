param(
    [string]$Project=(Join-Path $PSScriptRoot '..'),
    [string]$Output=(Join-Path $PSScriptRoot '..\reports\render-benchmark.csv'),
    [string]$VectorSource
)
$ErrorActionPreference='Stop'
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskRefs=@(Get-ChildItem -LiteralPath (Join-Path $Project 'lib') -Filter '*.dll' | ForEach-Object { '/reference:'+ $_.FullName })
$taskExecutable=Join-Path $Project 'EftPosition.RenderBenchmark.exe'
if (!$VectorSource) { $VectorSource=Join-Path $Project 'src\VectorMap.cs' }
& $taskCompiler /nologo /target:exe /platform:x64 /optimize+ /reference:System.Drawing.dll $taskRefs ('/out:'+ $taskExecutable) $VectorSource (Join-Path $Project 'src\SvgPathCanonicalizer.cs') (Join-Path $Project 'tests\RenderBenchmark.cs')
if ($LASTEXITCODE -ne 0) { throw 'Benchmark compilation failed.' }
Copy-Item -LiteralPath (Join-Path $Project 'EftPosition.exe.config') -Destination ($taskExecutable+'.config')
& $taskExecutable (Join-Path $Project 'assets') $Output
if ($LASTEXITCODE -ne 0) { throw 'Benchmark failed.' }
