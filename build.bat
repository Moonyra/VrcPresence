@echo off
set DOTNET_CLI_UI_LANGUAGE=en
where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET 8 SDK not found. Install it from: https://dotnet.microsoft.com/download/dotnet/8.0
  echo Press any key to exit...
  pause >nul
  exit /b 1
)
powershell -NoProfile -Command "if (Get-Process -Name VrcPresence -ErrorAction SilentlyContinue) { exit 1 }"
if errorlevel 1 (
  echo ERROR: VrcPresence.exe is running. Close the app and run this script again.
  echo Press any key to exit...
  pause >nul
  exit /b 1
)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
if errorlevel 1 (
  echo Publish failed.
  echo Press any key to exit...
  pause >nul
  exit /b 1
)
echo.
echo Done: publish\VrcPresence.exe
echo Press any key to exit...
pause >nul
