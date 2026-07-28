@echo off
echo ========================================================
echo   Compiling Wacom CTL-472 Ultra-Low Latency osu! Driver
echo ========================================================

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo [ERROR] C# Compiler csc.exe not found at %CSC%
    pause
    exit /b 1
)

"%CSC%" /target:winexe /optimize+ /out:CTL472_OsuDriver.exe Program.cs MainForm.cs DriverCore.cs ConfigManager.cs /r:System.dll,System.Windows.Forms.dll,System.Drawing.dll,System.Core.dll

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================================
    echo   BUILD SUCCESSFUL! Generated: CTL472_OsuDriver.exe
    echo ========================================================
) else (
    echo.
    echo [ERROR] Build failed with error code %ERRORLEVEL%.
)
