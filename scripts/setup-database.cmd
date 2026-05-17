@echo off
setlocal
cd /d "%~dp0.."

echo Restoring dotnet tools (dotnet-ef)...
dotnet tool restore
if errorlevel 1 exit /b 1

echo Applying database migrations...
dotnet dotnet-ef database update --project NotesApi\NotesApi.csproj
if errorlevel 1 exit /b 1

echo Database is up to date.
exit /b 0
