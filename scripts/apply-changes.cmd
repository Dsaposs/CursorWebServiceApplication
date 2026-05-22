@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0.."

where git >nul 2>nul || (echo ERROR: Git was not found. & exit /b 1)
where docker >nul 2>nul || (echo ERROR: Docker was not found. & exit /b 1)
docker info >nul 2>nul || (echo ERROR: Docker engine is not running. Start Docker Desktop and try again. & exit /b 1)

if not exist ".env" (
  echo ERROR: .env file not found.
  echo Copy .env.example to .env and fill in your secret values first.
  exit /b 1
)

set "targetServices="
set "redeployAll=0"
set "hasChanges=0"
set "statusFile=%TEMP%\apply-changes-status-%RANDOM%-%RANDOM%.txt"

git status --porcelain --untracked-files=all --no-renames > "%statusFile%"
if errorlevel 1 (
  echo ERROR: Failed to inspect git status.
  if exist "%statusFile%" del "%statusFile%" >nul 2>nul
  exit /b 1
)

for /f "usebackq delims=" %%I in ("%statusFile%") do (
  set "hasChanges=1"
  set "line=%%I"
  set "changedPath=!line:~3!"
  set "ignorePath=0"

  rem Remove optional porcelain quotes around paths with spaces.
  set "changedPath=!changedPath:"=!"

  rem Ignore common generated artifacts that should not trigger rebuilds.
  if not "!changedPath:\bin\=!"=="!changedPath!" set "ignorePath=1"
  if not "!changedPath:/bin/=!"=="!changedPath!" set "ignorePath=1"
  if not "!changedPath:\obj\=!"=="!changedPath!" set "ignorePath=1"
  if not "!changedPath:/obj/=!"=="!changedPath!" set "ignorePath=1"
  if not "!changedPath:\.nuxt\=!"=="!changedPath!" set "ignorePath=1"
  if not "!changedPath:/.nuxt/=!"=="!changedPath!" set "ignorePath=1"
  if not "!changedPath:\node_modules\=!"=="!changedPath!" set "ignorePath=1"
  if not "!changedPath:/node_modules/=!"=="!changedPath!" set "ignorePath=1"
  if not "!changedPath:\__pycache__\=!"=="!changedPath!" set "ignorePath=1"
  if not "!changedPath:/__pycache__/=!"=="!changedPath!" set "ignorePath=1"

  if "!ignorePath!"=="0" (
    if /i "!changedPath!"=="docker-compose.yml" set "redeployAll=1"
    if /i "!changedPath!"==".env" set "redeployAll=1"

    if /i "!changedPath:~0,4!"=="api\" call :addService api
    if /i "!changedPath:~0,4!"=="api/" call :addService api
    if /i "!changedPath:~0,3!"=="ui\" call :addService ui
    if /i "!changedPath:~0,3!"=="ui/" call :addService ui
    if /i "!changedPath:~0,7!"=="mobile\" call :addService mobile
    if /i "!changedPath:~0,7!"=="mobile/" call :addService mobile
    if /i "!changedPath:~0,4!"=="llm\" call :addService llm
    if /i "!changedPath:~0,4!"=="llm/" call :addService llm
  )
)
if exist "%statusFile%" del "%statusFile%" >nul 2>nul

if "!hasChanges!"=="0" (
  echo No local changes detected.
  echo Nothing to rebuild or redeploy.
  exit /b 0
)

if "!redeployAll!"=="1" (
  echo Shared stack config changed. Redeploying all services...
  docker compose up --build -d
) else if defined targetServices (
  echo Rebuilding and redeploying changed services: !targetServices!
  docker compose up --build -d !targetServices!
) else (
  echo Changes detected, but no service code/config changed.
  echo Nothing to rebuild or redeploy.
  exit /b 0
)
if errorlevel 1 exit /b 1

echo.
echo UI: http://localhost:3000
echo API: http://localhost:5294
exit /b 0

:addService
set "needle= %~1 "
set "haystack= !targetServices! "
if "!haystack:%needle%=!"=="!haystack!" set "targetServices=!targetServices! %~1"
exit /b 0
