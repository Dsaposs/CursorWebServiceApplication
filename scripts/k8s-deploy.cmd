@echo off
setlocal
cd /d "%~dp0.."

echo Applying Kubernetes manifests...
kubectl apply -k k8s

echo.
echo Waiting for deployment rollout...
kubectl rollout status deployment/notes-api -n notes

echo.
echo To open the app locally, run:
echo kubectl port-forward service/notes-api 5294:80 -n notes
@echo off
setlocal
cd /d "%~dp0.."

echo Applying Kubernetes manifests...
kubectl apply -k k8s

echo.
echo Waiting for deployment rollout...
kubectl rollout status deployment/notes-api -n notes

echo.
echo To open the app locally, run:
echo kubectl port-forward service/notes-api 5294:80 -n notes
