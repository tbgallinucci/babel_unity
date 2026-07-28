@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo ============================================
echo   BABEL_UNITY - Sync (atualizar com o GitHub)
echo ============================================
echo Rode este .bat sempre ao comecar a trabalhar.
echo.

for /f "delims=" %%b in ('git rev-parse --abbrev-ref HEAD 2^>nul') do set branch=%%b
if "%branch%"=="" goto naorepo
echo Branch atual: %branch%
echo.

set dirty=
for /f "delims=" %%i in ('git status --porcelain') do set dirty=1
if defined dirty goto quicksave
goto fetch

:quicksave
echo Ha mudancas nao salvas - fazendo um quicksave antes do sync...
git add -A
git commit -m "Quicksave antes do sync %date% %time%"
echo.

:fetch
echo Baixando novidades do GitHub...
git fetch origin
if errorlevel 1 goto semnet

echo.
echo Atualizando com o GitHub...
git pull origin %branch%
if errorlevel 1 goto conflito
echo.
echo Sync completo. Pode trabalhar.
pause
exit /b

:semnet
echo [ERRO] Nao consegui falar com o GitHub. Sem internet ou sem remoto configurado.
pause
exit /b

:conflito
echo.
echo ============================================
echo [CONFLITO] O Git nao conseguiu unir tudo sozinho.
echo Resolva os conflitos manualmente nos arquivos marcados
echo (procure por "<<<<<<<" neles), depois rode:
echo   git add -A
echo   git commit -m "merge"
echo ============================================
pause
exit /b

:naorepo
echo Este diretorio ainda nao e um repositorio Git. Rode: git init
pause
exit /b
