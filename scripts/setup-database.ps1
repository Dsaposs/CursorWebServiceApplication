# Prefer setup-database.cmd if execution policy blocks this script.
# Or run: powershell -ExecutionPolicy Bypass -File .\scripts\setup-database.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "Restoring project dependencies..." -ForegroundColor Cyan
dotnet restore api\src\NotesApi\NotesApi.csproj
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "NOTE: Database migrations run automatically when the API starts." -ForegroundColor Green
Write-Host "      Simply run the API and the SQLite database will be created/updated."
Write-Host ""
Write-Host "To apply migrations manually (e.g. against a PostgreSQL instance):" -ForegroundColor Cyan
Write-Host "  dotnet ef database update --project api\src\NotesApi\NotesApi.csproj"
Write-Host ""
Write-Host "Setup complete." -ForegroundColor Green
