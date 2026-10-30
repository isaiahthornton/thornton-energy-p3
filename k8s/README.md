# Kubernetes

Same app as `docker-compose.yml`, described for Kubernetes. One set of files in `base/`, two lists of image names:
`local/` uses the images `docker compose build` made on your laptop, `aks/` pulls versioned images from Docker Hub.

| Piece | Copies | Reached at |
|---|---|---|
| db (SQL Server + 8 GB disk) | 1 | `db:1433` inside the cluster only |
| api, customers, billing, outages, metering, messages | 1 each (customers scaled to 2 after first start) | `<name>:8080` inside the cluster only |
| web (Angular + nginx) | 2 | the only public address (LoadBalancer) |

Service names match compose, so `client/nginx.conf` and every connection string work unchanged.
Secrets are never in git: they are created from your local `.env`.

## Run on Docker Desktop Kubernetes

```powershell
docker compose build                                   # make sure the local images are current
kubectl config use-context docker-desktop
kubectl create namespace thornton
kubectl create secret generic thornton-secrets -n thornton --from-env-file=.env
kubectl apply -k k8s/local
kubectl get pods -n thornton -w                        # wait until every pod is 1/1 Running (Ctrl+C to stop watching)
kubectl scale deployment customers --replicas=2 -n thornton
```

Open http://localhost (port 80). If port 80 is taken: `kubectl port-forward svc/web 4300:80 -n thornton`, then http://localhost:4300.
SSMS: `kubectl port-forward svc/db 14331:1433 -n thornton`, then connect to `localhost,14331` as `sa`.

## Demo: self-healing

```powershell
kubectl get pods -n thornton
kubectl delete pod <one of the customers pods> -n thornton
kubectl get pods -n thornton -w                        # a replacement starts; the site keeps working
```

## Remove it

```powershell
kubectl delete namespace thornton                      # deletes everything, including the database disk
```

## AKS

Same commands with `kubectl apply -k k8s/aks`, after the three Docker Hub account names in `aks/kustomization.yaml` are filled in
and the cluster is created (team agrees size and cost first).
