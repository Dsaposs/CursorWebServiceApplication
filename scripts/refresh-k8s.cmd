@echo off
setlocal
cd /d "%~dp0.."

set "NAMESPACE=notes"
set "DEPLOYMENT=notes-api"
set "CONTAINER_NAME=notes-api"
set "LOCAL_CONTAINER_NAME=notes-api"
set "IMAGE_NAME=notes-api:local"
set "RESET_SQLITE=false"

if /I "%~1"=="/reset-sqlite" set "RESET_SQLITE=true"

where dotnet >nul 2>nul || (echo dotnet was not found.& exit /b 1)
where docker >nul 2>nul || (echo Docker was not found.& exit /b 1)
where kubectl >nul 2>nul || (echo kubectl was not found.& exit /b 1)

docker info >nul 2>nul
if errorlevel 1 (
  echo Docker is installed but the Docker engine is not running.
  echo Start Docker Desktop, wait until it is ready, then run this script again.
  exit /b 1
)

kubectl cluster-info >nul 2>nul
if errorlevel 1 (
  echo kubectl cannot connect to a Kubernetes cluster.
  echo Start Docker Desktop Kubernetes or minikube, then run this script again.
  exit /b 1
)

echo Stopping any standalone Docker container named %LOCAL_CONTAINER_NAME%...
docker rm -f "%LOCAL_CONTAINER_NAME%" >nul 2>nul

echo Scaling Kubernetes app down to zero pods...
kubectl get deployment "%DEPLOYMENT%" -n "%NAMESPACE%" >nul 2>nul
if not errorlevel 1 (
  kubectl scale deployment "%DEPLOYMENT%" --replicas=0 -n "%NAMESPACE%"
  if errorlevel 1 exit /b 1
  kubectl rollout status deployment/%DEPLOYMENT% -n "%NAMESPACE%" --timeout=60s >nul 2>nul
)

if /I "%RESET_SQLITE%"=="true" (
  echo Resetting SQLite PVC notes-sqlite-data...
  kubectl delete pvc notes-sqlite-data -n "%NAMESPACE%" --ignore-not-found=true
  if errorlevel 1 exit /b 1
) else (
  echo Preserving SQLite PVC notes-sqlite-data.
)

echo Building project...
dotnet publish NotesApi\NotesApi.csproj -c Release -o "%TEMP%\notes-api-publish-check" /p:UseAppHost=false
if errorlevel 1 exit /b 1

echo Building Docker image %IMAGE_NAME%...
docker build -t "%IMAGE_NAME%" .
if errorlevel 1 exit /b 1

if not "%IMAGE_REPOSITORY%"=="" (
  echo Tagging image as %IMAGE_REPOSITORY%...
  docker tag "%IMAGE_NAME%" "%IMAGE_REPOSITORY%"
  if errorlevel 1 exit /b 1

  echo Pushing image %IMAGE_REPOSITORY%...
  docker push "%IMAGE_REPOSITORY%"
  if errorlevel 1 exit /b 1
)

echo Applying Kubernetes manifests...
kubectl apply -k k8s
if errorlevel 1 exit /b 1

if not "%IMAGE_REPOSITORY%"=="" (
  echo Updating deployment image to %IMAGE_REPOSITORY%...
  kubectl set image deployment/%DEPLOYMENT% %CONTAINER_NAME%="%IMAGE_REPOSITORY%" -n "%NAMESPACE%"
  if errorlevel 1 exit /b 1
)

echo Starting Kubernetes app with one pod...
kubectl scale deployment "%DEPLOYMENT%" --replicas=1 -n "%NAMESPACE%"
if errorlevel 1 exit /b 1

echo Waiting for rollout...
kubectl rollout status deployment/%DEPLOYMENT% -n "%NAMESPACE%"
if errorlevel 1 exit /b 1

echo.
echo Refresh complete.
echo To open the app locally, run:
echo kubectl port-forward service/%DEPLOYMENT% 5294:80 -n %NAMESPACE%
