@echo off
cd /d "%~dp0.."

where docker >nul 2>nul || (echo ERROR: Docker was not found. & exit /b 1)
docker info >nul 2>nul || (echo ERROR: Docker engine is not running. & exit /b 1)

if not exist ".env" (
  echo ERROR: .env file not found. Copy .env.example to .env first.
  exit /b 1
)

if /I not "%SKIP_DOCKER_UP%"=="1" (
  echo Starting Docker stack...
  docker compose up -d
  if errorlevel 1 exit /b 1
)

echo Waiting for services...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0wait-for-stack.ps1"
if errorlevel 1 exit /b 1

pushd perf
if not exist node_modules (
  call npm install
  if errorlevel 1 (
    popd
    exit /b 1
  )
)

call npm run pipeline
set PERF_EXIT=%ERRORLEVEL%
popd

if not "%PERF_EXIT%"=="0" (
  echo.
  echo ERROR: Performance pipeline failed with exit code %PERF_EXIT%.
  exit /b %PERF_EXIT%
)

echo.
echo Performance report: perf\reports\PERFORMANCE_REPORT.md
exit /b 0
