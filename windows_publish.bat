@echo off
echo Publishing NextUI Setup Wizard...
dotnet publish -f net9.0-windows10.0.19041.0 -c Release -p:RuntimeIdentifierOverride=win10-x64 -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true

if %errorlevel% neq 0 (
    echo Publish failed!
    pause
    exit /b %errorlevel%
)

echo Creating run script...
set OUTPUT_DIR=bin\Release\net9.0-windows10.0.19041.0
echo @echo off > "%OUTPUT_DIR%\Launch NextUI Setup Wizard.bat"
echo start "" "win10-x64\NextUI Setup Wizard.exe" >> "%OUTPUT_DIR%\Launch NextUI Setup Wizard.bat"

echo.
echo Publish completed successfully!
echo Run script created at: %OUTPUT_DIR%\Launch NextUI Setup Wizard.bat
echo.
pause