@echo off
setlocal enabledelayedexpansion

echo Building CATUI 3Scrubs Patch...

:: Repo layout is typically: <GameDir>\7DTD-3scrubs\7DTD-3scrubs\UI-Extensions\<ThisMod>
:: So the game directory is usually 4 levels up from this script.
set GAME_DIR=%~dp0..\..\..\..\
for %%I in ("%GAME_DIR%") do set GAME_DIR=%%~fI
if not "%GAME_DIR:~-1%"=="\" set GAME_DIR=%GAME_DIR%\

echo Game Directory: %GAME_DIR%
if not exist "%GAME_DIR%7DaysToDie_Data\Managed\Assembly-CSharp.dll" (
    echo Error: Could not locate Assembly-CSharp.dll at:
    echo   %GAME_DIR%7DaysToDie_Data\Managed\Assembly-CSharp.dll
    echo Tip: Edit GAME_DIR in this script to point to your 7 Days To Die install folder.
    pause
    exit /b 1
)
set MOD_DIR=%~dp0
set OUTPUT_DLL=%MOD_DIR%ZZZ_CATUI_3Scrubs_Patch.dll

echo Mod Directory: %MOD_DIR%

:: Find the C# compiler
set CSC_PATH=
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

:: Resolve Harmony reference (TFP Harmony mod is the common install)
set HARMONY_DLL=
if exist "%GAME_DIR%7DaysToDie_Data\Managed\0Harmony.dll" (
    set HARMONY_DLL=%GAME_DIR%7DaysToDie_Data\Managed\0Harmony.dll
) else if exist "%GAME_DIR%Mods\0_TFP_Harmony\0Harmony.dll" (
    set HARMONY_DLL=%GAME_DIR%Mods\0_TFP_Harmony\0Harmony.dll
) else (
    echo Error: Could not find 0Harmony.dll
    echo Looked in:
    echo   %GAME_DIR%7DaysToDie_Data\Managed\0Harmony.dll
    echo   %GAME_DIR%Mods\0_TFP_Harmony\0Harmony.dll
    pause
    exit /b 1
)

:: Compile
set REFS=/reference:"%GAME_DIR%7DaysToDie_Data\Managed\Assembly-CSharp.dll"
set REFS=!REFS! /reference:"%GAME_DIR%7DaysToDie_Data\Managed\UnityEngine.dll"
set REFS=!REFS! /reference:"%GAME_DIR%7DaysToDie_Data\Managed\UnityEngine.CoreModule.dll"
set REFS=!REFS! /reference:"%GAME_DIR%7DaysToDie_Data\Managed\LogLibrary.dll"
set REFS=!REFS! /reference:"%GAME_DIR%7DaysToDie_Data\Managed\netstandard.dll"
set REFS=!REFS! /reference:"%HARMONY_DLL%"

%CSC_PATH% /target:library /out:"%OUTPUT_DLL%" /platform:x64 /optimize+ %REFS% "%MOD_DIR%Scripts\*.cs"

if %ERRORLEVEL% EQU 0 (
    echo Build successful! DLL created at: %OUTPUT_DLL%
) else (
    echo Build failed with error code %ERRORLEVEL%
    pause
    exit /b %ERRORLEVEL%
)

echo Build complete.
pause
