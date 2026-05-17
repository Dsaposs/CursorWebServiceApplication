@echo off
setlocal

echo Installing GitHub CLI...
winget install --id GitHub.cli --exact --source winget --accept-source-agreements --accept-package-agreements
if errorlevel 1 exit /b 1

echo.
echo Verifying installation...
gh --version
if errorlevel 1 (
  echo.
  echo GitHub CLI installed, but gh is not on PATH yet.
  echo Close and reopen your terminal, then run: gh --version
  exit /b 1
)

echo.
echo GitHub CLI is installed.
echo If you have not authenticated yet, run: gh auth login
