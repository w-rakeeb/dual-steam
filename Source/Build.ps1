param([switch]$AppOnly)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpf=Join-Path $framework 'WPF'
New-Item -ItemType Directory -Path "$project\Tools" -Force | Out-Null
if(!$AppOnly){
 & "$framework\csc.exe" /nologo /codepage:65001 /target:winexe /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "/out:$project\Tools\NativeAccountControl.exe" "$PSScriptRoot\Native\NativeAccountControl.cs" "$PSScriptRoot\AssemblyInfo.cs" "$PSScriptRoot\Native\SteamTaskbarIdentity.cs" "$PSScriptRoot\Shared\SettingsTransfer.cs" "$PSScriptRoot\Shared\NativePaths.cs" "$PSScriptRoot\Shared\ServicePolicy.cs" "$PSScriptRoot\Shared\ManagerSessionHost.cs"
 if($LASTEXITCODE -ne 0){throw 'Native helper build failed'}
}
$sources=Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | Select-Object -ExpandProperty FullName
& "$framework\csc.exe" /nologo /codepage:65001 /target:winexe /platform:x64 /main:App "/win32manifest:$project\Assets\app.manifest" "/win32icon:$project\Assets\dual-steam.ico" "/out:$project\Dual Steam.exe" /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Security.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll "/reference:$wpf\WindowsBase.dll" "/reference:$wpf\PresentationCore.dll" "/reference:$wpf\PresentationFramework.dll" "/reference:$framework\System.Xaml.dll" $sources "$PSScriptRoot\Shared\RemovalCore.cs" "$PSScriptRoot\Shared\SettingsTransfer.cs" "$PSScriptRoot\Shared\NativePaths.cs" "$PSScriptRoot\Shared\ServicePolicy.cs" "$PSScriptRoot\Shared\ManagerSessionHost.cs" "$PSScriptRoot\Native\SteamTaskbarIdentity.cs"
if($LASTEXITCODE -ne 0){throw 'Build failed'}
'<?xml version="1.0"?><configuration><startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8"/></startup></configuration>' | Set-Content -LiteralPath "$project\Dual Steam.exe.config"

& "$framework\csc.exe" /nologo /codepage:65001 /target:winexe /main:DualSteam.UpdaterProgram /reference:System.Core.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/out:$project\Dual Steam Updater.exe" "$PSScriptRoot\UpdateCore.cs" "$PSScriptRoot\UpdateService.cs" "$PSScriptRoot\UpdaterMain.cs" "$PSScriptRoot\AssemblyInfo.cs"
if($LASTEXITCODE -ne 0){throw 'Updater build failed'}