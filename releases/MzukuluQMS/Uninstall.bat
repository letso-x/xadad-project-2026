@echo off
REM Removes Mzukulu QMS and its shortcuts (keeps your local data folder).
setlocal
set "APPNAME=Mzukulu QMS"
set "TARGET=%LOCALAPPDATA%\MzukuluQMS"

echo Removing %APPNAME%...

del "%USERPROFILE%\Desktop\%APPNAME%.lnk" 2>nul
rmdir /s /q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Mzukulu QMS" 2>nul

REM Remove the installed app folder. Run from elsewhere so it can delete itself.
if exist "%TARGET%" (
  start "" cmd /c "timeout /t 1 >nul & rmdir /s /q \"%TARGET%\""
)

echo Done. (Your saved data in %%LOCALAPPDATA%%\MzukuluQC was left intact.)
endlocal
