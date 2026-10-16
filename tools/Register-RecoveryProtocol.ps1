# Manual Windows unpackaged development registration. Not run by app startup or Codex.
param([Parameter(Mandatory=$true)][string]$ExecutablePath)
$resolved=(Resolve-Path -LiteralPath $ExecutablePath -ErrorAction Stop).Path
if([IO.Path]::GetFileName($resolved) -ne 'MzukuluQMSMobile.exe'){throw 'Select the built MzukuluQMSMobile.exe.'}
$key='HKCU:\Software\Classes\mzukuluqms'
$command='"'+$resolved+'" "%1"'
if(Test-Path "$key\shell\open\command"){
 $existing=(Get-Item -LiteralPath "$key\shell\open\command").GetValue('')
 if($existing -ne $command){throw 'A different recovery URI handler already exists. Review it manually before changing registration.'}
}
New-Item -Path $key -Force | Out-Null
Set-Item -LiteralPath $key -Value 'URL:Mzukulu QMS recovery'
New-ItemProperty -LiteralPath $key -Name 'URL Protocol' -Value '' -PropertyType String -Force | Out-Null
New-Item -Path "$key\shell\open\command" -Force | Out-Null
Set-Item -LiteralPath "$key\shell\open\command" -Value $command
Write-Output 'Registered the current-user mzukuluqms URI handler. No cloud settings changed.'