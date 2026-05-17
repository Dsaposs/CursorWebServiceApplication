@echo off
setlocal

set "SOURCE_DIR=%~dp0.."
for %%I in ("%SOURCE_DIR%") do set "SOURCE_DIR=%%~fI"

set "TARGET_DIR=C:\Users\Dan\.cursor\projects\Cursor Web Service Application"
set "GITHUB_REPO=Dsaposs/Cursor-Web-Service-Application"
set "REMOTE_URL=https://github.com/%GITHUB_REPO%.git"

echo Creating project repo at:
echo   %TARGET_DIR%
echo.

if exist "%TARGET_DIR%\.git" (
  echo Target folder already contains a git repo:
  echo   %TARGET_DIR%
  echo.
  echo Refusing to overwrite it. Move or delete that folder before running again.
  exit /b 1
)

if not exist "%TARGET_DIR%" (
  mkdir "%TARGET_DIR%"
  if errorlevel 1 exit /b 1
)

for %%P in (
  ".config"
  ".dockerignore"
  ".gitignore"
  "Dockerfile"
  "k8s"
  "NotesApi"
  "NotesApi.sln"
  "README.md"
  "run-app.cmd"
  "scripts"
) do (
  if exist "%SOURCE_DIR%\%%~P\NUL" (
    xcopy "%SOURCE_DIR%\%%~P" "%TARGET_DIR%\%%~P" /E /I /Y >nul
    if errorlevel 1 (
      echo Failed to copy directory %%~P
      exit /b 1
    )
  ) else (
    copy /Y "%SOURCE_DIR%\%%~P" "%TARGET_DIR%\" >nul
    if errorlevel 1 (
      echo Failed to copy file %%~P
      exit /b 1
    )
  )
)

cd /d "%TARGET_DIR%"
if errorlevel 1 exit /b 1

git init
if errorlevel 1 exit /b 1

git add .
if errorlevel 1 exit /b 1

git commit -m "Initial notes web service application"
if errorlevel 1 exit /b 1

where gh >nul 2>nul
if errorlevel 1 (
  echo GitHub CLI was not found.
  echo Install gh or create %GITHUB_REPO% manually, then run:
  echo   git remote add origin %REMOTE_URL%
  echo   git branch -M main
  echo   git push -u origin main
  exit /b 1
)

gh repo view "%GITHUB_REPO%" >nul 2>nul
if errorlevel 1 (
  gh repo create "%GITHUB_REPO%" --private --source . --remote origin
  if errorlevel 1 exit /b 1
) else (
  git remote add origin "%REMOTE_URL%"
  if errorlevel 1 exit /b 1
)

git branch -M main
if errorlevel 1 exit /b 1

git push -u origin main
if errorlevel 1 exit /b 1

echo.
echo Done.
echo Repo: https://github.com/%GITHUB_REPO%
