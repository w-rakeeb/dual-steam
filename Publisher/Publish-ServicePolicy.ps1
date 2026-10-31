param([ValidateSet('keep','active','paused','shutdown')][string]$State='keep',[string]$Announcement,[string]$KeyFile,[string]$BootstrapVersion)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$file=Join-Path $repo 'service-policy.json'
$key=if($KeyFile){[IO.File]::ReadAllText($KeyFile)}else{$env:DUAL_STEAM_POLICY_KEY}
if(!$key){throw 'Publisher signing key is not configured'}
$rsa=[Security.Cryptography.RSACryptoServiceProvider]::new();$rsa.PersistKeyInCsp=$false
try{
 $rsa.FromXmlString($key);$key=$null
 $previous=$null
 if(Test-Path -LiteralPath $file){$envelope=Get-Content -LiteralPath $file -Raw -Encoding UTF8|ConvertFrom-Json;$bytes=[Convert]::FromBase64String($envelope.Payload);if(!$rsa.VerifyData($bytes,[Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'),[Convert]::FromBase64String($envelope.Signature))){throw 'Current policy signature is invalid; refusing to sign an untrusted transition'};$previous=[Text.Encoding]::UTF8.GetString($bytes)|ConvertFrom-Json}
 $status=if($State -eq 'keep'){if($previous){$previous.State}else{'active'}}else{$State}
 $message=if($PSBoundParameters.ContainsKey('Announcement')){$Announcement}elseif($previous){$previous.Announcement}else{'Dual Steam is available. Public updates are required at the next full startup.'}
 if($status -ne 'active' -and !$message){$message=if($status -eq 'paused'){'The service is temporarily paused. Please check back later.'}else{'The publisher has closed this service.'}}
 if($message.Length -gt 2000){throw 'Announcement must be 2000 characters or fewer'}
 $required=$BootstrapVersion;$asset=$null
 if(!$BootstrapVersion){
  $headers=@{Accept='application/vnd.github+json';'User-Agent'='DualSteam-Publisher'};if($env:GH_TOKEN){$headers.Authorization='Bearer '+$env:GH_TOKEN}
  $release=Invoke-RestMethod -Uri 'https://api.github.com/repos/w-rakeeb/dual-steam/releases/latest' -Headers $headers
  if($release.draft -or $release.prerelease -or $release.tag_name -notmatch '^v[0-9]+\.[0-9]+\.[0-9]+$'){throw 'No stable public release is ready'}
  $required=$release.tag_name.Substring(1);$zip=@($release.assets|Where-Object name -eq 'Dual-Steam-Windows.zip')
  if($zip.Count -ne 1 -or $zip[0].digest -notmatch '^sha256:[a-f0-9]{64}$' -or $zip[0].size -le 0 -or $zip[0].size -gt 52428800 -or $zip[0].browser_download_url -ne ('https://github.com/w-rakeeb/dual-steam/releases/download/'+$release.tag_name+'/Dual-Steam-Windows.zip')){throw 'A verified release ZIP must be uploaded before its policy can be published'}
  $asset=[ordered]@{Tag=$release.tag_name;Url=$zip[0].browser_download_url;Sha256=$zip[0].digest.Substring(7);Size=$zip[0].size}
  if($previous -and [version]$previous.RequiredVersion -gt [version]$required){$required=$previous.RequiredVersion;$asset=$previous.Release}
 }
 if($required -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$'){throw 'Invalid required version'}
 $payload=[ordered]@{Schema=1;State=$status;Announcement=$message;RequiredVersion=$required;PublishedUtc=[DateTime]::UtcNow.ToString('o');Release=$asset}|ConvertTo-Json -Depth 5 -Compress
 $bytes=[Text.Encoding]::UTF8.GetBytes($payload);$signature=$rsa.SignData($bytes,[Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'))
 $signed=[ordered]@{Payload=[Convert]::ToBase64String($bytes);Signature=[Convert]::ToBase64String($signature)}|ConvertTo-Json -Compress
 [IO.File]::WriteAllText($file,$signed,[Text.UTF8Encoding]::new($false))
 Write-Output ('Signed startup policy: '+$status+'; required version '+$required)
}finally{$rsa.Dispose();$key=$null}
