# Integration hand-off

## Backend
- Project: `server/ThorntonEnergy.Chat.Api`
- Run: `dotnet run --project server/ThorntonEnergy.Chat.Api/ThorntonEnergy.Chat.Api.csproj`
- Container port: `8080`; local default: ASP.NET Core development port unless `ASPNETCORE_HTTP_PORTS=8080`
- Build: `dotnet build server/ThorntonEnergy.Chat.Api/ThorntonEnergy.Chat.Api.csproj`
- Health: `GET /health/live`, `GET /health/ready`
- Stream API: `POST /api/chat/stream` (customer JWT, SSE response)
- Required config: `Jwt__Key`; `Foundry__Endpoint`; `Foundry__AgentId`; optional `Foundry__ApiKey`; `Services__Customers`, `Services__Metering`, `Services__Billing`, `Services__Outages`
- Foundry limitation: endpoint/agent ID and supported wire/auth contract are owner-supplied. Adapter contract and blocker documented in `docs/CHAT-SETUP.md`; readiness remains 503 until configured.

## Frontend
- Project: `client/`
- Build: `npm --prefix client run build` (or `npm run build` with `client/` as cwd)
- Dev: `npm --prefix client start`
- Chat route: `/account/chat`, customer-only
- API seam: `client/src/app/services/chat-service.ts` (`fetch` SSE client, `POST /api/chat/stream`)
- Mock files to delete: none were added for chat; no chat mock data or duplicate chat models
- Transcript: component memory only; reset/logout/navigation clears it

## API route inventory
- `GET /api/customers/{customerId}` — authenticated customer's own profile only; customer ID comes from validated JWT claims
- `GET /api/meters` — own meters
- `GET /api/meterReadings` — own readings
- `GET /api/bills` — own bills
- `GET /api/outages` — own outage reports
- `POST /api/outages` — only after explicit UI confirmation of exact address + description; ownership derives from JWT
- Chat endpoint itself: `POST /api/chat/stream`
- Explicitly not chat-exposed: admin list/map/update, `POST /api/meterReadings`, bill mutation/payment operations, cross-customer reads

## Database
- Existing app: SQL Server, owned by existing service projects
- Chat microservice: no database, no transcript persistence, no chat migrations
- Integration instruction: create no chat schema and **NO seed data**

## Shared types
- Backend: `server/ThorntonEnergy.Shared` authentication conventions, referenced by Chat API
- Frontend chat types: `client/src/app/services/chat-service.ts`
- Cross-package typed API client: none; browser uses local typed SSE client

## Services and classification
- Essential: Chat API, existing identity API JWT validation, Customers API, Metering API, Billing API, Outages API, Angular/Nginx web
- Enhancement/owner-dependent: Azure AI Foundry provider endpoint and credentials; compatible adapter contract required before runtime chat is available
- Routing: Nginx `/api/chat` to `chat:8080`; Kubernetes internal `ClusterIP` service `chat`; Compose service `chat`
