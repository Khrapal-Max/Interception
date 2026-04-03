@echo off
setlocal
set "APP_DIR=%~dp0"
cd /d "%APP_DIR%"

for /f "tokens=2" %%a in ('tasklist /FI "IMAGENAME eq Interception.UI.exe" /FO LIST ^| find "PID:"') do (
    taskkill /PID %%a /T /F >nul 2>&1
)

endlocal
