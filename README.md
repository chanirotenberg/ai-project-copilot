# AI Project Copilot

Production-oriented full-stack SaaS for project management, evolving toward AI-assisted
project memory, agent tools, human-in-the-loop approvals, GitHub integration, and cloud
deployment.

**Current phase:** Phase 0 (Backend Foundation) complete — see `MASTER_PLAN.md` for the
full roadmap and `CLAUDE.md` for engineering rules.

## Stack

- .NET 10 / ASP.NET Core (Clean Architecture: Domain, Application, Infrastructure, Api)
- PostgreSQL 17 + Entity Framework Core
- Docker Compose (local PostgreSQL)

## Prerequisites

- .NET SDK 10
- Docker

## Local Setup

### 1. Start PostgreSQL

```powershell
docker compose up -d
```

This starts PostgreSQL 17 in a container named `projectcopilot-postgres`, exposed on host
port `5433` (mapped to container port `5432`). Database: `projectcopilot`, user: `postgres`.

> Host port `5433` is used deliberately because `5432` may already be occupied by another
> local PostgreSQL installation. Do not change this back to `5432`.

### 2. Apply migrations

```powershell
dotnet ef database update `
  --project .\src\ProjectCopilot.Infrastructure\ProjectCopilot.Infrastructure.csproj `
  --startup-project .\src\ProjectCopilot.Api\ProjectCopilot.Api.csproj
```

### 3. Configure JWT settings

The API requires a `Jwt` configuration section — `Issuer`, `Audience`, `Key` (minimum
32 bytes/UTF-8), and `AccessTokenExpiryMinutes` — used to sign access tokens issued by
`POST /api/v1/auth/login`. Configure it in `appsettings.Development.json` (gitignored,
not committed) or via environment variables, for example:

```json
{
  "Jwt": {
    "Issuer": "ProjectCopilot.Dev",
    "Audience": "ProjectCopilot.Dev",
    "Key": "<a random 32+ byte string>",
    "AccessTokenExpiryMinutes": 60
  }
}
```

If `Jwt:Key` is missing or shorter than 32 bytes, or `Jwt:Issuer`/`Jwt:Audience` are
empty, the API fails fast at startup — this is by design, not a bug, so a
misconfigured signing key is never silently accepted.

> Never commit a real signing key. `appsettings.json` (committed) intentionally has no
> `Jwt` section, mirroring the existing convention for `ConnectionStrings`.

`dotnet ef` migration commands and `--seed-demo` do **not** require this config — they
don't start the full host pipeline — so CI's migration step works without a `Jwt` key
configured.

### 4. Run the API

```powershell
dotnet run --project .\src\ProjectCopilot.Api\ProjectCopilot.Api.csproj
```

Swagger UI is available in the Development environment at the root of the API host.

## Demo Seed

The demo seed is explicit — it does **not** run automatically on API startup.

```powershell
dotnet run --project .\src\ProjectCopilot.Api\ProjectCopilot.Api.csproj -- --seed-demo
```

- The seed is idempotent: running it multiple times will not create duplicate data.
- It works against an empty database.
- Demo project: `Checkout Platform V2` (`980f644c-039f-4031-80b0-c07ff3d14993`).
- Demo user id (referenced as `CreatedByUserId`): `11111111-1111-1111-1111-111111111111`.

## Build & Test

```powershell
dotnet build .\ProjectCopilot.slnx
dotnet test .\ProjectCopilot.slnx --configuration Release
```

## API Overview

Projects:
- `GET /api/v1/projects`
- `GET /api/v1/projects/{id}`
- `POST /api/v1/projects`

Tasks:
- `GET /api/v1/tasks?projectId={projectId}`
- `GET /api/v1/tasks/{id}`
- `POST /api/v1/tasks`
- `PUT /api/v1/tasks/{id}`

## CI

GitHub Actions (`.github/workflows/ci.yml`) runs on every push: checkout, .NET 10 setup,
PostgreSQL 17 service container, restore, build (Release), apply migrations, run tests.

## Architecture

Clean Architecture / Modular Monolith. Dependency direction:

```text
Domain <- Application <- Infrastructure <- Api
```

See `CLAUDE.md` for full engineering and security rules, and `MASTER_PLAN.md` for the
phase-by-phase execution roadmap.
