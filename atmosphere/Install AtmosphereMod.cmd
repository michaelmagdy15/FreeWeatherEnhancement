@echo off
setlocal
cd /d "%~dp0"
set "PY="
if exist "installer\python\python.exe" set "PY=installer\python\python.exe"
if not defined PY (
  py -3 -c "import sys; sys.exit(sys.version_info < (3, 10))" >nul 2>nul && set "PY=py -3"
)
if not defined PY (
  python -c "import sys; sys.exit(sys.version_info < (3, 10))" >nul 2>nul && set "PY=python"
)
if not defined PY (
  echo Python 3.10 or newer is needed: https://www.python.org/downloads/
  pause
  exit /b 1
)
%PY% "installer\install.py" %*
echo.
pause
