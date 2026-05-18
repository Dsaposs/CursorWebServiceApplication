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
set "NAMESPACE=notes"
set "DEPLOYMENT=notes-api"

where dotnet >nul 2>nul || (echo ERROR: dotnet was not found.& exit /b 1)
where docker >nul 2>nul || (echo ERROR: Docker was not found.& exit /b 1)

docker info >nul 2>nul
if errorlevel 1 (
  echo ERROR: Docker is installed but the Docker engine is not running.
  echo Start Docker Desktop, wait until it is ready, then run this script again.
  exit /b 1
)

if not exist "Dockerfile" (
  echo ERROR: Dockerfile was not found in:
  echo   %CD%
  exit /b 1
)

if not exist "NotesApi\NotesApi.csproj" (
  echo ERROR: NotesApi\NotesApi.csproj was not found.
  echo This folder does not currently have the expected project structure.
  exit /b 1
)

call :FailIfPortInUse
if errorlevel 1 exit /b 1

call :FailIfDockerInstanceExists
if errorlevel 1 exit /b 1

call :FailIfKubernetesInstanceExists
if errorlevel 1 exit /b 1

echo Building project...
dotnet publish NotesApi\NotesApi.csproj -c Release -o "%TEMP%\notes-api-publish-check" /p:UseAppHost=false
if errorlevel 1 exit /b 1

echo Building Docker image %IMAGE_NAME%...
docker build -t "%IMAGE_NAME%" .
if errorlevel 1 exit /b 1

docker volume inspect "%VOLUME_NAME%" >nul 2>nul
if errorlevel 1 (
  echo Creating Docker volume %VOLUME_NAME%...
  docker volume create "%VOLUME_NAME%" >nul
  if errorlevel 1 exit /b 1
)

echo Starting %CONTAINER_NAME% at http://localhost:%HOST_PORT%
docker run --name "%CONTAINER_NAME%" -d ^
  -p %HOST_PORT%:%CONTAINER_PORT% ^
  -v "%VOLUME_NAME%:/data" ^
  -e ASPNETCORE_URLS="http://+:%CONTAINER_PORT%" ^
  -e ASPNETCORE_ENVIRONMENT="Production" ^
  -e ConnectionStrings__DefaultConnection="%SQLITE_CONNECTION%" ^
  -e Jwt__Key="%JWT_KEY%" ^
  "%IMAGE_NAME%"
if errorlevel 1 exit /b 1

echo.
echo App started at http://localhost:%HOST_PORT%
exit /b 0

:FailIfPortInUse
powershell -NoProfile -Command "if (Get-NetTCPConnection -LocalPort %HOST_PORT% -State Listen -ErrorAction SilentlyContinue) { exit 1 }"
if errorlevel 1 (
  echo ERROR: An instance already appears to be using port %HOST_PORT%.
  echo Stop the existing app first with scripts\stop-app.cmd.
  exit /b 1
)
exit /b 0

:FailIfDockerInstanceExists
docker ps -a --format "{{.Names}}" | findstr /R /C:"^%CONTAINER_NAME%$" >nul
if not errorlevel 1 (
  echo ERROR: A Docker instance named %CONTAINER_NAME% already exists.
  echo Stop the existing app first with scripts\stop-app.cmd.
  exit /b 1
)
exit /b 0

:FailIfKubernetesInstanceExists
where kubectl >nul 2>nul
if errorlevel 1 exit /b 0

kubectl cluster-info >nul 2>nul
if errorlevel 1 exit /b 0

kubectl get deployment "%DEPLOYMENT%" -n "%NAMESPACE%" >nul 2>nul
if not errorlevel 1 (
  echo ERROR: A Kubernetes deployment named %DEPLOYMENT% already exists in namespace %NAMESPACE%.
  echo Stop the existing app first with scripts\stop-app.cmd.
  exit /b 1
)
exit /b 0
