@echo off
setlocal
cd /d "%~dp0.."

echo Building Docker image notes-api:local...
docker build -t notes-api:local .
@echo off
setlocal
cd /d "%~dp0.."

echo Building Docker image notes-api:local...
docker build -t notes-api:local .
