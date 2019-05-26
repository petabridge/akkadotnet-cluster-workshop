@echo off
REM deploys all Kubernetes services

for %%f in (*.yaml) do (
    echo "Deploying %%~nxf"
    kubectl apply -f "%%~nxf"
)