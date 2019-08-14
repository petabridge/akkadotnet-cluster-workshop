#!/usr/bin/env bash
# destroys all K8s services

kubectl -n akka-cqrs delete statefulsets,deployments,po,svc --all    