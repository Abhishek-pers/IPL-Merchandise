@echo off
REM ---------------------------------------------------------------------------
REM Builds and tests everything, writing the full output to verify-log.txt in
REM the repository root. Double-click it or run it from a terminal.
REM ---------------------------------------------------------------------------
setlocal
cd /d "%~dp0.."
set "LOG=%CD%\verify-log.txt"
> "%LOG%" echo IPL Store verification - %DATE% %TIME%
call :run "Tools" "where dotnet & dotnet --list-sdks & where node & node -v & where docker & docker info --format {{.ServerVersion}}"
call :run "Backend build" "dotnet build backend\IplStore.sln -c Release"
call :run "Unit tests" "dotnet test backend\tests\IplStore.UnitTests -c Release --no-build"
call :run "Integration tests (needs Docker)" "dotnet test backend\tests\IplStore.IntegrationTests -c Release --no-build"
call :run "Frontend install" "cd frontend && npm install --no-audit --no-fund"
call :run "Frontend typecheck" "cd frontend && npm run typecheck"
call :run "Frontend tests" "cd frontend && npm test"
call :run "Frontend build" "cd frontend && npm run build"
>> "%LOG%" echo ===== DONE =====
exit /b 0

:run
>> "%LOG%" echo.
>> "%LOG%" echo ===== %~1 =====
cmd /c %2 >> "%LOG%" 2>&1
>> "%LOG%" echo ----- exit code: %ERRORLEVEL% -----
exit /b 0
