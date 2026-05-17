@echo off
setlocal
cd /d "%~dp0"

set "IMAGE_NAME=notes-api:local"
set "CONTAINER_NAME=notes-api"
set "VOLUME_NAME=notes-data"
set "HOST_PORT=5294"
set "CONTAINER_PORT=8080"
set "SQLITE_CONNECTION=Data Source=/data/notes.db"
set "JWT_KEY=ChangeThisDockerJwtSigningKeyToARealLongRandomValue123!"

where docker >nul 2>nul
if errorlevel 1 (
  echo Docker was not found. Install/start Docker Desktop, then run this script again.
  exit /b 1
)

docker info >nul 2>nul
if errorlevel 1 (
  echo Docker is installed but the Docker engine is not running.
  echo Start Docker Desktop, wait until it is ready, then run this script again.
  exit /b 1
)

if not exist "Dockerfile" (
  echo Dockerfile was not found in:
  echo   %CD%
  exit /b 1
)

if not exist "NotesApi\NotesApi.csproj" (
  echo NotesApi\NotesApi.csproj was not found.
  echo This folder does not currently have the expected project structure.
  echo Re-run the move script from the original workspace, then run this script again.
  exit /b 1
)

echo Building Docker image %IMAGE_NAME%...
docker build -t "%IMAGE_NAME%" .
if errorlevel 1 exit /b 1

docker volume inspect "%VOLUME_NAME%" >nul 2>nul
if errorlevel 1 (
  echo Creating Docker volume %VOLUME_NAME%...
  docker volume create "%VOLUME_NAME%" >nul
  if errorlevel 1 exit /b 1
) else (
  echo Docker volume %VOLUME_NAME% already exists.
)

docker ps -a --format "{{.Names}}" | findstr /R /C:"^%CONTAINER_NAME%$" >nul
if not errorlevel 1 (
  echo Removing existing container %CONTAINER_NAME%...
  docker rm -f "%CONTAINER_NAME%" >nul
  if errorlevel 1 exit /b 1
)

echo Starting %CONTAINER_NAME% at http://localhost:%HOST_PORT%
docker run --name "%CONTAINER_NAME%" --rm ^
  -p %HOST_PORT%:%CONTAINER_PORT% ^
  -v "%VOLUME_NAME%:/data" ^
  -e ASPNETCORE_URLS="http://+:%CONTAINER_PORT%" ^
  -e ASPNETCORE_ENVIRONMENT="Production" ^
  -e ConnectionStrings__DefaultConnection="%SQLITE_CONNECTION%" ^
  -e Jwt__Key="%JWT_KEY%" ^
  "%IMAGE_NAME%"
