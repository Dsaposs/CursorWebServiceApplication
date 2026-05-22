@echo off
setlocal
set "GCLOUD_BIN=%ProgramFiles%\Google\Cloud SDK\google-cloud-sdk\bin"
if exist "%GCLOUD_BIN%\gcloud.cmd" goto found
set "GCLOUD_BIN=%ProgramFiles(x86)%\Google\Cloud SDK\google-cloud-sdk\bin"
if exist "%GCLOUD_BIN%\gcloud.cmd" goto found
set "GCLOUD_BIN=%LOCALAPPDATA%\Google\Cloud SDK\google-cloud-sdk\bin"
if exist "%GCLOUD_BIN%\gcloud.cmd" goto found
echo ERROR: Google Cloud SDK not found. Install with: winget install Google.CloudSDK
exit /b 1
:found
set "PATH=%GCLOUD_BIN%;%PATH%"
gcloud %*
