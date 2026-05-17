# Prefer setup-database.cmd if execution policy blocks this script.
# Or run: powershell -ExecutionPolicy Bypass -File .\scripts\setup-database.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "Restoring dotnet tools (dotnet-ef)..." -ForegroundColor Cyan
dotnet tool restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Applying database migrations..." -ForegroundColor Cyan
dotnet dotnet-ef database update --project NotesApi\NotesApi.csproj
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Database is up to date." -ForegroundColor Green
