$ErrorActionPreference='Stop'
 $project=Split-Path $PSScriptRoot -Parent
$settings=Get-Content -LiteralPath "$project\Data\settings.json" -Raw -Encoding UTF8 | ConvertFrom-Json
$native=$settings.NativeRoot
if(Test-Path -LiteralPath "$project\Data\Removal\request.json"){throw 'A removal is unfinished. Open Settings and finish removing the setup before creating it again.'}
if(!(Test-Path -LiteralPath "$native\Client\steam.exe")){throw 'The Second Steam client is missing. Finish removal in Settings, then set up Second Steam again.'}
New-Item -ItemType Directory -Path "$native\Evidence" -Force | Out-Null
$account=Get-Content -LiteralPath "$project\Data\Native\account.json" -Raw -Encoding UTF8 | ConvertFrom-Json
if([Security.Principal.WindowsIdentity]::GetCurrent().User.Value -ne $account.IssuerSID){throw 'Use the Windows account that created this profile'}
$password=(Get-Content -LiteralPath "$project\Data\Native\password.dpapi" -Raw -Encoding UTF8).Trim() | ConvertTo-SecureString
Add-Type -Path "$PSScriptRoot\DesktopAccess.cs"
[DesktopAccess]::Grant($account.SID)
$info=New-Object Diagnostics.ProcessStartInfo
$sha=[Security.Cryptography.SHA256]::Create();try{$suffix=([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($native.ToLowerInvariant())))).Replace('-','').Substring(0,12).ToLowerInvariant()}finally{$sha.Dispose()}
$pipeName=if($account.User -eq 'DualSteamLocal'){'WrakeebDualSteamNativeControl'}else{'WrakeebDualSteamNativeControl_'+$suffix}
$public=@{Home=$project;Root=$native;Profile=$account.Profile;SID=$account.SID;IssuerSID=$account.IssuerSID;User=$account.User;Pipe=$pipeName}
$public | ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath "$project\Data\controller.json"
$info.FileName="$native\Client\NativeAccountControl.exe";$info.Arguments='broker "'+$project+'"'
$info.WorkingDirectory="$native\Client";$info.UseShellExecute=$false;$info.LoadUserProfile=$true
$info.UserName=$account.User;$info.Domain=$env:COMPUTERNAME;$info.Password=$password
$process=[Diagnostics.Process]::Start($info)
$password.Dispose()
@{BrokerPID=$process.Id;User=$account.User} | ConvertTo-Json | Set-Content -Encoding UTF8 "$native\Evidence\broker.json"
