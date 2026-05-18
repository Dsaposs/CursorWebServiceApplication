@echo off
setlocal
cd /d "%~dp0.."

set "IMAGE_NAME=notes-api:local"
if not "%~1"=="" set "IMAGE_NAME=%~1"

echo Building Docker image %IMAGE_NAME%...
docker build -t "%IMAGE_NAME%" .
