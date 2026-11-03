# Thornton Energy AI Chat — Project Plan

## Status

Approved by the user, including the authenticated-only chat visibility requirement.

## Goal

Add a customer-facing AI chat experience to Thornton Energy. Implement a
separate ASP.NET Core chat microservice, connect it to the user's existing
Azure AI Foundry project/agent, and add an authenticated Angular chat window.
The first release will use the existing application JWT for customer-scoped
API access, keep conversation history in the browser, and leave provisioning
and deployment secrets to the project owner.

## Confirmed requirements

- Reuse an existing Azure AI Foundry project/agent; endpoint and agent
  identifiers will be supplied during deployment setup.
- Stream chat responses.
- Include a new-chat/reset action, readable formatted messages and numbered
  choices, and loading/error/retry feedback.
- Keep conversation history in browser memory/session only; do not persist
  transcripts in the application database.
- Implement project files, local configuration examples, and Kubernetes/
  Docker routing. Do not provision Azure resources or commit secrets.
- The signed-in customer's existing app JWT must scope customer API operations.
- The bot follows the supplied customer-service prompt. It must not invoke a
  data-modifying tool until the customer explicitly confirms the exact action.

## Architecture

1. Add `server/ThorntonEnergy.Chat.Api`, an ASP.NET Core service targeting the
   same .NET version and using the existing shared authentication conventions.
   It validates the existing application JWT and does not issue or persist
   credentials or conversation history.
2. The Angular app sends the current customer's JWT and the in-memory
   conversation to the chat service. The JWT is used only by the chat service
   for authentication and customer API calls; it is never included in model
   prompts, transcripts, or logs.
3. Use the existing Foundry project/agent through a replaceable, configured
   provider adapter. Keep Foundry endpoint/agent identifiers and any required
   credentials in environment configuration or Azure-managed secrets. No
   Foundry endpoint, key, or agent identifier is assumed or committed.
4. Do not have Foundry call the customer APIs directly using shared OpenAPI
   credentials. The chat service mediates customer API tool operations and
   forwards the authenticated customer's bearer token to existing internal
   APIs. Agent tool requests are allow-listed and customer-scoped; the
   browser's JWT is not exposed as a model tool argument.
5. Add a customer-only `/account/chat` route and chat component/service. Show
   the chat navigation entry only while an authenticated customer is signed in;
   hide it for anonymous visitors and admins. Protect the route with the
   existing customer auth guard. Stream server-sent events (or the compatible
   streaming transport supported by the current Foundry integration), retain
   the transcript in the browser for the active chat only, and clear it on
   reset/logout.
6. Route `/api/chat` through the existing Nginx frontend proxy and Kubernetes
   service network. Add Docker Compose and Kubernetes deployment/service
   configuration, health probes, and non-secret settings consistently with
   the existing microservices.

## First-release tool scope

- Read the authenticated customer's identity/profile, meters, meter readings,
  bills, and outage reports using existing customer-authorized API operations.
- Report an outage only after gathering the affected address/details and
  receiving explicit confirmation. Derive customer identity from the JWT.
- Do not offer bill status changes as payment processing: the existing billing
  API only marks a bill paid and does not initiate a financial transaction.
- Do not offer meter-reading submission: the existing create-reading endpoint
  is admin-only. Do not expose admin, internal, or cross-customer tools.
- Use the existing `docs/foundry-openapi.yaml` as API contract/documentation.
  The Foundry agent's live customer actions will be mediated by the chat
  service rather than directly authenticated OpenAPI requests.

## Setup deliverables

- Chat API project, configuration binding/validation, Foundry adapter,
  authorized tool mediation, streaming endpoint, and focused tests.
- Angular chat component, streaming client, account route/navigation, accessible
  loading/error/confirmation states, and focused tests.
- Compose, Nginx, and Kubernetes wiring, health probes, and documented
  environment-variable names with safe non-secret examples.
- Setup instructions identifying required Foundry project/agent values,
  customer JWT requirements, public HTTPS ingress, deployment secrets, and
  smoke-test steps. Azure resources, Foundry credentials, public ingress/TLS,
  and Foundry agent changes are applied only after their owner-provided values
  and access are available.

## Acceptance criteria

- A customer can open chat only while authenticated and receives only their
  own customer-scoped data.
- The chat navigation entry is hidden when no customer is authenticated,
  including for anonymous visitors and admin users.
- Chat responses stream; reset clears the browser transcript; errors and
  retries do not fabricate success.
- The service does not store transcripts or log bearer tokens.
- Unauthorized, invalid, admin-only, or cross-customer operations are rejected.
- Outage creation cannot happen without explicit confirmation. The bot does
  not claim to process payments or submit customer meter readings.
- Local build/tests and Kubernetes manifest validation pass, and the setup
  steps clearly list unresolved Azure-specific configuration.
