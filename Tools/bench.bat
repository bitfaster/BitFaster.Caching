@echo off
setlocal

if "%~1"=="" (
    echo Usage: bench.bat ^<runtimes^> [additional args]
    echo.
    echo   runtimes    Comma-separated list of runtimes ^(e.g. net90,net60^)
    echo.
    echo Example:
    echo   bench.bat net90,net60
    echo   bench.bat net90 --filter *Lru*
    exit /b 1
)

set RUNTIMES=%~1
shift

set EXTRA_ARGS=
:parse_args
if "%~1"=="" goto run
set EXTRA_ARGS=%EXTRA_ARGS% %1
shift
goto parse_args

:run
dotnet run -c Release --project BitFaster.Caching.Benchmarks --framework net9.0 -- --runtimes %RUNTIMES% %EXTRA_ARGS%
