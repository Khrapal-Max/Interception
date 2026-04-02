@echo off
setlocal

cd /d "%~dp0"

dotnet publish .\Interception.UI.csproj -c Release -r win-x64 --self-contained true -o .\publish\portable-win-x64

endlocal
