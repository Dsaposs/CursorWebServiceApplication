@echo off
setlocal
cd /d "%~dp0"

echo Starting Notes API + web UI at http://localhost:5294
echo Press Ctrl+C to stop.
dotnet run --project NotesApi\NotesApi.csproj
