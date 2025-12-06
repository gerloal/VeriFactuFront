@echo off
setlocal enabledelayedexpansion

REM Configuración
set REPOSITORY_URI=340663646958.dkr.ecr.eu-west-1.amazonaws.com/verifactu-portal-repo
set AWS_REGION=eu-west-1
set IMAGE_NAME=verifactu-portal
set IMAGE_TAG=latest
set SERVICE_ARN=arn:aws:apprunner:eu-west-1:340663646958:service/verifactu-portal-service/9a0e748da6e44e3b920c6b1f10584757

set WORKDIR=%~dp0
pushd "%WORKDIR%"

REM Paso 1: compilar el proyecto
powershell -NoProfile -Command "dotnet build" || goto :error

REM Paso 2: asegurar que no queden contenedores locales usando la imagen
powershell -NoProfile -Command "$containers = docker ps -a -q --filter ancestor=%IMAGE_NAME%; if ($containers) { Write-Host 'Eliminando contenedores locales basados en %IMAGE_NAME%...'; docker rm -f $containers | Out-Null }" || goto :error

REM Paso 3: construir la imagen Docker
powershell -NoProfile -Command "docker build -t %IMAGE_NAME% ." || goto :error

REM Paso 4: etiquetar la imagen
docker tag %IMAGE_NAME% %REPOSITORY_URI%:%IMAGE_TAG% || goto :error

REM Paso 5: autenticar en ECR
for /f "usebackq tokens=*" %%i in (`aws sts get-caller-identity --query Account --output text 2^>NUL`) do set AWS_ACCOUNT=%%i
if not defined AWS_ACCOUNT (
    echo No se pudo obtener el ID de la cuenta AWS. Asegurate de tener las credenciales configuradas.
    goto :error
)

aws ecr get-login-password --region %AWS_REGION% | docker login --username AWS --password-stdin %REPOSITORY_URI% || goto :error

REM Paso 6: hacer push de la imagen
powershell -NoProfile -Command "docker push %REPOSITORY_URI%:%IMAGE_TAG%" || goto :error

REM Paso 7: asegurar que el servicio App Runner esté en estado RUNNING
powershell -NoProfile -Command " $serviceArn = '%SERVICE_ARN%'; $maxAttempts = 30; $delaySeconds = 10; for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) { $status = aws apprunner describe-service --service-arn $serviceArn --query 'Service.Status' --output text; if ($LASTEXITCODE -ne 0) { exit 1 } if ($status -eq 'RUNNING') { Write-Host 'Servicio en estado RUNNING.'; exit 0 } if ($status -eq 'PAUSED') { Write-Host 'Servicio en estado PAUSED. Reanudando...'; aws apprunner resume-service --service-arn $serviceArn | Out-Null; if ($LASTEXITCODE -ne 0) { exit 1 } } elseif ($status -in @('DELETED','DELETING','FAILED')) { Write-Error ("Servicio en estado {0}. Debes recrearlo antes de desplegar." -f $status); exit 1 } else { Write-Host ('Estado actual: {0}. Esperando {1} segundos...' -f $status, $delaySeconds) } Start-Sleep -Seconds $delaySeconds } Write-Error 'El servicio no alcanzó el estado RUNNING a tiempo.'; exit 1" || goto :error

REM Paso 8: desplegar la imagen en App Runner
aws apprunner start-deployment --service-arn %SERVICE_ARN% || goto :error

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
