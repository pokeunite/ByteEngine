@echo off
setlocal

cd /d "%~dp0.."

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0RebuildByteEngine.ps1"
set "BYTEENGINE_RESULT=%ERRORLEVEL%"

if not "%BYTEENGINE_RESULT%"=="0" (
    echo.
    echo BUILD/UPDATE FAILED.
    pause
    exit /b %BYTEENGINE_RESULT%
)

exit /b 0
