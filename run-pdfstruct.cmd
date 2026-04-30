@echo off
REM Batch-process every PDF in the playground folder through pdfstruct.cli.
REM
REM For each <name>.pdf, produces:
REM   <name>\<name>.md         - extracted markdown
REM   <name>\page-NNN.png      - debug image overlays
REM
REM Usage:
REM   run-pdfstruct.cmd
REM   run-pdfstruct.cmd D:\some\folder

setlocal enabledelayedexpansion

set "PLAYGROUND=%~1"
if "%PLAYGROUND%"=="" set "PLAYGROUND=%~dp0playground"
set "CLI=%~dp0src\PdfStruct.Cli\bin\Debug\net8.0\pdfstruct.cli.exe"

if not exist "%CLI%" (
    echo CLI not found: %CLI%
    echo Build PdfStruct.Cli first.
    exit /b 1
)

if not exist "%PLAYGROUND%" (
    echo Playground folder not found: %PLAYGROUND%
    exit /b 1
)

set "COUNT=0"
set "FAILED=0"

for %%F in ("%PLAYGROUND%\*.pdf") do (
    set /a COUNT+=1
    set "NAME=%%~nF"
    set "OUTDIR=%PLAYGROUND%\!NAME!"
    set "MDPATH=!OUTDIR!\!NAME!.md"

    echo [!NAME!] %%~nxF

    if not exist "!OUTDIR!" mkdir "!OUTDIR!"

    "%CLI%" extract "%%F" --output "!MDPATH!" --debug-image "!OUTDIR!"
    if errorlevel 1 (
        set /a FAILED+=1
        echo   FAILED with exit code !errorlevel!
    )
)

echo.
echo Total: %COUNT% files, %FAILED% failed

if %FAILED% gtr 0 exit /b 1
endlocal
