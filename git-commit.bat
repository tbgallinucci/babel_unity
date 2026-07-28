@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo ============================================
echo   BABEL_UNITY - Commit (salvar progresso no Git)
echo ============================================
echo.
for /f "delims=" %%b in ('git rev-parse --abbrev-ref HEAD 2^>nul') do set branch=%%b
if "%branch%"=="" goto naorepo
echo Branch atual: %branch%
echo.
set /p msg=Descreva o que mudou:
if "%msg%"=="" set msg=Update
git add -A
git commit -m "%msg%"
echo.
echo Enviando para o GitHub (push)...
git push -u origin %branch%
if errorlevel 1 goto pushfail
echo.
echo Feito. Progresso salvo.
pause
exit /b

:pushfail
echo.
echo [AVISO] O push falhou. O commit LOCAL foi salvo mesmo assim.
echo Sem internet? Rode este .bat de novo mais tarde.
echo.
echo Feito. Progresso salvo (local).
pause
exit /b

:naorepo
echo Este diretorio ainda nao e um repositorio Git.
echo Abra o cmd aqui e rode uma vez:  git init
echo.
pause
exit /b
