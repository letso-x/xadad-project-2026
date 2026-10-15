<#
Start the local QMS API before running the MAUI Windows Machine target.
Leave this terminal running. Uses existing API User Secrets and HTTPS dev certificate.
Does not build or launch any mobile target, reset data, or change authentication.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$apiProject = Join-Path $PSScriptRoot '../mobile/MzukuluQMS.Api/MzukuluQMS.Api.csproj'
Write-Host 'Starting the QMS API at https://localhost:7044. Leave this terminal running.'
Write-Host 'Then run MzukuluQMSMobile using Windows Machine in Visual Studio.'
& dotnet run --project $apiProject --no-launch-profile --no-restore -- --environment Development --urls https://localhost:7044
if ($LASTEXITCODE -ne 0) { throw "The QMS API exited with code $LASTEXITCODE. Check the error above before starting the Windows app." }
