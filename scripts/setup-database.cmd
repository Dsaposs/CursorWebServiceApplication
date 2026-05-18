@echo off
setlocal
cd /d "%~dp0.."

echo Restoring project dependencies...
dotnet restore notes-api\NotesApi.csproj
if errorlevel 1 exit /b 1

echo SQLite database will be created automatically when the application starts.
exit /b 0
@echo off
setlocal
cd /d "%~dp0.."

echo Restoring dotnet tools (dotnet-ef)...
dotnet tool restore
if errorlevel 1 exit /b 1

echo Applying database migrations...
dotnet dotnet-ef database update --project notes-api\NotesApi.csproj
if errorlevel 1 exit /b 1

echo Database is up to date.
exit /b 0
