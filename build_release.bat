@echo off
setlocal
echo ========================================================
echo   Building and Packaging Billease Pro for Production
echo ========================================================
echo.

echo [1/3] Building BillingSuite in Release mode...
dotnet build -c Release
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Build FAILED. Check errors above.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [2/3] Publishing self-contained single-file release...
dotnet publish BillingSuite.App\BillingSuite.App.csproj -c Release -r win-x64 --self-contained
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Publish FAILED. Check errors above.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [3/3] Creating Modern Setup Installer (BilleasePro_Setup.exe)...
set "ISCC_PATH=C:\Users\prabhat\AppData\Local\Programs\Inno Setup 6\iscc.exe"
if not exist "%ISCC_PATH%" set "ISCC_PATH=%LOCALAPPDATA%\Programs\Inno Setup 6\iscc.exe"
if not exist "%ISCC_PATH%" set "ISCC_PATH=C:\Program Files (x86)\Inno Setup 6\iscc.exe"
if not exist "%ISCC_PATH%" set "ISCC_PATH=C:\Program Files\Inno Setup 6\iscc.exe"

"%ISCC_PATH%" installer.iss
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [WARNING] Inno Setup compiler exited with an error. Check logs above.
) else (
    echo.
    echo ========================================================
    echo   Setup Installer built successfully!
    echo   Location: BillingSuite.App\bin\Release\net8.0-windows\win-x64\publish\BilleasePro_Setup.exe
    echo ========================================================
)

echo.
pause
