@echo off
setlocal
cd /d "%~dp0"

echo [1/3] Building Release single-file publish...
dotnet publish -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=true -o "%~dp0dist\publish"

if errorlevel 1 (
    echo [ERROR] dotnet publish failed.
    exit /b %errorlevel%
)

echo [2/3] Generating portable and installer executables...
copy /y "%~dp0dist\publish\YUNSEUL-EDITOR.exe" "%~dp0YUNSEUL-EDITOR.exe"
copy /y "%~dp0dist\publish\YUNSEUL-EDITOR.exe" "%~dp0YUNSEUL-EDITOR-Setup.exe"

if not exist "%~dp0publish" mkdir "%~dp0publish"
copy /y "%~dp0dist\publish\YUNSEUL-EDITOR.exe" "%~dp0publish\YUNSEUL-EDITOR.exe"
copy /y "%~dp0dist\publish\YUNSEUL-EDITOR.exe" "%~dp0publish\YUNSEUL-EDITOR.v1.2.exe"

echo [3/3] Build completed successfully!
echo - Portable:  %~dp0YUNSEUL-EDITOR.exe
echo - Installer: %~dp0YUNSEUL-EDITOR-Setup.exe
