@echo off
setlocal
cd /d "%~dp0.."

set "CONTAINER_NAME=notes-api"
set "NAMESPACE=notes"
set "DEPLOYMENT=notes-api"
set "SERVICE=notes-api"

echo Stopping local dotnet app process if it is running...
powershell -NoProfile -Command "$processes = Get-CimInstance Win32_Process | Where-Object { ($_.Name -eq 'dotnet.exe' -or $_.Name -eq 'NotesApi.exe') -and ($_.CommandLine -like '*NotesApi\NotesApi.csproj*' -or $_.CommandLine -like '*NotesApi.dll*') }; if (-not $processes) { Write-Output 'No local dotnet app process found.'; exit 0 }; foreach ($process in $processes) { Write-Output ('Stopping process {0} ({1})' -f $process.ProcessId, $process.Name); Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue }"

echo.
echo Stopping Kubernetes port-forward if it is running...
powershell -NoProfile -Command "$processes = Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'kubectl.exe' -and $_.CommandLine -like '*port-forward*' -and $_.CommandLine -like '*notes-api*' }; if (-not $processes) { Write-Output 'No notes-api port-forward process found.'; exit 0 }; foreach ($process in $processes) { Write-Output ('Stopping kubectl port-forward process {0}' -f $process.ProcessId); Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue }"

echo.
where docker >nul 2>nul
if errorlevel 1 (
  echo Docker was not found. Skipping Docker cleanup.
) else (
  docker info >nul 2>nul
  if errorlevel 1 (
    echo Docker engine is not running. Skipping Docker cleanup.
  ) else (
    echo Removing Docker container %CONTAINER_NAME% if it exists...
    docker rm -f "%CONTAINER_NAME%" >nul 2>nul
    if errorlevel 1 (
      echo No Docker container named %CONTAINER_NAME% found.
    ) else (
      echo Docker container %CONTAINER_NAME% removed.
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
    kubectl delete deployment "%DEPLOYMENT%" -n "%NAMESPACE%" --ignore-not-found=true
    if errorlevel 1 exit /b 1
    kubectl delete service "%SERVICE%" -n "%NAMESPACE%" --ignore-not-found=true
    if errorlevel 1 exit /b 1
    echo Kubernetes app resources removed. Namespace, secret, and SQLite PVC were preserved.
  )
)

echo.
echo Stop complete.
