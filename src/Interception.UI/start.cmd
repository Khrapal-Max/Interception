@echo off
setlocal
set "APP_DIR=%~dp0"
cd /d "%APP_DIR%"

if not exist "%APP_DIR%data" mkdir "%APP_DIR%data"
if not exist "%APP_DIR%data\keys" mkdir "%APP_DIR%data\keys"

start "" http://127.0.0.1:5099
start "" "%APP_DIR%Interception.UI.exe" --urls http://127.0.0.1:5099
