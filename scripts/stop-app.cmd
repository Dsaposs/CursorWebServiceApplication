@echo off
setlocal
cd /d "%~dp0.."

set "API_CONTAINER_NAME=notes-api"
set "UI_CONTAINER_NAME=notes-ui"
set "NETWORK_NAME=notes-network"
set "NAMESPACE=notes"
set "API_DEPLOYMENT=notes-api"
set "UI_DEPLOYMENT=notes-ui"
set "API_SERVICE=notes-api"
set "UI_SERVICE=notes-ui"

echo Stopping local dotnet app process if it is running...
powershell -NoProfile -Command "$processes = Get-CimInstance Win32_Process | Where-Object { ($_.Name -eq 'dotnet.exe' -or $_.Name -eq 'NotesApi.exe') -and ($_.CommandLine -like '*notes-api\NotesApi.csproj*' -or $_.CommandLine -like '*NotesApi.dll*') }; if (-not $processes) { Write-Output 'No local dotnet app process found.'; exit 0 }; foreach ($process in $processes) { Write-Output ('Stopping process {0} ({1})' -f $process.ProcessId, $process.Name); Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue }"

echo.
echo Stopping Kubernetes port-forward if it is running...
powershell -NoProfile -Command "$processes = Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'kubectl.exe' -and $_.CommandLine -like '*port-forward*' -and ($_.CommandLine -like '*notes-api*' -or $_.CommandLine -like '*notes-ui*') }; if (-not $processes) { Write-Output 'No notes app port-forward process found.'; exit 0 }; foreach ($process in $processes) { Write-Output ('Stopping kubectl port-forward process {0}' -f $process.ProcessId); Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue }"

echo.
echo Stopping local Nuxt app process if it is running...
powershell -NoProfile -Command "$processes = Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'node.exe' -and $_.CommandLine -like '*notes-ui*' -and ($_.CommandLine -like '*nuxt*' -or $_.CommandLine -like '*index.mjs*') }; if (-not $processes) { Write-Output 'No local Nuxt app process found.'; exit 0 }; foreach ($process in $processes) { Write-Output ('Stopping process {0} ({1})' -f $process.ProcessId, $process.Name); Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue }"

echo.
where docker >nul 2>nul
if errorlevel 1 (
  echo Docker was not found. Skipping Docker cleanup.
) else (
  docker info >nul 2>nul
  if errorlevel 1 (
    echo Docker engine is not running. Skipping Docker cleanup.
  ) else (
    echo Removing Docker container %UI_CONTAINER_NAME% if it exists...
    docker rm -f "%UI_CONTAINER_NAME%" >nul 2>nul
    if errorlevel 1 (
      echo No Docker container named %UI_CONTAINER_NAME% found.
    ) else (
      echo Docker container %UI_CONTAINER_NAME% removed.
    )

    echo Removing Docker container %API_CONTAINER_NAME% if it exists...
    docker rm -f "%API_CONTAINER_NAME%" >nul 2>nul
    if errorlevel 1 (
      echo No Docker container named %API_CONTAINER_NAME% found.
    ) else (
      echo Docker container %API_CONTAINER_NAME% removed.
    )

    echo Removing Docker network %NETWORK_NAME% if it is unused...
    docker network rm "%NETWORK_NAME%" >nul 2>nul
    if errorlevel 1 (
      echo Docker network %NETWORK_NAME% was not removed or was not found.
    ) else (
      echo Docker network %NETWORK_NAME% removed.
    )
  )
)

echo.
where kubectl >nul 2>nul
if errorlevel 1 (
  echo kubectl was not found. Skipping Kubernetes cleanup.
) else (
  kubectl cluster-info >nul 2>nul
  if errorlevel 1 (
    echo kubectl cannot connect to a Kubernetes cluster. Skipping Kubernetes cleanup.
  ) else (
    echo Deleting Kubernetes app resources if they exist...
    kubectl delete deployment "%UI_DEPLOYMENT%" -n "%NAMESPACE%" --ignore-not-found=true
    if errorlevel 1 exit /b 1
    kubectl delete service "%UI_SERVICE%" -n "%NAMESPACE%" --ignore-not-found=true
    if errorlevel 1 exit /b 1
    kubectl delete deployment "%API_DEPLOYMENT%" -n "%NAMESPACE%" --ignore-not-found=true
    if errorlevel 1 exit /b 1
    kubectl delete service "%API_SERVICE%" -n "%NAMESPACE%" --ignore-not-found=true
    if errorlevel 1 exit /b 1
    echo Kubernetes app resources removed. Namespace, secret, and SQLite PVC were preserved.
  )
)

echo.
echo Stop complete.
