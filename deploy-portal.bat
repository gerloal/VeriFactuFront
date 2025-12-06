@echo off
setlocal enabledelayedexpansion

REM Configuración
set REPOSITORY_URI=340663646958.dkr.ecr.eu-west-1.amazonaws.com/verifactu-portal-repo
set AWS_REGION=eu-west-1
set IMAGE_NAME=verifactu-portal
set IMAGE_TAG=latest

set WORKDIR=%~dp0
pushd "%WORKDIR%"

REM Paso 1: compilar el proyecto
powershell -NoProfile -Command "dotnet build" || goto :error

REM Paso 2: construir la imagen Docker
powershell -NoProfile -Command "docker build -t %IMAGE_NAME% ." || goto :error

REM Paso 3: etiquetar la imagen
docker tag %IMAGE_NAME% %REPOSITORY_URI%:%IMAGE_TAG% || goto :error

REM Paso 4: autenticar en ECR
for /f "usebackq tokens=*" %%i in (`aws sts get-caller-identity --query Account --output text 2^>NUL`) do set AWS_ACCOUNT=%%i
if not defined AWS_ACCOUNT (
    echo No se pudo obtener el ID de la cuenta AWS. Asegurate de tener las credenciales configuradas.
    goto :error
)

aws ecr get-login-password --region %AWS_REGION% | docker login --username AWS --password-stdin %REPOSITORY_URI% || goto :error

REM Paso 5: hacer push de la imagen
powershell -NoProfile -Command "docker push %REPOSITORY_URI%:%IMAGE_TAG%" || goto :error

REM Paso 6: desplegar la imagen en App Runner
aws apprunner start-deployment --service-arn arn:aws:apprunner:%AWS_REGION%:%AWS_ACCOUNT%:service/VeriFacturFront/279bc793845f42b186d4ef67056b1c0d || goto :error

echo.
echo Despliegue completado correctamente.
popd
endlocal
exit /b 0

:error
echo.
echo Se produjo un error durante el despliegue. Revisa los mensajes anteriores.
popd
endlocal
exit /b 1
