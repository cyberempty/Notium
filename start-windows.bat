@echo off
setlocal EnableExtensions
chcp 65001 >nul
for %%I in ("%~dp0.") do set "ROOT=%%~fI"
cd /d "%ROOT%"
title Notium

set "ARCH=x64"
if /i "%PROCESSOR_ARCHITECTURE%"=="ARM64" set "ARCH=arm64"
set "RID=win-%ARCH%"
set "PORT=%ROOT%\Notium-portable"
set "RT=%ROOT%\runtime"
set "DOTNET_ROOT=%RT%\dotnet"
set "DOTNET_CLI_HOME=%RT%\home"
set "NUGET_PACKAGES=%RT%\nuget"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_NOLOGO=1"
set "DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1"
set "NUGET_XMLDOC_MODE=skip"
set "MSBUILDDISABLENODEREUSE=1"
set "DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1"
set "DOTNET_MULTILEVEL_LOOKUP=0"

if exist "%PORT%\Notium.exe" goto :run

cls
echo.
echo   NOTIUM
echo   --------------------------------------------------------
echo   First start: preparing the program. This happens only once
echo   and needs an Internet connection.
echo.

echo   [  0%%] Downloading components
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%\scripts\download-dotnet.ps1" -Arch %ARCH% -InstallDir "%DOTNET_ROOT%" -Work "%RT%"
if errorlevel 1 goto :fail

echo.
echo   [ 50%%] Building Notium (this takes a few minutes)
"%DOTNET_ROOT%\dotnet.exe" publish "%ROOT%\src\Notium\Notium.csproj" -c Release -r %RID% --self-contained true -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false -p:UseSharedCompilation=false -nodeReuse:false -nologo -v q -o "%RT%\out"
if errorlevel 1 goto :fail

echo   [ 90%%] Creating Notium-portable
if not exist "%PORT%" mkdir "%PORT%"
xcopy "%RT%\out\*" "%PORT%\" /E /Y /Q >nul
if errorlevel 1 goto :fail
move /y "%PORT%\Notium.App.exe" "%PORT%\Notium.exe" >nul

echo   [100%%] Done. Starting Notium...
echo.

:run

cls
echo.
echo   NOTIUM
echo   --------------------------------------------------------
echo   First start: preparing the program. This happens only once
echo   and needs an Internet connection.
echo.

echo   [  0%%] Downloading components
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%\scripts\download-dotnet.ps1" -Arch %ARCH% -InstallDir "%DOTNET_ROOT%" -Work "%RT%"
if errorlevel 1 goto :fail

echo.
echo   [ 50%%] Building Notium (this takes a few minutes)
"%DOTNET_ROOT%\dotnet.exe" publish "%ROOT%\src\Notium\Notium.csproj" -c Release -r %RID% --self-contained true -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false -p:UseSharedCompilation=false -nodeReuse:false -nologo -v q -o "%RT%\out"
if errorlevel 1 goto :fail

echo   [ 90%%] Creating Notium-portable
if not exist "%PORT%" mkdir "%PORT%"
xcopy "%RT%\out\*" "%PORT%\" /E /Y /Q >nul
if errorlevel 1 goto :fail
move /y "%PORT%\Notium.App.exe" "%PORT%\Notium.exe" >nul

echo   [ 97%%] Cleaning up
rmdir /s /q "%RT%" >nul 2>&1
rmdir /s /q "%ROOT%\src\Notium\obj" >nul 2>&1
rmdir /s /q "%ROOT%\src\Notium\bin" >nul 2>&1

echo   [100%%] Done. Starting Notium...
echo.

:run
start "" "%PORT%\Notium.exe" %*
if exist "%RT%" (
    rem tidy up after the program is already on screen
    "%DOTNET_ROOT%\dotnet.exe" build-server shutdown >nul 2>&1
    rmdir /s /q "%RT%" >nul 2>&1
    rmdir /s /q "%ROOT%\src\Notium\obj" >nul 2>&1
    rmdir /s /q "%ROOT%\src\Notium\bin" >nul 2>&1
)
exit /b 0

:fail
echo.
echo   --------------------------------------------------------
echo   Setup could not be completed. Please copy the message
echo   above and report it.
echo   --------------------------------------------------------
pause
exit /b 1
