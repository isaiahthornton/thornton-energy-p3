# Customer chat setup and deployment

## What is implemented

- Angular customer chat at `/account/chat`, protected by the account route's customer-role guard. The chat links render only for a signed-in customer; they are absent for anonymous visitors and admins.
- Separate `server/ThorntonEnergy.Chat.Api` .NET 10 service. It validates the same issuer, audience, and signing key as the existing APIs, and only allows the `Customer` role to call chat.
- The browser holds the active transcript in component memory. No transcript endpoint, database, or browser storage is used. Reset starts a fresh conversation; leaving the page or logging out discards the component's transcript.
- Server-sent events carry incremental text, confirmation prompts, and safe retry errors. Nginx disables proxy buffering for `/api/chat`.
- An allow-listed tool mediator forwards the authenticated customer's bearer only to internal customer APIs. It derives customer identity from the validated JWT, does not take a customer ID from model arguments, and never puts the JWT in the model request or logs.
- An outage write is paused until the customer confirms the exact address and description displayed in the UI. The service rechecks that both values exactly match the model's requested operation before forwarding it.
- The tool allow-list contains no payment/bill update, meter-reading submission, admin, or cross-customer operation.

## Foundry configuration — owner action required

No Foundry endpoint, project ID, agent ID, deployment name, or credential was available during implementation. None is fabricated or committed. Set these values only in local ignored `.env` or in deployment secret management:

| Environment variable | Purpose |
|---|---|
| `FOUNDRY_ENDPOINT` | Complete HTTPS URL of the configured Foundry-compatible streaming endpoint |
| `FOUNDRY_AGENT_ID` | Existing agent identifier used as the request's `model` value |
| `FOUNDRY_API_KEY` | Endpoint credential, if required by the owner-configured endpoint |

`FoundryAgentClient` is the replaceable provider adapter. Its wire contract is OpenAI-compatible streaming chat completions over HTTPS: the endpoint accepts `{ model, stream: true, messages, tools, tool_choice }`, returns SSE `data:` events containing `choices[0].delta.content` and/or `choices[0].delta.tool_calls`, and ends with `[DONE]`. The adapter parses tool calls and relays text while streaming; automatic HTTP redirects are disabled so configured credentials are not forwarded to a redirect target.

Foundry agent APIs vary by project/agent version and may require a project-scoped endpoint, Entra ID, different request/response semantics, or a separately deployed model. Because the actual Foundry values and supported endpoint contract are unavailable, this integration does **not** assert that a raw Foundry project URL accepts this adapter contract. The Foundry owner must provide a compatible endpoint and authentication mode or replace `IFoundryAgentClient`/`FoundryAgentClient` with the matching Foundry SDK/adapter before enabling chat. Until `FOUNDRY_ENDPOINT` and `FOUNDRY_AGENT_ID` are configured, readiness returns HTTP 503 and chat responses return a configuration error. No Foundry SDK package was added whose compatibility would otherwise depend on an unavailable project/agent deployment value.

## Local configuration

1. Copy `.env.example` to `.env` and supply the existing SQL/JWT values needed by the app. Add the owner-provided Foundry endpoint, agent ID, and key as above. `.env` is ignored by git.
2. Build and start the complete app with `docker compose up --build`. Open the configured `WEB_PORT` (default `http://localhost:4200`).
3. Sign in as a customer. The chat link is not shown to anonymous users or admins. `/api/chat/stream` is reached through the web container's Nginx route; the chat container has no host-published port.
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
| `POST /api/outages` | `report_outage` | Only after explicit exact-details confirmation; customer ID omitted and derived by the API from JWT |

Admin list/map/update endpoints, cross-customer identifiers, `POST /api/meterReadings`, and bill mutation/payment-like operations are not registered as chat tools. Existing authorized application routes remain governed by their own service policies.

## Deferred deployment-specific items

- Foundry owner supplies the exact existing project/agent identifiers and compatible HTTPS streaming endpoint/auth contract.
- Store provider credentials and the shared JWT signing key in a secret manager; rotate them per operational policy.
- Configure and validate public HTTPS ingress/TLS, image registry references, and any network egress restrictions.
- Run an owner-approved Foundry/customer smoke test using a non-production customer account.
