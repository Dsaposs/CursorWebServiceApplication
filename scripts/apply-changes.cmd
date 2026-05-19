@echo off
cd /d "%~dp0.."

where docker >nul 2>nul || (echo ERROR: Docker was not found. & exit /b 1)
docker info >nul 2>nul || (echo ERROR: Docker engine is not running. Start Docker Desktop and try again. & exit /b 1)

if not exist ".env" (
  echo ERROR: .env file not found.
  echo Copy .env.example to .env and fill in your secret values first.
  exit /b 1
)

docker compose up --build -d
if errorlevel 1 exit /b 1

echo.
echo UI: http://localhost:3000
echo API: http://localhost:5294
