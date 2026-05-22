@echo off
cd /d "%~dp0.."

where docker >nul 2>nul || (echo ERROR: Docker was not found. & exit /b 1)
docker info >nul 2>nul || (echo ERROR: Docker engine is not running. Start Docker Desktop and try again. & exit /b 1)

if not exist ".env" (
  echo ERROR: .env file not found.
  echo Copy .env.example to .env and fill in your secret values first.
  exit /b 1
)

echo Detecting LAN IP for network access...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0resolve-lan-host.ps1"
if errorlevel 1 exit /b 1

docker compose build --no-cache
if errorlevel 1 exit /b 1

docker compose up -d
if errorlevel 1 exit /b 1

echo.
echo Waiting for services to become healthy...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0wait-for-stack.ps1"
if errorlevel 1 exit /b 1

if /I "%SKIP_E2E%"=="1" (
  echo SKIP_E2E=1 — skipping Playwright end-to-end tests.
  goto :done
)

echo.
echo Running Playwright end-to-end tests against the Docker stack...
pushd e2e
if not exist node_modules (
  call npm ci
  if errorlevel 1 (
    popd
    exit /b 1
  )
)

call npx playwright install chromium
if errorlevel 1 (
  popd
  exit /b 1
)

call npm test
set E2E_EXIT=%ERRORLEVEL%
popd
if not "%E2E_EXIT%"=="0" (
  echo.
  echo ERROR: E2E tests failed with exit code %E2E_EXIT%.
  exit /b %E2E_EXIT%
)

echo.
echo E2E tests passed.

:done
echo.
echo  Local (this machine):
echo    API:    http://localhost:5294
echo    UI:     http://localhost:3000
echo    Mobile: http://localhost:3001
echo    LLM:    http://localhost:8000
echo    Ollama: http://localhost:11434
echo.
echo  Swagger: http://localhost:5294/swagger
echo  Health:  http://localhost:5294/health
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0resolve-lan-host.ps1" -PrintUrls
