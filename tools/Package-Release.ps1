param([string]$OutputDirectory=(Join-Path $PSScriptRoot '..\artifacts'))
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
$version=(Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid VERSION'}
$exe=Join-Path $root 'EftPosition.exe'
if(!(Test-Path -LiteralPath $exe)){throw 'Build the application before packaging'}
if([Diagnostics.FileVersionInfo]::GetVersionInfo($exe).ProductVersion -ne $version){throw 'Executable version does not match VERSION'}
$runtime=Join-Path $OutputDirectory ('EFT-Position-'+$version)
$source=Join-Path $OutputDirectory ('EFT-Map-'+$version)
if((Test-Path -LiteralPath $runtime) -or (Test-Path -LiteralPath $source)){throw 'Use a new output directory'}
New-Item -ItemType Directory -Force -Path $runtime,$source | Out-Null
foreach($folder in @('assets','lib','licenses','sample','docs')){
    Copy-Item -LiteralPath (Join-Path $root $folder) -Destination $runtime -Recurse
}
foreach($name in @('EftPosition.exe','EftPosition.exe.config','LICENSE.txt','THIRD-PARTY-NOTICES.md','README.md','RAPORT.md','RELEASE-NOTES.md','VERSION','Przyklad.cmd','Uruchom.cmd')){
    Copy-Item -LiteralPath (Join-Path $root $name) -Destination $runtime
}
foreach($folder in @('src','tests','tools','assets','lib','licenses','sample','.github','docs')){
    Copy-Item -LiteralPath (Join-Path $root $folder) -Destination $source -Recurse
}
foreach($name in @('Build.ps1','EftPosition.csproj','EftPosition.exe.config','LICENSE.txt','THIRD-PARTY-NOTICES.md','README.md','RAPORT.md','PUBLICATION-NOTES.md','RELEASE-NOTES.md','VERSION','Przyklad.cmd','Uruchom.cmd','Zbuduj.cmd','Testuj.cmd','Testuj.ps1','.gitignore')){
    Copy-Item -LiteralPath (Join-Path $root $name) -Destination $source
}
# Include only the reviewed report for this release, never the entire local reports folder.
$publicReport=Join-Path $root ('reports\'+$version)
foreach($destination in @($runtime,$source)){
    $reportRoot=Join-Path $destination 'reports'
    New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null
    if(Test-Path -LiteralPath $publicReport){Copy-Item -LiteralPath $publicReport -Destination $reportRoot -Recurse}
}
# Source archive contains only source files, including after a local build.
foreach($file in Get-ChildItem -LiteralPath $source -Recurse -File -Filter '*.exe'){
    $resolved=[IO.Path]::GetFullPath($file.FullName)
    if(!$resolved.StartsWith($source+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected source artifact path'}
    Remove-Item -LiteralPath $resolved
}
$appHash=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
$buildInfo=[ordered]@{version=$version;applicationSha256=$appHash;sourceRevision=$env:GITHUB_SHA;build='Built from the source distributed with this release'}
$buildInfo | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runtime 'BUILD-INFO.json') -Encoding UTF8
Add-Type -AssemblyName System.IO.Compression.FileSystem
$runtimeZip=Join-Path $OutputDirectory ('EFT-Position-Windows-'+$version+'.zip')
$sourceZip=Join-Path $OutputDirectory ('EFT-Map-kod-zrodlowy-'+$version+'.zip')
foreach($zip in @($runtimeZip,$sourceZip)){if(Test-Path -LiteralPath $zip){throw 'Archive already exists'}}
[IO.Compression.ZipFile]::CreateFromDirectory($runtime,$runtimeZip,[IO.Compression.CompressionLevel]::Optimal,$true)
[IO.Compression.ZipFile]::CreateFromDirectory($source,$sourceZip,[IO.Compression.CompressionLevel]::Optimal,$true)
$sums=@($runtimeZip,$sourceZip) | ForEach-Object {((Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant())+'  '+[IO.Path]::GetFileName($_)}
$sums | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Encoding ASCII
Write-Output ('Packaged '+$version+'; application SHA-256 '+$appHash)
