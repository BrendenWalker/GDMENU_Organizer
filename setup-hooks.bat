@echo off
setlocal
REM Configure this clone to use the repository Git hooks under .githooks\

cd /d "%~dp0"

where git >nul 2>&1
if errorlevel 1 (
  echo error: git is not installed or not on PATH
  exit /b 1
)

git rev-parse --git-dir >nul 2>&1
if errorlevel 1 (
  echo error: run this script from a git clone of the repository
  exit /b 1
)

if not exist "%~dp0.githooks\" (
  echo error: .githooks directory not found
  exit /b 1
)

git config core.hooksPath .githooks
if errorlevel 1 (
  echo error: failed to set core.hooksPath
  exit /b 1
)

echo Configured core.hooksPath=.githooks for this repository.
echo Active hooks:
dir /b "%~dp0.githooks"
exit /b 0
