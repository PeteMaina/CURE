# CURE

CURE is The World Class CRM: a customer relationship and revenue intelligence platform.

## Structure

- `frontend/`: React, JavaScript, React Router, Material UI, TanStack Query, webpack.
- `backend/CURE.Api/`: ASP.NET Core HTTP host.
- `backend/CURE.Application/`: application contracts and use cases.
- `backend/CURE.Domain/`: domain rules and value objects.
- `backend/CURE.Infrastructure/`: PostgreSQL persistence, schema bootstrap, and infrastructure adapters.
- `backend/CURE.Worker/`: background-process host.
- `tests/`: domain and integration test projects.

## Prerequisites

- Node.js 18 or newer and npm
- .NET SDK 10
- Docker Desktop with Compose

## Run The Full Development Stack

From the repository root, start PostgreSQL and Redis:

```powershell
docker compose up -d postgres redis
```

In a second terminal, start the API:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project backend/CURE.Api
```

In a third terminal, start the worker:

```powershell
$env:DOTNET_ENVIRONMENT = "Development"
dotnet run --project backend/CURE.Worker
```

In a fourth terminal, start the frontend:

```powershell
cd frontend
npm install
npm start
```

Open `http://localhost:3000`. The frontend proxies `/api` to `http://localhost:8080` by default. Override it with:

```powershell
$env:CURE_API_PROXY = "http://localhost:5000"
npm start
```

The development API uses the seeded tenant and administrator context. The development password is `cure-development-only`; override it with `Authentication__DevelopmentPassword` or `CURE_DEV_PASSWORD` through your local environment. Never use development credentials outside local development.

The API and worker use a separate, non-owner `cure_app` PostgreSQL role for normal queries. Schema initialization uses the development migration connection. If PostgreSQL was initialized before this role split, recreate the local development database volume or provision the role and grants from a privileged local connection before starting the API. Do not use the development role or credentials in a deployed environment.

Production requires `Authentication__Authority`, `Authentication__Audience`, `Database__ConnectionString` for a non-superuser/non-owner runtime role, `Database__RuntimeRoleName` for least-privilege grants, and `Database__MigrationConnectionString` for the deployment migration identity. Configure the OIDC provider to issue CURE tokens with UUID `tenant_id` and `user_id` claims plus an approved `role` claim. No signing-key, issuer, or audience fallback is enabled in Production. Frontend public OIDC settings are `CURE_OIDC_AUTHORITY`, `CURE_OIDC_CLIENT_ID`, and optionally `CURE_OIDC_SCOPE`; use Authorization Code with PKCE and configure the registered callback as `/auth/callback`.

Stop dependencies with:

```powershell
docker compose down
```

Add `-v` only when you intentionally want to delete local PostgreSQL and Redis data.

## Frontend Commands

```powershell
cd frontend
npm install
npm start
npm run build
```

The frontend uses `/api/v1` by default. Set `CURE_API_BASE_URL` when the API is hosted elsewhere.

## Backend Commands

```powershell
dotnet restore CURE.sln
dotnet build CURE.sln
dotnet test CURE.sln
```

Useful endpoints during development:

- `GET /` API process check
- `GET /health/live` process liveness
- `GET /health/ready` readiness endpoint
- `/swagger` OpenAPI UI in Development
- `POST /api/v1/auth/development-token` development JWT issuance
- `GET /api/v1/customers` tenant-scoped customer collection

## Current Phase 2 Scope

Implemented vertical slice:

- Runnable API and worker hosts.
- JWT validation and development authentication path.
- Tenant request context and PostgreSQL RLS policies.
- Customer list/search/pagination, create, detail, duplicate checks, optimistic concurrency, and audit persistence.
- Frontend loading, empty, error, retry, form validation, and session UI.

The Phase 2 resource routes now cover organizations, contacts, leads, opportunities, activities, cases, saved views, and customers with tenant-scoped list/get/create/update/delete operations, pagination, search, forms, optimistic-concurrency headers, duplicate checks where defined, and audit events committed in the same transaction as each mutation.

The API now supports provider-neutral OIDC validation in production and the frontend uses Authorization Code with PKCE when public OIDC settings are configured. A real identity-provider configuration and end-to-end sign-in test are still required before deployment. MFA enforcement, session revocation/device management, granular RBAC/ABAC policy matrices, and domain-specific lifecycle policies such as lead conversion, opportunity stage transition validation, case SLA enforcement, and saved-view permission sharing remain hardening work before production release.

## Development Status

Still in Development.

- Contact: devpetermaina@gmail.com
- Visit: https://mainapeter.netlify.app

