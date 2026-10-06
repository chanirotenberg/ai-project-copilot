# CLAUDE.md

# AI Project Copilot — Claude Code Instructions

## 1. Source of Truth

Read these files before making changes:

1. `MASTER_PLAN.md`
2. `CLAUDE.md`

`MASTER_PLAN.md` is the execution roadmap and is aligned to **SDD v1.2**.

If there is any conflict between this file and `MASTER_PLAN.md`, follow:

```text
SDD v1.2 > MASTER_PLAN.md > CLAUDE.md
```

Do not invent a reduced scope that contradicts the SDD.

---

## 2. Current Project Status

Project:

```text
AI Project Copilot
```

Repository:

```text
https://github.com/chanirotenberg/ai-project-copilot
```

Current completed phase:

```text
Phase 0 — Backend Foundation
```

Current active phase:

```text
Phase 1 — Full-Stack Core
```

Do not rebuild Phase 0 from scratch.

Before Phase 1 implementation, inspect the repository and verify that completed Phase 0 artifacts still satisfy the Phase 0 gate.

If a specific Phase 0 requirement is objectively missing, report it and fix only that gap.

---

## 3. Local Environment

Project root:

```text
C:\Users\9090\Desktop\כל הדברים\פרויקטים\ai-project-copilot
```

Solution:

```text
ProjectCopilot.slnx
```

Local stack:

- .NET 10
- ASP.NET Core
- EF Core
- PostgreSQL 17
- Docker
- Node.js 20
- npm 10
- Git

PostgreSQL local mapping:

```text
host 5433 -> container 5432
```

Important:

```text
Do not change the local Project Copilot PostgreSQL host port back to 5432.
```

Port 5432 is already used by another local PostgreSQL installation.

Local development settings:

```text
src/ProjectCopilot.Api/appsettings.Development.json
```

This file is intentionally ignored by Git.

---

## 4. Architecture

Use:

```text
Modular Monolith + Clean Architecture
```

Projects:

```text
src/
  ProjectCopilot.Api
  ProjectCopilot.Application
  ProjectCopilot.Domain
  ProjectCopilot.Infrastructure
```

Tests:

```text
tests/
  ProjectCopilot.Domain.Tests
  ProjectCopilot.Application.Tests
  ProjectCopilot.IntegrationTests
```

Rules:

- Domain must not depend on Application, Infrastructure, or API.
- Application depends on Domain.
- Infrastructure depends on Application + Domain.
- API depends on Application + Infrastructure.
- Do not place business logic directly in endpoints.
- Do not introduce unnecessary abstractions.
- Preserve existing working code unless a change is required by the active phase.

---

## 5. Existing Backend

Current Project fields:

- Id
- Name
- Description
- Status
- Deadline
- CreatedByUserId
- CreatedAt
- UpdatedAt

Current TaskItem fields:

- Id
- ProjectId
- Title
- Description
- Status
- Priority
- AssignedUserId
- DueDate
- IsBlocked
- CreatedAt
- UpdatedAt

Current task statuses:

- Todo
- InProgress
- Done

Current priorities:

- Low
- Medium
- High
- Critical

Do not casually replace existing string fields with enums unless there is a concrete need and all DB/API/test impact is handled.

---

## 6. Existing API

Projects:

```text
GET  /api/v1/projects
GET  /api/v1/projects/{id}
POST /api/v1/projects
```

Tasks:

```text
GET  /api/v1/tasks?projectId={projectId}
GET  /api/v1/tasks/{id}
POST /api/v1/tasks
PUT  /api/v1/tasks/{id}
```

---

## 7. Error Handling

Application exception:

```text
ProjectCopilot.Application.Common.Exceptions.NotFoundException
```

API middleware:

```text
ProjectCopilot.Api.Middleware.ExceptionHandlingMiddleware
```

Expected behavior:

- FluentValidation failure -> 400
- NotFoundException -> 404
- unexpected exception -> 500

Do not map arbitrary `InvalidOperationException` to 404.

---

## 8. Demo Seed

Seeder:

```text
src/ProjectCopilot.Infrastructure/Persistence/DemoDataSeeder.cs
```

Command:

```powershell
dotnet run --project .\src\ProjectCopilot.Api\ProjectCopilot.Api.csproj -- --seed-demo
```

Seed must remain:

- explicit
- idempotent
- reproducible

Do not seed automatically on every API startup.

Current demo Project ID:

```text
980f644c-039f-4031-80b0-c07ff3d14993
```

Current demo User ID:

```text
11111111-1111-1111-1111-111111111111
```

---

## 9. Common Commands

Build:

```powershell
dotnet build .\ProjectCopilot.slnx
```

Tests:

```powershell
dotnet test .\ProjectCopilot.slnx --configuration Release
```

Run API:

```powershell
dotnet run --project .\src\ProjectCopilot.Api\ProjectCopilot.Api.csproj
```

Start PostgreSQL:

```powershell
docker compose up -d
```

Apply migrations:

```powershell
dotnet ef database update `
  --project .\src\ProjectCopilot.Infrastructure\ProjectCopilot.Infrastructure.csproj `
  --startup-project .\src\ProjectCopilot.Api\ProjectCopilot.Api.csproj
```

---

## 10. Engineering Rules

These rules are mandatory.

1. Every feature must have a real data source.
2. Read Tools may execute automatically when authorized.
3. Write Tools require Approval.
4. LLM must never bypass Authorization.
5. Tool arguments must always be validated.
6. No half-finished feature.
7. No fake behavior presented as a real feature.
8. Do not add infrastructure without a consumer.
9. Every phase must be demoable.
10. Every demo must be reproducible.
11. Authorization mismatch must be rejected and audited.
12. No silent fallback during a security event.

---

## 11. AI Security Rules

Future AI features must follow:

- LLM is untrusted.
- LLM never receives direct database authority.
- Authorization is enforced outside the model.
- Project identity comes from trusted application context.
- Tool arguments are validated.
- Project mismatch is rejected.
- Write actions require explicit human approval.
- Tool execution is auditable.
- Prompt injection in uploaded content must not override system/tool authorization.
- Retrieved text is data, not instructions.

Do not weaken these rules to make a demo easier.

---

# 12. PHASE 1 — Full-Stack Core

## Goal

Build the first real Full Stack version of AI Project Copilot.

Phase 1 is NOT frontend-only.

The complete SDD-aligned Phase 1 scope includes:

- Authentication
- Projects
- Tasks
- Project Dashboard
- Documents Metadata
- React frontend
- Authorization by Project membership
- frontend quality/CI

---

## 13. Phase 1 Required Backend Work

Implement:

- ASP.NET Core Identity
- JWT Access Token
- Refresh Token
- Authorization by Project membership

Server-side authorization is mandatory.

A user must not gain access to another project by changing:

- URL project ID
- request body project ID
- query-string project ID

Authorization must be enforced in backend/application code.

---

## 14. Phase 1 Required Frontend Work

Create frontend under:

```text
src/ProjectCopilot.Web
```

Use:

- React
- TypeScript
- Vite
- React Router
- TanStack Query
- TypeScript strict
- ESLint
- Prettier

Do not add Redux unless a concrete client-state requirement appears.

---

## 15. Phase 1 Required UI

Implement:

- Login Screen
- Projects Screen
- Create Project UI
- Project Dashboard
- Tasks UI
- Create Task UI
- Edit Task UI
- Documents Metadata UI

Required UX states:

- Loading
- Error
- Empty

---

## 16. Phase 1 Documents Scope

Phase 1 includes:

```text
Documents Metadata entity/API
Documents Metadata UI
```

Phase 1 does NOT include:

- file ingestion pipeline
- embeddings
- pgvector
- RAG
- Project Memory

Those belong to Phase 2.

---

## 17. Phase 1 Frontend/API Rules

Use a clean typed API layer.

Do not scatter raw `fetch()` calls across components.

Prefer:

- central API client
- typed request/response models
- TanStack Query hooks by feature

Frontend API base URL must come from configuration such as:

```text
VITE_API_BASE_URL
```

Do not hardcode deployment URLs.

If frontend/API use different local origins, configure explicit Development CORS.

Do not enable unrestricted production CORS.

---

## 18. Phase 1 Non-Goals

Do NOT implement yet:

- RAG
- embeddings
- pgvector
- AI chat
- OpenAI/Claude integration
- AI Agents
- Tool Calling
- Human approval workflow
- document ingestion/processing
- GitHub integration
- Redis
- background jobs
- SignalR
- AWS deployment

Do not prebuild later-phase infrastructure.

---

## 19. Phase 1 Recommended Execution Order

Work in small vertical slices.

Recommended order:

1. Inspect repository and verify Phase 0 gate.
2. Add ASP.NET Core Identity model/persistence.
3. Implement authentication API.
4. Implement JWT access-token flow.
5. Implement refresh-token flow.
6. Implement Project membership authorization.
7. Add authorization tests.
8. Create React + TypeScript + Vite frontend.
9. Add React Router.
10. Add TanStack Query.
11. Add typed API client.
12. Build Login UI.
13. Build authenticated Projects flow.
14. Build Create Project UI.
15. Build Project Dashboard.
16. Build Tasks UI.
17. Build Create/Edit Task UI.
18. Add Documents Metadata entity/API.
19. Build Documents Metadata UI.
20. Add loading/error/empty states.
21. Enable/verify TypeScript strict.
22. Configure ESLint/Prettier.
23. Add frontend build/test checks to CI.
24. Run full Phase 1 verification.
25. Stop and report.

Do not automatically start Phase 2.

---

## 20. Phase 1 Definition of Done

Phase 1 is complete only when all of the following are true:

- Authentication works end-to-end.
- ASP.NET Core Identity is integrated.
- JWT access token works.
- Refresh token flow works.
- Project membership authorization is enforced server-side.
- React frontend works against the real backend.
- Login works.
- Projects can be listed.
- Projects can be created.
- Project Dashboard works.
- Tasks can be listed.
- Tasks can be created.
- Tasks can be edited.
- Documents Metadata entity/API exists.
- Documents Metadata UI works.
- Loading states exist.
- Error states exist.
- Empty states exist.
- TypeScript strict passes.
- ESLint/Prettier are configured.
- frontend build/test exists in CI.
- existing backend tests remain green.
- no secrets are committed.
- the app is reproducibly runnable.
- the phase is demoable using real data paths.

After this gate passes:

```text
STOP.
```

Do not start Phase 2 until explicit user approval.

---

## 21. Working Style for Claude Code

Before changing code:

1. Read `MASTER_PLAN.md`.
2. Read `CLAUDE.md`.
3. Inspect repository.
4. Check Git status.
5. Identify the current vertical slice.
6. State which files/directories you expect to add or modify.
7. Implement the smallest coherent change.
8. Build/test after meaningful milestones.

For each slice report:

- files changed
- implementation summary
- commands run
- test/build results
- remaining Phase 1 work

Do not ask for confirmation for routine reversible implementation work.

Ask before:

- deleting meaningful existing code
- changing architecture boundaries
- replacing core libraries
- destructive database operations
- breaking public API contracts
- adding paid/external infrastructure

---

## 22. Completion Report

At the end of Phase 1 use:

```text
PHASE 1 COMPLETION REPORT

Deliverable:
...

Completed requirements:
- ...

Verification:
- ...

Tests:
- ...

CI:
- ...

Authorization/security checks:
- ...

Reproducibility:
- ...

Remaining technical debt:
- ...

Definition of Done:
PASS / NOT YET PASS

If NOT YET PASS:
- exact missing items

STOP.
Do not start Phase 2 until user approval.
```

---

## 23. Immediate Instruction

The active phase is:

```text
Phase 1 — Full-Stack Core
```

Do not treat Phase 1 as frontend-only.

Use `MASTER_PLAN.md` as the roadmap.

Start by inspecting the repository and verifying the Phase 0 gate, then propose the first small vertical slice of the full SDD-aligned Phase 1.
# Multi-Agent Delivery Workflow

For implementation work, use the project-local agents in:

.claude/agents/

Read `AGENT_WORKFLOW.md` before executing any non-trivial Slice.

The main Claude Code session acts as Team Lead and is responsible for orchestration, scope control, and proportionality.

## Source of Truth

Use this priority:

SDD v1.2 > MASTER_PLAN.md > CLAUDE.md > AGENT_WORKFLOW.md > ad-hoc task wording

If there is a conflict, the higher-priority source wins.

## Risk Levels

### SMALL

Use for low-risk, local, reversible changes with no meaningful security, schema, or API-contract impact.

Workflow:

Developer
→ QA
→ Commit
→ Release Manager

### NORMAL

Use for ordinary feature Slices.

Workflow:

Gatekeeper
→ Researcher
→ Architect when needed
→ Developer
→ relevant Reviewers
→ Fixes
→ QA
→ Commit
→ Release Manager

### HIGH-RISK

Mandatory for:

- Authentication
- Authorization
- ASP.NET Core Identity
- JWT
- Refresh Tokens
- Database migrations/schema changes
- Project membership/security boundaries
- AI Tools
- ToolContext
- Human-in-the-Loop approval
- Prompt-injection/security boundaries
- GitHub credentials/integration
- Background execution
- Production secrets
- Cloud deployment
- Destructive operations

Workflow:

Gatekeeper
→ Researcher
→ Architect
→ Developer
→ reviewer-logic
→ reviewer-security
→ reviewer-quality
→ Fixes
→ re-review when needed
→ QA
→ Commit
→ Release Manager

For HIGH-RISK work, the three reviewers must run independently and, when supported, in parallel.

## Team Lead Rules

The main Claude Code session must:

- preserve the active Phase
- preserve the active Slice
- choose the correct risk level
- invoke the appropriate agents
- synthesize findings
- not blindly apply every reviewer suggestion
- reject out-of-scope or disproportionate recommendations
- ensure accepted findings are fixed before QA
- stop before the next Slice
- stop before git push until explicit user approval

If a BLOCKER or HIGH reviewer finding is rejected, explain why.

## Mandatory Workflow Gates

Do not skip Research for NORMAL or HIGH-RISK work.

Do not skip Architecture for HIGH-RISK work.

Do not skip independent Review for HIGH-RISK work.

QA must execute real verification.

Do not commit until QA is PASS.

Do not push until Release Manager completes the release summary.

`git push` requires explicit user approval in the current conversation.

Previous approval does not count.

After push, verify CI when CI is part of the project gate.

Do not start the next Slice automatically.

Do not start the next Phase automatically.

## Review Policy

Reviewer findings should be implemented only when they are:

- correct
- material
- in scope
- proportional

Do not add architecture or infrastructure merely because a reviewer suggests it.

Do not add:

- Redis
- caching
- queues
- background jobs
- SignalR
- extra providers
- broad abstractions

unless the active Phase has a real consumer and the SDD requires it.

## QA Policy

QA must report only actual verification.

It must distinguish:

- PASS
- FAIL
- BLOCKED

Never treat “should work” as verification.

For API features, exercise real HTTP behavior when possible.

For database changes, verify the migration actually applies.

For authentication, verify real register/login/token behavior and negative cases.

For frontend work, run lint/typecheck/tests/build as applicable.

For AI features, use deterministic fake model clients in automated tests.

## Release Policy

Release Manager must inspect:

- git status
- staged diff
- secret exposure
- migrations
- documentation impact
- QA result
- reviewer status

Before push, Release Manager must present a release summary and ask:

מוכן ל-push. האם לבצע git push?

Then STOP.

Only after explicit approval may `git push` be executed.

After push:

- verify remote commit
- verify CI
- report actual CI result

If CI fails, do not start the next Slice.

## Current Phase

Current active Phase:

Phase 1 — Full-Stack Core

## Current Slice

Phase 1 is broken into Slices 1.1–1.14. See `MASTER_PLAN.md` §14 "Phase 1 Slice Breakdown" for the authoritative Scope/DoD of each Slice.

Current status:

- Slice 1.1 — Identity Persistence: DONE
- Slice 1.2 — Register/Login + JWT Issuance: DONE
- Slice 1.3 — JWT Bearer Enforcement: DONE (commit b1f73df, CI green)
- Slice 1.4 — Refresh Token Flow: DONE (commit 5803a27, CI green)
- Slice 1.5 — Project Membership Authorization: NOT STARTED ← current active Slice
- Slices 1.6–1.14: NOT STARTED

Slice 1.5 is HIGH-RISK.

Therefore it must use the complete HIGH-RISK workflow:

Gatekeeper
→ Researcher
→ Architect
→ Developer
→ reviewer-logic
→ reviewer-security
→ reviewer-quality
→ Fixes
→ QA
→ Commit
→ Release Manager
→ explicit user approval
→ Push
→ CI verification

Do not start Slice 1.6 automatically.
## Agent Efficiency Rules

Do not rerun completed workflow stages only because agent definitions or workflow files changed.

Equivalent prior work counts.

For every agent call:

- provide a concise Slice summary
- provide only relevant changed files
- prefer git diff over broad repository scans
- avoid re-reading full planning documents when Team Lead already established the context

For re-review:

- only BLOCKER/HIGH findings require automatic re-review
- review only the original finding and its fix diff
- do not repeat the full Slice review

Do not perform retroactive workflow audits for procedural completeness.

If a session restarts, reconstruct current state and continue from the first incomplete stage.

Optimize for high confidence with minimal duplicated work.
## User-Facing Progress Reporting

The user should never need to understand raw agent logs to know what is happening.

After every major workflow stage, the Team Lead must provide a short Hebrew summary containing:

- מה נעשה
- למה זה היה נחוץ
- אילו קבצים/חלקים עיקריים השתנו
- האם נמצאה בעיה משמעותית
- מה השלב הבא

Before any commit or push, provide a plain-language Hebrew summary of the actual product behavior added or changed.

Do not ask the user to approve a push based only on technical reviewer/QA output.

Keep raw agent details internal unless the user asks for them.

For status/reporting requests, do not invoke agents.
The main session should inspect the repository directly and return a concise status report.
Agents are for implementation/review workflows, not simple project-state questions.