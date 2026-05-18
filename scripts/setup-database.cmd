@echo off
setlocal
cd /d "%~dp0.."

echo Restoring project dependencies...
dotnet restore api\src\NotesApi\NotesApi.csproj
if errorlevel 1 exit /b 1

echo SQLite database will be created automatically when the application starts.
exit /b 0
