@echo off
REM deploys all Kubernetes services

for %%f in (k8s/*.yaml) do (
    echo "Deploying %%~nxf"
    kubectl apply -f "k8s/%%~nxf"
)