@echo off
setlocal
cd /d "%~dp0.."

set "IMAGE_NAME=notes-api:local"
set "CONTAINER_NAME=notes-api"
set "VOLUME_NAME=notes-data"
set "HOST_PORT=5294"
set "CONTAINER_PORT=8080"
set "SQLITE_CONNECTION=Data Source=/data/notes.db"
set "JWT_KEY=ChangeThisDockerJwtSigningKeyToARealLongRandomValue123!"
set "RESET_SQLITE=false"

if /I "%~1"=="/reset-sqlite" set "RESET_SQLITE=true"

where dotnet >nul 2>nul || (echo dotnet was not found.& exit /b 1)
where docker >nul 2>nul || (echo Docker was not found.& exit /b 1)

docker info >nul 2>nul
if errorlevel 1 (
  echo Docker is installed but the Docker engine is not running.
  echo Start Docker Desktop, wait until it is ready, then run this script again.
  exit /b 1
)

echo Stopping existing local Docker container if it is running...
docker rm -f "%CONTAINER_NAME%" >nul 2>nul

if /I "%RESET_SQLITE%"=="true" (
  echo Resetting SQLite Docker volume %VOLUME_NAME%...
  docker volume rm "%VOLUME_NAME%" >nul 2>nul
)

docker volume inspect "%VOLUME_NAME%" >nul 2>nul
if errorlevel 1 (
  echo Creating Docker volume %VOLUME_NAME%...
  docker volume create "%VOLUME_NAME%" >nul
  if errorlevel 1 exit /b 1
)

echo Building project...
dotnet publish NotesApi\NotesApi.csproj -c Release -o "%TEMP%\notes-api-publish-check" /p:UseAppHost=false
if errorlevel 1 exit /b 1

echo Building Docker image %IMAGE_NAME%...
docker build -t "%IMAGE_NAME%" .
if errorlevel 1 exit /b 1

echo Starting refreshed local container at http://localhost:%HOST_PORT%
docker run --name "%CONTAINER_NAME%" --rm ^
  -p %HOST_PORT%:%CONTAINER_PORT% ^
  -v "%VOLUME_NAME%:/data" ^
  -e ASPNETCORE_URLS="http://+:%CONTAINER_PORT%" ^
  -e ASPNETCORE_ENVIRONMENT="Production" ^
  -e ConnectionStrings__DefaultConnection="%SQLITE_CONNECTION%" ^
  -e Jwt__Key="%JWT_KEY%" ^
  "%IMAGE_NAME%"
