@echo off
REM ---------------------------------------------------------------
REM  GMSoft - arranca el backend en modo desarrollo.
REM  Crea la base y aplica migraciones solo en el primer arranque.
REM ---------------------------------------------------------------
title GMSoft API

REM Ubicarse en la carpeta del script, sin importar desde donde se lo llame.
cd /d "%~dp0"

REM El perfil de launchSettings ya lo pone, pero si alguien corre esto con otra
REM configuracion cargada, sin Development no se leen appsettings.Development.json
REM ni se aplica la migracion automatica.
set ASPNETCORE_ENVIRONMENT=Development

echo.
echo   GMSoft API
echo   Swagger: http://localhost:5142
echo   Para cortar: Ctrl+C
echo.

dotnet run --project GMSoft.API

REM Si dotnet falla, sin esto la ventana se cierra sola y no se ve el error.
if errorlevel 1 (
    echo.
    echo   *** La API termino con error. Revisa el detalle arriba. ***
    echo.
    echo   Lo mas comun:
    echo     - Postgres no esta corriendo.
    echo     - La contrasena en GMSoft.API\appsettings.Development.json sigue en CAMBIAR.
    echo.
)

pause
