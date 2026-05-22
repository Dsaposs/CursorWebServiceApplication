@echo off
cd /d "%~dp0.."

where docker >nul 2>nul || (echo ERROR: Docker was not found. & exit /b 1)
docker info >nul 2>nul || (echo ERROR: Docker engine is not running. Start Docker Desktop and try again. & exit /b 1)

echo WARNING: This will stop all containers and DELETE all database data.
set /p CONFIRM=Type YES to continue: 
if /i not "%CONFIRM%"=="YES" (echo Cancelled. & exit /b 0)

echo.
echo Stopping containers and removing volumes...
docker compose down -v
if errorlevel 1 exit /b 1

echo.
echo Rebuilding images from scratch (no cache)...
docker compose build --no-cache
if errorlevel 1 exit /b 1

echo.
echo Starting services...
docker compose up -d
if errorlevel 1 exit /b 1

echo.
echo  API:    http://localhost:5294
echo  UI:     http://localhost:3000
echo  Mobile: http://localhost:3001
echo  LLM:    http://localhost:8000
echo  Ollama: http://localhost:11434
echo.
echo  Swagger: http://localhost:5294/swagger
echo  Health:  http://localhost:5294/health
