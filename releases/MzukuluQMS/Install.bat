@echo off
REM ============================================================
REM  Mzukulu QMS - installer
REM  Copies the application into your user profile and creates
REM  a Start Menu and Desktop shortcut. No admin rights needed.
REM ============================================================

setlocal
set "APPNAME=Mzukulu QMS"
set "TARGET=%LOCALAPPDATA%\MzukuluQMS"
set "SRC=%~dp0"

echo.
echo Installing %APPNAME% to:
echo   %TARGET%
echo.

if not exist "%TARGET%" mkdir "%TARGET%"

REM Copy everything except this installer script.
robocopy "%SRC%." "%TARGET%" /E /XF "Install.bat" "Uninstall.bat" >nul

REM Create Desktop + Start Menu shortcuts via PowerShell.
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ws = New-Object -ComObject WScript.Shell;" ^
  "$exe = Join-Path '%TARGET%' 'MzuApplication.exe';" ^
  "$ico = Join-Path '%TARGET%' 'app.ico';" ^
  "$desktop = [Environment]::GetFolderPath('Desktop');" ^
  "$sc = $ws.CreateShortcut((Join-Path $desktop '%APPNAME%.lnk'));" ^
  "$sc.TargetPath = $exe; $sc.WorkingDirectory = '%TARGET%'; $sc.IconLocation = $ico; $sc.Description = '%APPNAME%'; $sc.Save();" ^
  "$startDir = Join-Path ([Environment]::GetFolderPath('Programs')) 'Mzukulu QMS';" ^
  "if (-not (Test-Path $startDir)) { New-Item -ItemType Directory -Path $startDir | Out-Null }" ^
  "$sc2 = $ws.CreateShortcut((Join-Path $startDir '%APPNAME%.lnk'));" ^
  "$sc2.TargetPath = $exe; $sc2.WorkingDirectory = '%TARGET%'; $sc2.IconLocation = $ico; $sc2.Description = '%APPNAME%'; $sc2.Save();"

echo.
echo Done. "%APPNAME%" has been installed.
echo A shortcut has been added to your Desktop and Start Menu.
echo.
choice /C YN /M "Launch %APPNAME% now"
if errorlevel 2 goto end
start "" "%TARGET%\MzuApplication.exe"

:end
endlocal
