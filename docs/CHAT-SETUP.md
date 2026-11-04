# Customer chat setup and deployment

## What is implemented

- A small light-bulb button in the bottom-right corner of every page, for everyone. Its name is Wattson.
- Separate `server/ThorntonEnergy.Chat.Api` .NET 10 service. It validates the same issuer, audience, and signing key as the existing APIs. The role in the signed token picks the assistant: `Customer` gets the personal assistant; everyone else (visitors, admins) gets the general one, which has no account access and can only read the public rate plans.
- The browser holds the active transcript in component memory. No transcript endpoint, database, or browser storage is used. Reset starts a fresh conversation, and signing in or out starts a new one automatically.
- Server-sent events carry incremental text, confirmation prompts, and safe retry errors. Nginx disables proxy buffering for `/api/chat`.
- An allow-listed tool mediator forwards the authenticated customer's bearer only to internal customer APIs. It derives customer identity from the validated JWT, does not take a customer ID from model arguments, and never puts the JWT in the model request or logs.
- An outage write is paused until the customer confirms the exact address and description displayed in the UI. The service rechecks that both values match the model's requested operation (ignoring extra spaces and capital letters) before forwarding it, and a confirmation is good for one report only.
- The tool allow-list contains no payment/bill update, meter-reading submission, admin, or cross-customer operation.

## Model configuration

The chat service talks to a **model deployment** in Azure AI Foundry using the standard chat-completions API with streaming and tool calling. It does not use a Foundry Agent: the instructions and the tools live in this repository (`CustomerPrompt.cs` and `FoundryAgentClient.cs`), so they are reviewed, tested and versioned with the code, and every tool call is made by this service with the signed-in customer's own token.

| Environment variable | Purpose |
|---|---|
| `FOUNDRY_ENDPOINT` | Full chat-completions URL, e.g. `https://<resource>.services.ai.azure.com/openai/v1/chat/completions` |
| `FOUNDRY_AGENT_ID` | The deployment name chosen in the portal (sent as the request's `model`) |
| `FOUNDRY_API_KEY` | The key from the portal, sent in the `api-key` header |

Keep these in the local ignored `.env` and in the `thornton-secrets` Kubernetes secret. Never commit them. Redirects are disabled so the key is never forwarded to another host. Until the endpoint and deployment name are set, readiness returns 503 and chat shows a friendly "not configured" message.

## How the assistant knows the customer

- When the chat window opens, `GET /api/chat/hello` returns the customer's first name for the greeting (no AI call).
- On every message, the service reads the customer's own profile, rate plan, meters, bills and outages with their token and adds a short snapshot to the assistant's instructions. Anything that cannot be loaded is left out.
- Tools remain available for details the snapshot does not hold (meter readings, rate plans) and for filing an outage.
- Limits: 15 chat requests per customer per minute, 20 messages of up to 2,000 characters per conversation, replies capped at 500 tokens.
- Visitors and admins: one shared pool of 20 requests a minute (the load balancer hides who is who), 10 messages of up to 500 characters, and only the public rate-plans tool.

## Local configuration

1. Copy `.env.example` to `.env` and supply the existing SQL/JWT values needed by the app. Add the owner-provided Foundry endpoint, agent ID, and key as above. `.env` is ignored by git.
2. Build and start the complete app with `docker compose up --build`. Open the configured `WEB_PORT` (default `http://localhost:4200`).
3. Sign in as a customer and click the chat button in the bottom-right corner. It is not shown to anonymous users or admins. `/api/chat/stream` is reached through the web container's Nginx route; the chat container has no host-published port.
4. Verify `GET /health/live` and `GET /health/ready` on the chat service from the cluster network. Readiness intentionally stays 503 until the Foundry endpoint and agent ID are present.
5. Test a read-only question; confirm reset removes the current transcript; test provider/network error and retry behavior; then test an outage through the confirmation UI. Do not treat a UI message as proof of a filed outage—verify the customer-scoped outage list/API.

For direct local development, set `Jwt__Key`, `Foundry__Endpoint`, `Foundry__AgentId`, and optionally `Foundry__ApiKey` using user-secrets or process environment variables; run `dotnet run --project server/ThorntonEnergy.Chat.Api/ThorntonEnergy.Chat.Api.csproj`. Set `Services__Customers`, `Services__Metering`, `Services__Billing`, and `Services__Outages` to reachable internal API URLs. The service listens on the .NET development port unless `ASPNETCORE_HTTP_PORTS=8080` is set.

## Kubernetes / AKS

- `k8s/base/chat.yaml` defines an internal `ClusterIP` service and startup/liveness/readiness probes. The public web deployment proxies `/api/chat` to `chat:8080`.
- Build/push the chat image alongside the existing images. Set the image mapping in the selected Kustomize overlay.
- Add `JWT_KEY`, `FOUNDRY_ENDPOINT`, `FOUNDRY_AGENT_ID`, and `FOUNDRY_API_KEY` to the `thornton-secrets` secret from a secure local or Azure-managed source. Never check secret values into YAML or source control. The existing JWT issuer/audience values are supplied by `appsettings.json`.
- Configure public HTTPS ingress/TLS at the deployment owner boundary. This change does not provision Azure resources, Foundry, credentials, or public ingress.

## Mediated customer API contract

These are the chat service's complete allow-listed internal operations:

| Method and path | Chat capability | Scope |
|---|---|---|
| `GET /api/customers/{customerId}` | `get_profile` | `{customerId}` is derived from the validated JWT |
| `GET /api/meters` | `get_meters` | Existing API scopes to the signed-in customer |
| `GET /api/meterReadings` | `get_meter_readings` | Existing API scopes to the signed-in customer's meters |
| `GET /api/bills` | `get_bills` | Existing API scopes to the signed-in customer |
| `GET /api/outages` | `get_outages` | Existing API scopes to the signed-in customer |
| `GET /api/ratePlans` | `get_rate_plans` | Public plan list |
| `POST /api/outages` | `report_outage` | Only after explicit exact-details confirmation; customer ID omitted and derived by the API from JWT |

Admin list/map/update endpoints, cross-customer identifiers, `POST /api/meterReadings`, and bill mutation/payment-like operations are not registered as chat tools. Existing authorized application routes remain governed by their own service policies.

## Deferred deployment-specific items

- Foundry owner supplies the exact existing project/agent identifiers and compatible HTTPS streaming endpoint/auth contract.
- Store provider credentials and the shared JWT signing key in a secret manager; rotate them per operational policy.
- Configure and validate public HTTPS ingress/TLS, image registry references, and any network egress restrictions.
- Run an owner-approved Foundry/customer smoke test using a non-production customer account.
