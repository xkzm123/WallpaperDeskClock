@echo off
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [x] csc.exe not found. Install .NET Framework 4.x first.
  exit /b 1
)

if not exist "dist" mkdir "dist"

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ ^
  /out:"dist\WallpaperDeskClock.exe" ^
  /win32manifest:"src\app.manifest" ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  "src\Program.cs" "src\AppContext.cs" "src\ClockWindow.cs" "src\Dialogs.cs"

if errorlevel 1 (
  echo [x] build failed
  exit /b 1
)

echo [ok] dist\WallpaperDeskClock.exe
