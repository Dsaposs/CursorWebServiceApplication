@echo off
setlocal

set "GITHUB_REPO=dsaposs/CursorWebServiceApplication"
set "REMOTE_URL=https://github.com/%GITHUB_REPO%.git"

cd /d "%~dp0"
if errorlevel 1 exit /b 1

where gh >nul 2>nul
if errorlevel 1 (
  echo GitHub CLI was not found. Install it, then run:
  echo   gh auth login
  echo   %~nx0
  exit /b 1
)

gh auth status
if errorlevel 1 (
  echo Please authenticate first:
  echo   gh auth login
  exit /b 1
)

gh repo view "%GITHUB_REPO%" >nul 2>nul
if errorlevel 1 (
  gh repo create "%GITHUB_REPO%" --private --source . --remote origin
  if errorlevel 1 exit /b 1
) else (
  git remote remove origin >nul 2>nul
  git remote add origin "%REMOTE_URL%"
  if errorlevel 1 exit /b 1
)

git branch -M main
if errorlevel 1 exit /b 1

git push -u origin main
if errorlevel 1 exit /b 1

echo.
echo Pushed to https://github.com/%GITHUB_REPO%
