$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
foreach($suite in 'CoreTests','SettingsTests','MaintenanceTests','PolicyTests','UpdateRecoveryTests'){
$test=Join-Path $project ($suite+'-'+[guid]::NewGuid().ToString('N')+'.exe')
try{
 & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /codepage:65001 /target:exe /reference:System.Core.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/reference:$project\Dual Steam.exe" "/out:$test" "$PSScriptRoot\$suite.cs"
 if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
 & $test
 if($LASTEXITCODE -ne 0){throw 'Core tests failed'}
}finally{if(Test-Path -LiteralPath $test){Remove-Item -LiteralPath $test}}
}
