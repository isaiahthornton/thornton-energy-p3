# What is left, and what to improve

The app works end to end. This page is the honest list of what we did not get to, roughly in order of value.

## Worth doing next

| What | Why | How, in short |
|---|---|---|
| **HTTPS** | Logins and chat messages currently travel over plain `http`. The Azure address works, but it is not encrypted. | Add an ingress in front of `web`, get a free Let's Encrypt certificate for the Azure DNS name, and make `web` private. Check Azure's current recommended ingress before choosing one. |
| **Azure Key Vault** | Secrets live in a Kubernetes secret made from a local `.env`. That is fine for a project, but a vault adds central control and rotation. | Use the Key Vault secrets driver for the cluster and remove the secret created by hand. |
| **CI/CD** | Nothing runs the tests automatically. | A GitHub Actions workflow: run the server and Angular tests on every pull request, then build and push the images on merge. Keep the Docker Hub token in GitHub secrets. |
| **Database backups** | SQL Server is one pod with one disk. If the disk is lost, so is the data. | Schedule disk snapshots, or move to a managed Azure SQL database per service. |
| **Safer first start** | Each service creates and fills its database when it first starts, so two copies starting at once can collide. | Run migrations and seeding in a one-off Job, then allow more copies of every service. |

## Smaller ideas

- Admin "system health" page with a green or red light per service, built from each `/health/ready`.
- Autoscaling for the website under load, and monitoring (Azure Container Insights costs money).
- Wattson ideas: usage tips from a customer's own readings, an FAQ knowledge file, and admin-only tools.
- Visitors share one chat rate-limit pool because the load balancer hides who is who. An ingress that passes the real client address would allow a per-visitor limit.
- Move the images to Azure Container Registry.

## Things to check or know

- **Swagger.** Every service has a Swagger page, but it is only on in Development mode. To see it locally, put `ASPNETCORE_ENVIRONMENT=Development` in `.env`, run `docker compose up -d`, and open `/swagger` on a service port (8080 to 8085). Paste a login token into the Authorize button.
- **SSMS on the cluster.** Locally, connect to `localhost,14330` as `sa`. On AKS the database is not public. Run `kubectl port-forward svc/db 14331:1433 -n thornton` and connect to `localhost,14331` as `sa`, with the same `SQL_PASSWORD` as the cluster secret. This path is documented but has not been verified on AKS yet. Whoever tests it should note the result here.
- **Known build warnings, left alone on purpose:** a missing decimal precision on a read-only usage row, one query without an explicit order, an AutoMapper licence reminder, and data-protection keys that are not persisted. None affects behaviour.
- **Cost.** The cluster and the model resource bill while they exist. Stop the cluster when idle. After the project, delete the resource group `rg-thornton-p3` (this also removes the hidden `MC_...` group) and the model resource `energy-bot`. The `p3-budget` alert covers only `rg-thornton-p3`, so also add a budget at the subscription level.
- **Docker images.** The service and database images are on Docker Hub under `michaelgeo29`, and the website and chat images under `isaiahthornton`. The versions in use are listed in `k8s/aks/kustomization.yaml` and `docker-compose.yml`.
