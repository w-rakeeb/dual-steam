$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$desktop=[Environment]::GetFolderPath('DesktopDirectory')
$shell=New-Object -ComObject WScript.Shell
foreach($entry in @(@('Dual Steam','','dual-steam.ico'),@('Second Steam','--second','second-steam.ico'))){
 $shortcut=$shell.CreateShortcut((Join-Path $desktop ($entry[0]+'.lnk')))
 $shortcut.TargetPath=Join-Path $project 'Dual Steam.exe'
 $shortcut.Arguments=$entry[1]
 $shortcut.WorkingDirectory=$project
 $shortcut.IconLocation=(Join-Path $project ('Assets\'+$entry[2]))+',0'
 $shortcut.Description=if($entry[1]){'Open or restore Second Steam through Dual Steam'}else{'Manage two independent Steam logins'}
 $shortcut.Save()
}
