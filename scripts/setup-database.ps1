# Prefer setup-database.cmd if execution policy blocks this script.
# Or run: powershell -ExecutionPolicy Bypass -File .\scripts\setup-database.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "Restoring project dependencies..." -ForegroundColor Cyan
dotnet restore notes-api\NotesApi.csproj
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "SQLite database will be created automatically when the application starts." -ForegroundColor Green
# Prefer setup-database.cmd if execution policy blocks this script.
# Or run: powershell -ExecutionPolicy Bypass -File .\scripts\setup-database.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "Restoring dotnet tools (dotnet-ef)..." -ForegroundColor Cyan
dotnet tool restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Applying database migrations..." -ForegroundColor Cyan
dotnet dotnet-ef database update --project notes-api\NotesApi.csproj
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Database is up to date." -ForegroundColor Green
