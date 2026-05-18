@echo off
setlocal
cd /d "%~dp0.."

set "API_IMAGE_NAME=ttrpg-api:local"
set "UI_IMAGE_NAME=ttrpg-ui:local"
set "API_CONTAINER_NAME=ttrpg-api"
set "UI_CONTAINER_NAME=ttrpg-ui"
set "NETWORK_NAME=ttrpg-network"
set "VOLUME_NAME=ttrpg-data"
set "API_HOST_PORT=5294"
set "UI_HOST_PORT=3000"
set "API_CONTAINER_PORT=8080"
set "UI_CONTAINER_PORT=3000"
set "SQLITE_CONNECTION=Data Source=/data/ttrpg.db"
if "%TTRPG_JWT_KEY%"=="" (
  for /f "delims=" %%K in ('powershell -NoProfile -Command "$bytes = New-Object byte[] 48; $rng = [Security.Cryptography.RandomNumberGenerator]::Create(); try { $rng.GetBytes($bytes); [Convert]::ToBase64String($bytes) } finally { $rng.Dispose() }"') do set "JWT_KEY=%%K"
) else (
  set "JWT_KEY=%TTRPG_JWT_KEY%"
)
if "%TTRPG_SEED_ADMIN_PASSWORD%"=="" (
  set "SEED_ADMIN_PASSWORD=LocalAdminPassword1!"
) else (
  set "SEED_ADMIN_PASSWORD=%TTRPG_SEED_ADMIN_PASSWORD%"
)
set "NAMESPACE=ttrpg"

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

if not exist "ui\src\Dockerfile" (
  echo ERROR: ui\src\Dockerfile was not found.
  exit /b 1
)

if not exist "api\src\NotesApi\NotesApi.csproj" (
  echo ERROR: api\src\NotesApi\NotesApi.csproj was not found.
  echo This folder does not currently have the expected project structure.
  exit /b 1
)

if not exist "ui\src\package.json" (
  echo ERROR: ui\src\package.json was not found.
  exit /b 1
)

call :FailIfPortInUse "%API_HOST_PORT%"
if errorlevel 1 exit /b 1

call :FailIfPortInUse "%UI_HOST_PORT%"
if errorlevel 1 exit /b 1

call :FailIfDockerInstanceExists "%API_CONTAINER_NAME%"
if errorlevel 1 exit /b 1

call :FailIfDockerInstanceExists "%UI_CONTAINER_NAME%"
if errorlevel 1 exit /b 1

call :FailIfKubernetesInstanceExists "ttrpg-api"
if errorlevel 1 exit /b 1

call :FailIfKubernetesInstanceExists "ttrpg-ui"
if errorlevel 1 exit /b 1

echo Building backend project...
dotnet publish api\src\NotesApi\NotesApi.csproj -c Release -o "%TEMP%\ttrpg-api-publish-check" /p:UseAppHost=false
if errorlevel 1 exit /b 1

echo Building Docker image %API_IMAGE_NAME%...
docker build -t "%API_IMAGE_NAME%" .
if errorlevel 1 exit /b 1

echo Building Docker image %UI_IMAGE_NAME%...
docker build -t "%UI_IMAGE_NAME%" ui\src
if errorlevel 1 exit /b 1

docker volume inspect "%VOLUME_NAME%" >nul 2>nul
if errorlevel 1 (
  echo Creating Docker volume %VOLUME_NAME%...
  docker volume create "%VOLUME_NAME%" >nul
  if errorlevel 1 exit /b 1
)

docker network inspect "%NETWORK_NAME%" >nul 2>nul
if errorlevel 1 (
  echo Creating Docker network %NETWORK_NAME%...
  docker network create "%NETWORK_NAME%" >nul
  if errorlevel 1 exit /b 1
)

echo Starting %API_CONTAINER_NAME% at http://localhost:%API_HOST_PORT%
docker run --name "%API_CONTAINER_NAME%" -d ^
  --network "%NETWORK_NAME%" ^
  -p %API_HOST_PORT%:%API_CONTAINER_PORT% ^
  -v "%VOLUME_NAME%:/data" ^
  -e ASPNETCORE_URLS="http://+:%API_CONTAINER_PORT%" ^
  -e ASPNETCORE_ENVIRONMENT="Production" ^
  -e ConnectionStrings__DefaultConnection="%SQLITE_CONNECTION%" ^
  -e Jwt__Key="%JWT_KEY%" ^
  -e Seed__AdminPassword="%SEED_ADMIN_PASSWORD%" ^
  -e Cors__AllowedOrigins__0="http://localhost:%UI_HOST_PORT%" ^
  "%API_IMAGE_NAME%"
if errorlevel 1 exit /b 1

echo Starting %UI_CONTAINER_NAME% at http://localhost:%UI_HOST_PORT%
docker run --name "%UI_CONTAINER_NAME%" -d ^
  --network "%NETWORK_NAME%" ^
  -p %UI_HOST_PORT%:%UI_CONTAINER_PORT% ^
  -e NUXT_API_BASE_URL="http://%API_CONTAINER_NAME%:%API_CONTAINER_PORT%" ^
  -e HOST="0.0.0.0" ^
  -e PORT="%UI_CONTAINER_PORT%" ^
  "%UI_IMAGE_NAME%"
if errorlevel 1 exit /b 1

echo.
echo UI started at http://localhost:%UI_HOST_PORT%
echo API started at http://localhost:%API_HOST_PORT%
exit /b 0

:FailIfPortInUse
powershell -NoProfile -Command "if (Get-NetTCPConnection -LocalPort %~1 -State Listen -ErrorAction SilentlyContinue) { exit 1 }"
if errorlevel 1 (
  echo ERROR: An instance already appears to be using port %~1.
  echo Stop the existing app first with scripts\stop-app.cmd.
  exit /b 1
)
exit /b 0

:FailIfDockerInstanceExists
docker ps -a --format "{{.Names}}" | findstr /R /C:"^%~1$" >nul
if not errorlevel 1 (
  echo ERROR: A Docker instance named %~1 already exists.
  echo Stop the existing app first with scripts\stop-app.cmd.
  exit /b 1
)
exit /b 0

:FailIfKubernetesInstanceExists
where kubectl >nul 2>nul
if errorlevel 1 exit /b 0

kubectl cluster-info >nul 2>nul
if errorlevel 1 exit /b 0

kubectl get deployment "%~1" -n "%NAMESPACE%" >nul 2>nul
if not errorlevel 1 (
  echo ERROR: A Kubernetes deployment named %~1 already exists in namespace %NAMESPACE%.
  echo Stop the existing app first with scripts\stop-app.cmd.
  exit /b 1
)
exit /b 0
