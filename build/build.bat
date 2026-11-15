@echo off
setlocal
cd /d "%~dp0\.."

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [x] csc.exe not found. Install .NET Framework 4.x first.
  exit /b 1
)

if not exist "WallpaperDeskClock.ico" (
  echo [i] generating WallpaperDeskClock.ico ...
  powershell -NoProfile -ExecutionPolicy Bypass -File "build\gen-icon.ps1"
)

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ ^
  /out:"WallpaperDeskClock.exe" ^
  /win32icon:"WallpaperDeskClock.ico" ^
  /win32manifest:"src\app.manifest" ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  "src\Program.cs" "src\AppContext.cs" "src\ClockWindow.cs" "src\Dialogs.cs" ^
  "src\WallpaperForm.cs" "src\Ui.cs" "src\Paths.cs" "src\ShellImage.cs" "src\AssemblyInfo.cs"

if errorlevel 1 (
  echo [x] build failed
  exit /b 1
)

echo [ok] WallpaperDeskClock.exe
