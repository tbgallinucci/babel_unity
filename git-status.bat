@echo off
chcp 65001 >nul
cd /d "%~dp0"

for /f "delims=" %%b in ('git rev-parse --abbrev-ref HEAD 2^>nul') do set branch=%%b
if "%branch%"=="" goto naorepo

echo ============================================
echo   BABEL_UNITY - Status   (branch atual: %branch%)
echo ============================================
echo.
echo === O que mudou desde o ultimo commit ===
echo.
git status
echo.
echo === Ultimos commits ===
echo.
git log --oneline -10
echo.
pause
exit /b

:naorepo
echo Este diretorio ainda nao e um repositorio Git. Rode: git init
pause
exit /b
