param([switch]$RemoveSecond)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$desktop=[Environment]::GetFolderPath('DesktopDirectory')
$shell=New-Object -ComObject WScript.Shell
[void][Reflection.Assembly]::LoadFrom((Join-Path $project 'Dual Steam.exe'))
foreach($entry in @(@('Dual Steam','','dual-steam.ico'),@('Second Steam','--second','second-steam.ico'))){
 $shortcut=$shell.CreateShortcut((Join-Path $desktop ($entry[0]+'.lnk')))
 if($RemoveSecond){if($entry[1] -eq '--second' -and $shortcut.TargetPath -eq (Join-Path $project 'Dual Steam.exe') -and $shortcut.Arguments -eq '--second'){Remove-Item -LiteralPath (Join-Path $desktop ($entry[0]+'.lnk'))};continue}
 $shortcut.TargetPath=Join-Path $project 'Dual Steam.exe'
 $shortcut.Arguments=$entry[1]
 $shortcut.WorkingDirectory=$project
 $shortcut.IconLocation=(Join-Path $project ('Assets\'+$entry[2]))+',0'
 $shortcut.Description=if($entry[1]){'Open or restore Second Steam through Dual Steam'}else{'Manage two independent Steam logins'}
 $shortcut.Save()
 $identity=if($entry[1]){[DualSteamNative.SteamTaskbarIdentity]::SecondSteamId}else{[DualSteamNative.SteamTaskbarIdentity]::ManagerId}
 [DualSteamNative.SteamTaskbarIdentity]::SetShortcut((Join-Path $desktop ($entry[0]+'.lnk')),$identity)
}
$pinned=Join-Path $env:APPDATA 'Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar'
if(Test-Path -LiteralPath $pinned){foreach($file in Get-ChildItem -LiteralPath $pinned -Filter '*.lnk'){
 $link=$shell.CreateShortcut($file.FullName)
 if($link.TargetPath -eq (Join-Path $project 'Dual Steam.exe') -and ($link.Arguments -eq '' -or $link.Arguments -eq '--second')){
  if($RemoveSecond){if($link.Arguments -eq '--second'){Remove-Item -LiteralPath $file.FullName};continue}
  $identity=if($link.Arguments -eq '--second'){[DualSteamNative.SteamTaskbarIdentity]::SecondSteamId}else{[DualSteamNative.SteamTaskbarIdentity]::ManagerId}
  [DualSteamNative.SteamTaskbarIdentity]::SetShortcut($file.FullName,$identity)
 }
}}
