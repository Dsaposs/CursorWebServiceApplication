@echo off
setlocal

set "SOURCE_DIR=%~dp0.."
for %%I in ("%SOURCE_DIR%") do set "SOURCE_DIR=%%~fI"

set "TARGET_DIR=C:\Users\Dan\.cursor\projects\Cursor Web Service Application"
set "REMOTE_URL=https://github.com/dsaposs/CursorWebServiceApplication.git"

echo Creating project repo at:
echo   %TARGET_DIR%
echo.

if not exist "%TARGET_DIR%" (
  mkdir "%TARGET_DIR%"
  if errorlevel 1 exit /b 1
)

echo Reusing existing target folder and replacing project files...
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
  if exist "%TARGET_DIR%\%%~P" (
    if exist "%TARGET_DIR%\%%~P\NUL" (
      rmdir /S /Q "%TARGET_DIR%\%%~P"
    ) else (
      del /F /Q "%TARGET_DIR%\%%~P"
    )
  )
)

for %%D in (
  ".config"
  "k8s"
  "NotesApi"
  "scripts"
) do (
  robocopy "%SOURCE_DIR%\%%~D" "%TARGET_DIR%\%%~D" /E /NFL /NDL /NJH /NJS /NP >nul
  if errorlevel 8 (
    echo Failed to copy directory %%~D
    exit /b 1
  )
)

for %%F in (
  ".dockerignore"
  ".gitignore"
  "Dockerfile"
  "NotesApi.sln"
  "README.md"
  "run-app.cmd"
) do (
  copy /Y "%SOURCE_DIR%\%%~F" "%TARGET_DIR%\" >nul
  if errorlevel 1 (
    echo Failed to copy file %%~F
    exit /b 1
  )
)

cd /d "%TARGET_DIR%"
if errorlevel 1 exit /b 1

if not exist ".git" (
  git init
  if errorlevel 1 exit /b 1
)

git add .
if errorlevel 1 exit /b 1

git commit -m "Initial notes web service application"
if errorlevel 1 (
  echo Nothing new to commit or commit failed.
)

git branch -M main
if errorlevel 1 exit /b 1

git remote remove origin >nul 2>nul
git remote add origin "%REMOTE_URL%"
if errorlevel 1 exit /b 1

echo.
echo Done.
echo Existing folder now contains the project and git remote:
echo   %TARGET_DIR%
echo.
echo Remote set to:
echo   %REMOTE_URL%
echo.
echo If you already created the GitHub repo, push with:
echo   git push -u origin main
