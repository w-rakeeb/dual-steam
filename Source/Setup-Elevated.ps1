param([Parameter(Mandatory=$true)][string]$Request,[switch]$Remove)
if($Remove){
$ErrorActionPreference='Stop'
$env:PSModulePath=Join-Path $PSHOME 'Modules'
$project=Split-Path $PSScriptRoot -Parent
$state=Join-Path $project 'Data\Removal'
$result=Join-Path $state 'result.json'
$removeMutex=$null;$removeHeld=$false;$safeResult=$false
try{
 [void][Reflection.Assembly]::LoadFrom((Join-Path $project 'Dual Steam.exe'))
 [SettingsTransfer]::NoLinks($state);[SettingsTransfer]::NoLinks($result);$safeResult=$true
 if([IO.Path]::GetFullPath($Request) -ne (Join-Path $state 'request.json')){throw 'Unexpected removal request path'}
 [SettingsTransfer]::NoLinks($Request)
 $r=Get-Content -LiteralPath $Request -Raw -Encoding UTF8 | ConvertFrom-Json
 $p=New-Object RemovalPlan
 foreach($name in 'Home','Root','Profile','MainSteam','User','SID','IssuerSID','OriginalProfileBackup','Libraries'){$p.$name=$r.$name}
 $index=Join-Path (Split-Path $r.MainSteam -Parent) 'steamapps\libraryfolders.vdf'
 if(Test-Path -LiteralPath $index){if((Get-Item -LiteralPath $index).Length -gt 1048576){throw 'Main Steam library index is too large'};$p.Libraries=@($p.Libraries)+@([LibraryCatalog]::ReadPaths([IO.File]::ReadAllText($index)))}
 [RemovalCore]::Validate($p,$r.IssuerSID)
 $sha=[Security.Cryptography.SHA256]::Create();try{$lock=([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($r.Root.TrimEnd('\').ToLowerInvariant())))).Replace('-','').Substring(0,16)}finally{$sha.Dispose()}
 $removeMutex=New-Object Threading.Mutex($false,('Global\DualSteamSetup_'+$lock));try{$removeHeld=$removeMutex.WaitOne(0)}catch [Threading.AbandonedMutexException]{$removeHeld=$true};if(!$removeHeld){throw 'Setup or removal for this folder is already running'}
 if($r.Home -ne $project){throw 'Removal request belongs to another app folder'}
 $account=Get-Content -LiteralPath (Join-Path $project 'Data\Native\account.json') -Raw -Encoding UTF8 | ConvertFrom-Json
 if($account.SID -ne $r.SID -or $account.User -ne $r.User -or $account.Profile -ne $r.Profile -or $account.IssuerSID -ne $r.IssuerSID -or [string]$account.OriginalProfileBackup -ne [string]$r.OriginalProfileBackup -or $account.ManagedBy -notlike 'Dual Steam*'){throw 'The managed account record changed'}
 $user=Get-LocalUser -Name $r.User -ErrorAction SilentlyContinue
 if($user -and $user.SID.Value -ne $r.SID){throw 'An unrelated Windows account has this name'}
 $marker='HKLM:\SOFTWARE\DualSteamManagedRemoval\'+$r.SID
 $trusted=if(Test-Path -LiteralPath $marker){Get-ItemProperty -LiteralPath $marker}else{$null}
 if($trusted -and ($trusted.Root -ne $r.Root -or $trusted.Profile -ne $r.Profile -or $trusted.User -ne $r.User -or $trusted.IssuerSID -ne $r.IssuerSID)){throw 'The protected removal recovery record does not match this setup'}
 if(!$user -and !$trusted){throw 'The managed Windows account is missing. No unverified folder was removed.'}
 $issuerKey='HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\'+$r.IssuerSID
 $issuerProfile=[Environment]::ExpandEnvironmentVariables((Get-ItemProperty -LiteralPath $issuerKey).ProfileImagePath).TrimEnd('\')
 if($issuerProfile -eq $r.Root -or $issuerProfile.StartsWith($r.Root+'\',[StringComparison]::OrdinalIgnoreCase) -or $issuerProfile -eq $r.OriginalProfileBackup){throw 'Removal would affect the setup owner profile'}
 $admins=Get-LocalGroup -SID 'S-1-5-32-544'
 if($user -and @(Get-LocalGroupMember -Group $admins | Where-Object {$_.SID.Value -eq $r.SID}).Count){throw 'The managed account is now an administrator; review it before removal'}
 $processes=Get-CimInstance Win32_Process
 foreach($process in $processes){if($process.ExecutablePath -and $process.ExecutablePath.StartsWith($r.Root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'A program still uses the second-client folder. Close it before removal'};$owner=Invoke-CimMethod -InputObject $process -MethodName GetOwnerSid -ErrorAction SilentlyContinue;if($owner.Sid -eq $r.SID){throw 'A process still uses the second Windows account. Close its programs, or restart Windows, before removal'}}
 $profile=Get-CimInstance Win32_UserProfile | Where-Object {$_.SID -eq $r.SID}
 if(!$profile -and !$trusted){throw 'Windows could not verify the managed profile location. No folder was removed.'}
 if($profile){if($profile.Special -or [IO.Path]::GetFullPath($profile.LocalPath).TrimEnd('\') -ne $r.Profile){throw 'The managed Windows profile has an unexpected location'};$limit=(Get-Date).AddSeconds(20);while($profile.Loaded -and (Get-Date) -lt $limit){Start-Sleep -Milliseconds 500;$profile=Get-CimInstance Win32_UserProfile | Where-Object {$_.SID -eq $r.SID}};if($profile.Loaded){throw 'Windows is still using the second profile. Restart Windows and retry removal.'}}
 if(!$trusted){New-Item -Path $marker -Force|Out-Null;$acl=New-Object Security.AccessControl.RegistrySecurity;$acl.SetAccessRuleProtection($true,$false);foreach($sid in 'S-1-5-18','S-1-5-32-544'){$identity=New-Object Security.Principal.SecurityIdentifier($sid);$rule=New-Object Security.AccessControl.RegistryAccessRule($identity,'FullControl','ContainerInherit','None','Allow');$acl.AddAccessRule($rule)};Set-Acl -LiteralPath $marker -AclObject $acl;foreach($name in 'Root','Profile','User','IssuerSID'){New-ItemProperty -LiteralPath $marker -Name $name -Value $r.$name -PropertyType String -Force|Out-Null}}
 if($profile){[RemovalCore]::UnlinkProfileLinks($r.Profile);Remove-CimInstance -InputObject $profile}
 if(Test-Path -LiteralPath ('Registry::HKEY_USERS\'+$r.SID)){throw 'Windows still has the second profile registry loaded. Restart Windows and retry.'}
 [RemovalCore]::DeleteTree($r.Root)
 if($r.OriginalProfileBackup){[RemovalCore]::DeleteTree($r.OriginalProfileBackup)}
 foreach($folder in @($project,(Join-Path $project 'Assets'),(Join-Path $project 'Data'),(Join-Path $project 'Tools'))){[SettingsTransfer]::NoLinks($folder);$acl=Get-Acl -LiteralPath $folder;foreach($rule in $acl.GetAccessRules($true,$false,[Security.Principal.SecurityIdentifier])){if($rule.IdentityReference.Value -eq $r.SID){$acl.RemoveAccessRuleSpecific($rule)}};Set-Acl -LiteralPath $folder -AclObject $acl}
 if($user){Remove-LocalUser -SID $user.SID}
 if(Get-LocalUser -Name $r.User -ErrorAction SilentlyContinue){throw 'The managed Windows account could not be removed'}
 @{Success=$true;AccountRemoved=$r.User;SID=$r.SID;RootRemoved=$r.Root}|ConvertTo-Json|Set-Content -LiteralPath $result -Encoding UTF8
 Remove-Item -LiteralPath $marker
 exit 0
}catch{
 if($safeResult){@{Success=$false;Error=$_.Exception.Message;Recovery='Keep Data/Native and Data/Removal for retry. Do not manually delete shared Steam libraries.'}|ConvertTo-Json|Set-Content -LiteralPath $result -Encoding UTF8}
 exit 1
}finally{if($removeMutex){if($removeHeld){$removeMutex.ReleaseMutex()};$removeMutex.Dispose()}}
}
$ErrorActionPreference='Stop'
$env:PSModulePath=Join-Path $PSHOME 'Modules'
$project=Split-Path $PSScriptRoot -Parent
$state=Join-Path $project 'Data\Native'
$result=Join-Path $state 'setup-result.json'
$setupMutex=$null;$held=$false
try {
 if([IO.Path]::GetFullPath($Request) -ne (Join-Path $state 'setup-request.json')){throw 'Unexpected setup request path'}
 $r=Get-Content -LiteralPath $Request -Raw -Encoding UTF8 | ConvertFrom-Json
 if($r.Home -ne $project -or $r.User -notmatch '^DualSteam_[a-f0-9]{8}$' -or $r.Pipe -notmatch '^DualSteamSetup_[a-f0-9]{32}$'){throw 'Invalid setup request'}
 $root=[IO.Path]::GetFullPath($r.Root).TrimEnd('\')
 $hash=[Security.Cryptography.SHA256]::Create();try{$lockName=([BitConverter]::ToString($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($root.ToLowerInvariant())))).Replace('-','').Substring(0,16)}finally{$hash.Dispose()}
 $setupMutex=New-Object Threading.Mutex($false,('Global\DualSteamSetup_'+$lockName));try{$held=$setupMutex.WaitOne(0)}catch [Threading.AbandonedMutexException]{$held=$true};if(!$held){throw 'Setup for this data folder is already running. Wait for it to finish.'}
 $main=Split-Path ([IO.Path]::GetFullPath($r.Steam)) -Parent
 if($root.Length -le 3 -or $root.StartsWith('\\') -or $root -eq $main -or $root.StartsWith($main+'\',[StringComparison]::OrdinalIgnoreCase) -or $main.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase) -or $root -eq $project -or $project.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe setup location'}
 for($p=$root;$p;$p=Split-Path $p -Parent){if(Test-Path -LiteralPath $p){if((Get-Item -LiteralPath $p -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked setup paths are not supported'}}}
 $journalFile=Join-Path $state 'setup-journal.json'
 $journal=if(Test-Path -LiteralPath $journalFile){Get-Content -LiteralPath $journalFile -Raw -Encoding UTF8|ConvertFrom-Json}else{$null}
 $existing=Get-LocalUser -Name $r.User -ErrorAction SilentlyContinue
 $resume=$existing -and $journal -and $journal.CreatedSID -eq $existing.SID.Value -and $journal.Root -eq $root -and $journal.IssuerSID -eq $r.IssuerSID
 if((Test-Path -LiteralPath $root) -and @(Get-ChildItem -LiteralPath $root -Force).Count -and !$resume){throw 'The second-client folder must be empty'}
 if($existing -and !$resume){throw 'A Windows account with this name already exists and is not owned by this setup. It has not been changed.'}
 $signature=Get-AuthenticodeSignature -LiteralPath $r.Steam
 if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Valve'){throw 'The selected Steam executable does not have a valid Valve signature'}
 $pipe=New-Object IO.Pipes.NamedPipeClientStream('.', $r.Pipe, [IO.Pipes.PipeDirection]::In)
 try{$pipe.Connect(15000);$reader=New-Object IO.StreamReader($pipe);$password=$reader.ReadLine()}finally{$pipe.Dispose()}
 if(!$password -or $password.Length -lt 32){throw 'Secure setup credential was not received'}
 $secure=ConvertTo-SecureString $password -AsPlainText -Force;$password=$null
 if($resume){$user=$existing}else{$user=New-LocalUser -Name $r.User -Password $secure -FullName 'Dual Steam profile' -Description 'Dual Steam background client' -PasswordNeverExpires -UserMayNotChangePassword;$group=Get-LocalGroup -SID 'S-1-5-32-545';Add-LocalGroupMember -Group $group -Member $user}
 @{CreatedSID=$user.SID.Value;IssuerSID=$r.IssuerSID;Root=$root;Stage='account created'}|ConvertTo-Json|Set-Content -Encoding UTF8 (Join-Path $state 'setup-journal.json')
 Add-Type @'
using System;using System.Text;using System.Runtime.InteropServices;public static class ProfileSetup{[DllImport("userenv.dll",CharSet=CharSet.Unicode)]public static extern int CreateProfile(string sid,string user,StringBuilder path,uint size);}
'@
 $profile=Join-Path $root 'Windows Profile';$client=Join-Path $root 'Client';$evidence=Join-Path $root 'Evidence'
 $key='HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\'+$user.SID.Value
 if(!(Test-Path $key)){
  $generated=New-Object Text.StringBuilder 1024;$hr=[ProfileSetup]::CreateProfile($user.SID.Value,$r.User,$generated,1024)
  if($hr -ne 0){
   Add-Type -Path (Join-Path $PSScriptRoot 'DesktopAccess.cs');[DesktopAccess]::Grant($user.SID.Value)
   $start=New-Object Diagnostics.ProcessStartInfo;$start.FileName=Join-Path $env:WINDIR 'System32\cmd.exe';$start.Arguments='/d /c exit 0';$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.UserName=$r.User;$start.Domain=$env:COMPUTERNAME;$start.Password=$secure;$start.LoadUserProfile=$true
   $bootstrap=[Diagnostics.Process]::Start($start);if(!$bootstrap.WaitForExit(30000)){throw 'Windows is still initializing the new profile. Try setup again after it finishes.'};if($bootstrap.ExitCode -ne 0){throw 'Windows could not initialize the new account profile.'}
  }
 }
 $secure.Dispose()
 $source=(Get-ItemProperty $key).ProfileImagePath
 $initial=if($journal -and $journal.InitialProfile){$journal.InitialProfile}else{$source}
 @{CreatedSID=$user.SID.Value;IssuerSID=$r.IssuerSID;Root=$root;InitialProfile=$initial;Stage='profile initialized'}|ConvertTo-Json|Set-Content -Encoding UTF8 $journalFile
 $limit=(Get-Date).AddSeconds(30)
 while((Get-CimInstance Win32_UserProfile | Where-Object SID -eq $user.SID.Value).Loaded -and (Get-Date) -lt $limit){Start-Sleep -Milliseconds 500}
 if((Get-CimInstance Win32_UserProfile | Where-Object SID -eq $user.SID.Value).Loaded){throw 'Windows is still releasing the new profile. Try setup again after it finishes.'}
 if($source -ne $profile -and (Split-Path $source -Leaf) -notlike ($r.User+'*')){throw 'Generated profile containment failed'}
 New-Item -ItemType Directory -Path $profile,$client,$evidence -Force|Out-Null
 if($source -ne $profile){& robocopy.exe $source $profile /E /COPYALL /XJ /B /R:0 /W:0 /NFL /NDL /NJH /NJS /NP|Out-Null;if($LASTEXITCODE -ge 8){throw 'Profile copy could not finish'}}
 if(!(Test-Path -LiteralPath (Join-Path $profile 'NTUSER.DAT'))){throw 'The new profile registry hive is missing'}
 Set-ItemProperty ('HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\'+$user.SID.Value) -Name ProfileImagePath -Value $profile
 foreach($folder in @($profile,$client,$evidence)){
  & icacls.exe $folder /grant ('*'+$user.SID.Value+':(OI)(CI)F') /T /C /Q|Out-Null
  if($LASTEXITCODE -ne 0){throw 'The new profile permissions could not be applied'}
 }
 foreach($folder in @($project,(Join-Path $project 'Assets'),(Join-Path $project 'Data'),(Join-Path $project 'Tools'))){
  & icacls.exe $folder /grant ('*'+$user.SID.Value+':(OI)(CI)RX') /C /Q|Out-Null
  if($LASTEXITCODE -ne 0){throw 'The controller could not be granted read access'}
 }
 Copy-Item -LiteralPath $r.Steam -Destination (Join-Path $client 'steam.exe')
 Copy-Item -LiteralPath (Join-Path $project 'Tools\NativeAccountControl.exe') -Destination (Join-Path $client 'NativeAccountControl.exe')
 @{User=$r.User;SID=$user.SID.Value;IssuerSID=$r.IssuerSID;Profile=$profile;ManagedBy='Dual Steam 3';OriginalProfileBackup=$initial}|ConvertTo-Json|Set-Content -Encoding UTF8 (Join-Path $state 'account.json')
 @{Success=$true;Profile=$profile;WindowsProfileBootstrapBackup=$initial;Account=$r.User}|ConvertTo-Json|Set-Content -Encoding UTF8 $result
 exit 0
}catch{
 @{Success=$false;Error=$_.Exception.Message;Recovery='Main Steam was not modified. Keep the setup journal and credentials for recovery; do not retry into the same folder until the recorded partial setup is reviewed.'}|ConvertTo-Json|Set-Content -Encoding UTF8 $result
 exit 1
}finally{if($setupMutex){if($held){$setupMutex.ReleaseMutex()};$setupMutex.Dispose()}}
