@echo off
echo Building CATUI ZombieINC Patch...

set GAME_DIR=%~dp0..\..\
set MOD_DIR=%~dp0
set OUTPUT_DIR=%MOD_DIR%

echo Game Directory: %GAME_DIR%
echo Mod Directory: %MOD_DIR%

:: Find the C# compiler
set CSC_PATH=""
if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe" (
    set CSC_PATH="C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe"
) else if exist "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe" (
    set CSC_PATH="C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
) else if exist "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" (
    set CSC_PATH="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
) else (
    echo Error: Could not find C# compiler (csc.exe)
    echo Please install Visual Studio or .NET Framework SDK
    pause
    exit /b 1
)

echo Using compiler: %CSC_PATH%

:: Create references string
set REFS=/reference:"%GAME_DIR%7DaysToDie_Data\Managed\Assembly-CSharp.dll"
set REFS=%REFS% /reference:"%GAME_DIR%7DaysToDie_Data\Managed\UnityEngine.dll"
set REFS=%REFS% /reference:"%GAME_DIR%7DaysToDie_Data\Managed\UnityEngine.CoreModule.dll"
set REFS=%REFS% /reference:"%GAME_DIR%7DaysToDie_Data\Managed\0Harmony.dll"
set REFS=%REFS% /reference:"%GAME_DIR%7DaysToDie_Data\Managed\LogLibrary.dll"

:: Compile the DLL
%CSC_PATH% /target:library /out:"%OUTPUT_DIR%CATUIZombieINCPatch.dll" /platform:x64 /optimize+ %REFS% "%MOD_DIR%Scripts\*.cs"

if %ERRORLEVEL% EQU 0 (
    echo Build successful! DLL created at: %OUTPUT_DIR%CATUIZombieINCPatch.dll
) else (
    echo Build failed with error code %ERRORLEVEL%
    pause
    exit /b %ERRORLEVEL%
)

echo Build complete.
pause
