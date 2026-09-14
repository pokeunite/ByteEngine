@echo off
setlocal

cd /d "%~dp0"

echo.
echo ByteEngine will close any running Editor window.
echo Save your scene and Blueprint changes before continuing.
echo.
pause

call "%~dp0Tools\RebuildByteEngine.bat"
set "BYTEENGINE_RESULT=%ERRORLEVEL%"

if not "%BYTEENGINE_RESULT%"=="0" (
    echo.
    echo ByteEngine update failed. Review the errors above.
    pause
    exit /b %BYTEENGINE_RESULT%
)

exit /b 0
