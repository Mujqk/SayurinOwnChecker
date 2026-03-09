@echo off
set "APP_VERSION=2.0.0"
echo ========================================
echo Building Sayurin Checker v%APP_VERSION%
echo Mode: Single File, Self-Contained
echo ========================================

:: Kill any running instances
taskkill /f /im "Sayurin Checker.exe" /t 2>nul

:: Clear previous builds
if exist "bin" rd /s /q "bin"
if exist "obj" rd /s /q "obj"
if exist "Releases" rd /s /q "Releases"

:: 1. Restore and Publish
echo.
echo [1/3] Publishing application...
dotnet publish BazaChecker.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:IncludeAllContentForSelfExtract=true /p:DebugType=none

if %ERRORLEVEL% neq 0 (
    echo [ERROR] Publish failed!
    pause
    exit /b 1
)

set "PUBLISH_DIR=bin\Release\net9.0-windows\win-x64\publish"
set "EXE_PATH=%PUBLISH_DIR%\Sayurin Checker.exe"

:: 2. Signing
echo.
echo [2/3] Signing executable...
powershell -ExecutionPolicy Bypass -File sign_app.ps1 "%EXE_PATH%"

:: 3. Velopack Packing
echo.
echo [3/3] Creating Velopack Release...
:: Check if vpk is installed, if not install it locally
dotnet tool install Velopack.Vpk --version 0.0.1298 --tool-path .tools >nul 2>&1
set "VPK=.tools\vpk.exe"

"%VPK%" pack --packId BazaChecker --packVersion %APP_VERSION% --packDir %PUBLISH_DIR% --mainExe "Sayurin Checker.exe" --packTitle "Sayurin Checker"

if %ERRORLEVEL% neq 0 (
    echo [ERROR] Velopack packing failed!
    pause
    exit /b 1
)

echo.
echo ========================================
echo Build finished!
echo.
echo Output:
echo - Single EXE: %EXE_PATH%
echo - Update Files: Releases\ (Upload content of this folder to GitHub)
echo ========================================
pause
