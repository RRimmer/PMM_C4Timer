@echo off
rem Builds PMM_C4Timer in Release and writes the log next to this file.
cd /d "%~dp0"
dotnet build PMM_C4Timer.csproj -c Release -nologo > build.log 2>&1
echo exit %ERRORLEVEL% >> build.log
