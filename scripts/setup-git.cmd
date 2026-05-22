@echo off
setlocal

echo Setting up Git identity...
set /p GNAME="Enter your name: "
set /p GMAIL="Enter your email: "

git config --global user.name "%GNAME%"
git config --global user.email "%GMAIL%"

where gh >nul 2>nul
if errorlevel 1 (
    echo GitHub CLI (gh) not found. Please install it from https://cli.github.com/
    exit /b 1
)

echo.
echo Starting GitHub authentication...
gh auth login

echo.
echo Git identity and GitHub authentication setup complete.
git config --list --global | findstr "user"
gh auth status