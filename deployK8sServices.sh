#!/usr/bin/env bash
# deploys all Kubernetes services

find ./k8s -name "*.yaml" | while read fname; do
    echo "Deploying $fname"
    kubectl apply -f "$fname"

    echo "Waiting 10s before start of next deployment."
    sleep 10  
done
