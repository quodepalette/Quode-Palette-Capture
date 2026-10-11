@echo off
setlocal

echo ============================================
echo   Quode Palette Capture - Build Script
echo ============================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] .NET SDK was not found on this machine.
    echo Install the free .NET 8 SDK from:
    echo   https://dotnet.microsoft.com/download/dotnet/8.0
    echo Then run this script again.
    echo.
    pause
    exit /b 1
)

cd /d "%~dp0"

echo Closing any running copy of the app so its files are not locked...
taskkill /f /im "Quode Palette Capture.exe" >nul 2>nul

echo Cleaning stale build output (bin, obj, dist)...
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj
if exist dist rmdir /s /q dist

echo Restoring packages (this may take a minute the first time)...
dotnet restore -p:Platform=x64
if errorlevel 1 (
    echo.
    echo [ERROR] Package restore failed. Check your internet connection and try again.
    pause
    exit /b 1
)

echo.
echo Building Quode Palette Capture (self-contained, no .NET install required to run)...
echo Note: this produces a folder, not a single exe file. ScreenRecorderLib
echo relies on a native component that can't be bundled into a single file.
dotnet publish -c Release -r win-x64 --self-contained true -p:Platform=x64 -o dist

if errorlevel 1 (
    echo.
    echo [ERROR] Build failed. Scroll up to see the error above.
    pause
    exit /b 1
)

echo.
echo ============================================
echo   Build succeeded!
echo   Your app is in the "dist" folder: dist\Quode Palette Capture.exe
echo   Copy/move the WHOLE "dist" folder (not just the exe) anywhere you like,
echo   then run "Quode Palette Capture.exe" from inside it.
echo ============================================
echo.
pause
