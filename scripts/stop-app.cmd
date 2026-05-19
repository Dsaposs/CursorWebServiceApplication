@echo off
cd /d "%~dp0.."

where docker >nul 2>nul || (echo ERROR: Docker was not found. & exit /b 1)
docker info >nul 2>nul || (echo ERROR: Docker engine is not running. & exit /b 1)

docker compose down
