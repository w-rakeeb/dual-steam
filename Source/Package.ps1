$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$release=Join-Path $project 'Release'
$stage=Join-Path $release ('Windows-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force|Out-Null
[void][Reflection.Assembly]::LoadFrom((Join-Path $project 'Dual Steam.exe'))
foreach($relative in [DualSteam.UpdatePackage]::Files){
 $target=Join-Path $stage $relative
 New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force|Out-Null
 Copy-Item -LiteralPath (Join-Path $project $relative) -Destination $target
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=Join-Path $release 'Dual-Steam-Windows.zip'
if(Test-Path -LiteralPath $zip){Move-Item -LiteralPath $zip -Destination (Join-Path $release ('Previous-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff')+'.zip'))}
[IO.Compression.ZipFile]::CreateFromDirectory($stage,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
[DualSteam.UpdatePackage]::Validate($zip)|Out-Null
(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()+'  Dual-Steam-Windows.zip'|Set-Content (Join-Path $release 'SHA256SUMS.txt')
Write-Output $zip
