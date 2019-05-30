@echo off
REM deploys all Kubernetes services

for %%f in (k8s/*.yaml) do (
    echo "Deploying %%~nxf"
    kubectl apply -f "k8s/%%~nxf"

    echo "Waiting 10s before start of next deployment."
    echo "Press any key to continue without waiting."
    TIMEOUT /T 10
)