# Kubernetes

Same app as `docker-compose.yml`, described for Kubernetes. One set of files in `base/`, two lists of image names:
`local/` uses the images `docker compose build` made on your laptop, `aks/` pulls versioned images from Docker Hub.

| Piece | Copies | Reached at |
|---|---|---|
| db (SQL Server + 8 GB disk) | 1 | `db:1433` inside the cluster only |
| api, customers, billing, outages, metering, messages, chat | 1 each (customers scaled to 2 after first start) | `<name>:8080` inside the cluster only |
| web (Angular + nginx) | 2 | the only public address (LoadBalancer) |

Service names match compose, so `client/nginx.conf` and every connection string work unchanged.
Secrets are never in git: they are created from your local `.env`. For chat, the secret also needs
`FOUNDRY_ENDPOINT`, `FOUNDRY_AGENT_ID` and `FOUNDRY_API_KEY`; see [CHAT-SETUP.md](../docs/CHAT-SETUP.md).
The chat pod reports "not ready" until the endpoint and deployment name are present.

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

The AKS list pulls the published images from Docker Hub (see `aks/kustomization.yaml`). The cluster is `aks-thornton-p3` in the resource group `rg-thornton-p3`.

```powershell
az aks start -g rg-thornton-p3 -n aks-thornton-p3              # takes a few minutes
az aks get-credentials -g rg-thornton-p3 -n aks-thornton-p3
kubectl create namespace thornton
kubectl create secret generic thornton-secrets -n thornton --from-env-file=.env
kubectl apply -k k8s/aks
kubectl get pods -n thornton -w
kubectl get svc web -n thornton                                # the public address
```

- **The address:** the `web` service has an Azure DNS name, `thornton-energy.centralus.cloudapp.azure.com`.
- **If the namespace or secret already exists:** do not recreate them. The database password was set the first time the database started, so replacing `SQL_PASSWORD` would lock the services out. To add or change only the chat values, patch the existing secret instead.
- **Swagger and SSMS:** see [docs/NEXT-STEPS.md](../docs/NEXT-STEPS.md).
- **When idle:** `az aks stop -g rg-thornton-p3 -n aks-thornton-p3`. A stopped cluster still bills a little for its disks and public address.
