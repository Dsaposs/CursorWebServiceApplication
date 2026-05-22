@echo off
cd /d "%~dp0.."
call "%~dp0gcloud.cmd" auth login
if errorlevel 1 exit /b 1
echo.
echo Set your project (edit project id if needed):
call "%~dp0gcloud.cmd" config set project keen-truth-463618-a0
call "%~dp0gcloud.cmd" --version
