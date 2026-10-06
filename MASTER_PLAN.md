# MASTER_PLAN.md

# AI Project Copilot — Master Implementation Plan
**Source of truth:** SDD v1.2  
**Execution model:** Phase-by-phase, gated, reproducible, demoable  
**Primary implementer:** Claude Code  
**Repository:** `ai-project-copilot`

---

## 1. Purpose

This document is the execution plan for the AI Project Copilot SDD v1.2.

It must remain aligned with the SDD.  
When there is any conflict between this file and the SDD, the SDD wins.

Claude Code must:

- work phase by phase, in order;
- not start a future phase before the current phase satisfies its Definition of Done;
- preserve architecture and security rules;
- keep the application runnable after every meaningful change;
- keep CI green;
- avoid infrastructure that has no real consumer yet;
- stop at the end of each phase and report completion instead of automatically continuing.

---

# 2. Product Vision

AI Project Copilot is a production-oriented project management SaaS.

The product evolves from a real Full Stack application into an AI-assisted project management system with:

- Projects
- Tasks
- Documents
- Project Memory
- RAG with real citations
- AI Copilot
- Tool Calling
- Human approval for write actions
- Project Intelligence
- GitHub integration
- Background automation
- Production observability
- Cloud deployment

The system must never present fake behavior as a real feature.

---

# 3. Target Architecture

## Backend

- C#
- .NET 10
- ASP.NET Core
- Entity Framework Core
- PostgreSQL
- pgvector when Project Memory is implemented

## Frontend

- React
- TypeScript
- Vite
- React Router
- TanStack Query

## Architecture Style

Modular Monolith + Clean Architecture.

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

Dependency direction:

```text
Domain
↑
Application
↑
Infrastructure
↑
Api
```

More precisely:

- Domain depends on nothing application-specific.
- Application depends on Domain.
- Infrastructure depends on Application + Domain.
- API depends on Application + Infrastructure.

Business logic must not be placed directly in HTTP endpoints.

---

# 4. Planned Domain Model

The SDD roadmap includes the following domain/persistence concepts.

Core:

- User
- Project
- ProjectMember
- TaskItem
- TaskDependency
- Document
- DocumentChunk

Project Intelligence:

- ProjectDecision
- ProjectRisk

AI:

- AiConversation
- AiMessage
- AiToolExecution
- AiProposedAction

Audit:

- ActivityLog

Do not create all entities upfront.

Create an entity only when the active phase has a real consumer for it.

---

# 5. AI Trust Boundary

The LLM is untrusted.

The model must never be treated as:

- an authorization system;
- a source of trusted project identity;
- a direct database authority;
- a validator;
- a security boundary.

All AI tool execution must be controlled by deterministic application code.

Retrieved documents and external data are data, not instructions.

Prompt injection inside uploaded documents must not be able to override:

- system rules;
- authorization;
- project scope;
- tool restrictions;
- approval requirements.

---

# 6. Engineering Rules — SDD §72

These 12 rules are mandatory and must not be weakened.

1. **Every Feature must have a real data source.**
2. **Read Tools may execute automatically.**
3. **Write Tools require Approval.**
4. **The LLM must never bypass Authorization.**
5. **Tool arguments must always be validated.**
6. **No half-finished Feature.**
7. **No fake behavior presented as a real Feature.**
8. **Do not add Infrastructure without a consumer.**
9. **Every Phase must be Demoable.**
10. **Every Demo must be reproducible.**
11. **Authorization mismatch must be rejected and written to Audit/security logging.**
12. **No silent fallback during a security event.**

Claude Code must explicitly evaluate changes against these rules.

---

# 7. Cross-Cutting Security Requirements

## Authorization

Authorization is enforced by application/server code.

Future AI tools must receive trusted context such as:

```text
AuthenticatedUserId
AuthorizedProjectId
```

If a tool request contains a `projectId` that does not match the trusted authorized project context:

1. reject the request;
2. do not execute the operation;
3. write an audit/security event;
4. do not silently substitute another project;
5. do not silently continue.

## Write Operations

Read operations can be eligible for automatic execution.

Write operations must not modify the database directly from an AI proposal.

They must pass through the Human-in-the-Loop approval flow.

## Secrets

Never commit:

- production credentials;
- LLM API keys;
- GitHub tokens;
- cloud secrets;
- user secrets.

CI tests for AI functionality must not require a real external AI key.

---

# 8. Testing Strategy

Use the correct test layer for the behavior.

## Domain Tests

For deterministic domain/business logic.

## Application Tests

For commands, validators, use cases and application behavior.

## Integration Tests

For:

- API behavior;
- database integration;
- authorization boundaries;
- persistence behavior;
- meaningful end-to-end backend flows.

## AI Tests

Use `FakeAiModelClient`.

Tests must not call an external LLM provider during normal CI.

## External Integration Tests

GitHub integration tests should use a mock/fake GitHub client where possible.

Every important security rule needs regression coverage.

---

# 9. CI/CD Principles

GitHub Actions is the CI system.

The pipeline must remain green.

As the application grows, CI must validate the relevant parts of the project.

Expected progression:

- backend restore/build/test;
- database migrations;
- frontend install/build/test/lint;
- integration tests;
- later deployment checks.

Do not make CI dependent on paid external AI calls.

---

# 10. Seed Strategy — SDD §57

Seed is part of the product's reproducibility requirement.

A phase is not considered safely demoable merely because local developer data happens to exist.

## Seed Definition of Done

All seven requirements must be satisfied:

1. There is one documented command for running Demo Seed.
2. Seed works on an empty database.
3. Seed works on repeated execution.
4. Seed does not create duplicates.
5. Seed creates a complete Demo Project required for the current demo.
6. README contains Seed instructions.
7. The Demo can be recreated from zero in a reproducible way.

The seed must evolve as later phases require additional demo data.

For example:

- Phase 2 may require demo documents.
- Later phases may require deterministic project risks, conversations, GitHub fixture data, etc., when appropriate.

Do not silently seed production environments.

---

# 11. Stop Points — SDD §76

These are intentional portfolio/demo checkpoints.

## After Phase 1

**Full Stack C# application**

The product is already presentable as a real Full Stack application.

## After Phase 2

**Full Stack + RAG**

Project Memory works with real Sources.

## After Phase 3

**Full Stack + RAG + Agent**

The Copilot uses real Tools.

## After Phase 4

**Full Stack AI Agent + Human-in-the-Loop**

This is a strong portfolio checkpoint.

At each stop point, the application should be in a clean demoable state.

---

# 12. Execution Rules for Claude Code

Before every phase:

1. Read `CLAUDE.md`.
2. Read this `MASTER_PLAN.md`.
3. Inspect repository structure.
4. Inspect current implementation.
5. Check Git status.
6. Identify the active phase.
7. Compare existing code to the active phase requirements.
8. Break the phase into small vertical slices.

During implementation:

- make the smallest coherent change;
- preserve working code;
- build/test after meaningful milestones;
- do not prebuild later phases;
- do not add speculative infrastructure;
- do not create placeholder “real” features;
- do not weaken security to make a demo work.

At the end of each slice:

- report files changed;
- report commands executed;
- report test/build result;
- identify remaining work in the active phase.

At the end of each phase:

- check every required task;
- verify phase deliverable;
- verify Definition of Done;
- verify CI;
- verify reproducibility;
- report remaining debt or risks;
- STOP.

Do not automatically start the next phase.

---

# 13. PHASE 0 — Backend Foundation

**Estimate:** 4–6 days

## Deliverable

A real backend through Swagger supporting:

- Create Project
- Create Task
- List Tasks
- Update Task

Phase 0 is not Done until the Demo Seed command and reproducibility gate are satisfied.

## Scope

- .NET Solution
- Clean Architecture:
  - Api
  - Application
  - Domain
  - Infrastructure
- PostgreSQL
- Entity Framework Core
- Initial EF Core migrations
- Docker Compose for PostgreSQL
- Global Error Handling
- Structured Logging
- Swagger / OpenAPI
- Project entity + basic CRUD
- TaskItem entity + basic CRUD
- Validation
- Basic Unit Tests
- Basic Integration Tests
- GitHub Actions — backend restore/build/test
- official Demo Seed command
- idempotent Demo Seed
- Seed works on empty DB and repeated execution
- README run/Seed instructions

## Phase 0 Scope Guard

Do NOT implement in Phase 0:

- React
- AI
- Redis
- GitHub integration
- Document Processing

## Phase 0 Gate

In addition to the phase tasks, all Seed Definition of Done items in §10 must pass.

## Current Repository Status

Phase 0 has been implemented and CI has been reported green.

Known current foundation includes:

- .NET 10 solution
- Clean Architecture projects
- PostgreSQL 17
- EF Core
- migrations
- Docker Compose
- Swagger
- Projects API
- Tasks API
- validation
- global exception middleware
- tests
- GitHub Actions
- explicit idempotent demo seed

Current demo seed command:

```powershell
dotnet run --project .\src\ProjectCopilot.Api\ProjectCopilot.Api.csproj -- --seed-demo
```

Current demo Project ID:

```text
980f644c-039f-4031-80b0-c07ff3d14993
```

Important local PostgreSQL mapping:

```text
host 5433 -> container 5432
```

Do not change the local host port back to 5432.

Before treating Phase 0 as permanently closed, Claude Code should verify the repository itself against the complete SDD Phase 0 gate, especially documentation and reproducibility. Do not rewrite completed Phase 0 functionality merely to restyle it.

---

# 14. PHASE 1 — Full-Stack Core

**Estimate:** 5–7 days

## Deliverable

A Full Stack C# + React application working End-to-End with:

- Authentication
- Projects
- Tasks
- basic Dashboard
- Documents metadata

## Required Work

### Backend / Authentication

- ASP.NET Core Identity
- JWT Access Token
- Refresh Token
- Authorization by Project membership

### Frontend Foundation

- React
- TypeScript
- Vite
- React Router
- TanStack Query
- TypeScript strict
- ESLint / Prettier

Recommended frontend location:

```text
src/ProjectCopilot.Web
```

### UI

- Login Screen
- Projects Screen
- Create Project UI
- basic Project Dashboard
- Tasks UI
- Create/Edit Task UI
- Documents Metadata UI

### Documents

Implement only document metadata at this phase:

- Documents Metadata entity/API
- Documents Metadata UI

Do not implement ingestion/RAG yet.

### UX States

Required:

- Loading states
- Error states
- Empty states

### CI

Add frontend build/test validation to CI.

## Authorization Requirement

A user must not gain access to another project merely by changing a project ID in a URL or request.

Project membership must be enforced server-side.

## Phase 1 Non-Goals

Do not implement:

- RAG
- embeddings
- pgvector
- AI Copilot
- AI tools
- approval flow
- GitHub integration
- Redis
- background automation

## Phase 1 Definition of Done

Phase 1 is complete only when:

- Authentication works end-to-end.
- Access + refresh token flow is functional.
- Project membership authorization is enforced.
- React frontend works against the real API.
- Projects can be viewed/created.
- Project dashboard works.
- Tasks can be viewed/created/edited.
- Document metadata can be viewed through the real backend.
- loading/error/empty states exist.
- TypeScript strict passes.
- frontend CI checks pass.
- backend tests remain green.
- app is reproducibly runnable.
- the deliverable can be demonstrated without fake data paths.

## Phase 1 Slice Breakdown

This breakdown organizes the Phase 1 requirements above into small, independently closable Slices. It does not add, remove, or change any Phase 1 requirement — it only sequences and bounds the existing scope.

### Slice 1.1 — Identity Persistence
- Goal: Introduce ASP.NET Core Identity model and persistence.
- IN SCOPE: ApplicationUser, Identity tables, Identity migration.
- OUT OF SCOPE: endpoints, JWT, refresh tokens.
- Definition of Done: migration applies cleanly; Identity tables exist in the database; build/test green.
- Dependencies: Phase 0 gate verified.
- Risk: HIGH-RISK (Identity / database migration).
- Demo/Verification: `dotnet ef migrations list` shows the Identity migration applied.
- Status: DONE.

### Slice 1.2 — Register/Login API + JWT Issuance
- Goal: Real Register and Login endpoints that issue a valid JWT.
- IN SCOPE: Register endpoint, Login endpoint, validators, JWT token generator (issuance only).
- OUT OF SCOPE: JWT bearer enforcement/validation, refresh tokens.
- Definition of Done: register/login work end-to-end; issued token has correct claims/expiry; negative cases (duplicate/invalid input) covered by tests.
- Dependencies: Slice 1.1.
- Risk: HIGH-RISK (Authentication / JWT).
- Demo/Verification: POST /api/v1/auth/register and /login return a real token via Swagger/HTTP.
- Status: DONE.

### Slice 1.3 — JWT Bearer Enforcement
- Goal: Make issued JWTs actually enforced by the API.
- IN SCOPE: `AddAuthentication`/`AddJwtBearer`, `UseAuthentication`/`UseAuthorization`, `[Authorize]` on at least one endpoint.
- OUT OF SCOPE: Refresh tokens, Project membership authorization.
- Definition of Done: a request without a valid token to a protected endpoint returns 401; a request with a valid token returns 200.
- Dependencies: Slice 1.2.
- Risk: HIGH-RISK (Authentication).
- Demo/Verification: HTTP calls with and without a valid Authorization header against a protected endpoint.
- Status: DONE. Merged to main (commit b1f73df), CI green.

### Slice 1.4 — Refresh Token Flow
- Goal: Issue, store, rotate and revoke refresh tokens.
- IN SCOPE: refresh token entity/persistence, refresh endpoint, rotation on use.
- OUT OF SCOPE: UI.
- Definition of Done: a valid refresh token issues a new access token; a revoked/expired refresh token is rejected.
- Dependencies: Slice 1.3.
- Risk: HIGH-RISK (Refresh Tokens).
- Demo/Verification: full refresh flow exercised via HTTP, including a rejected-reuse case.
- Status: DONE. Merged to main (commit 5803a27), CI green.

### Slice 1.5 — Project Membership Authorization
- Goal: Enforce project membership server-side on Projects/Tasks endpoints.
- IN SCOPE: ProjectMember entity/migration, server-side membership checks on existing Projects/Tasks endpoints.
- OUT OF SCOPE: UI.
- Definition of Done: a user who is not a project member cannot read/write that project's data by changing the project ID in the URL, body, or query string; covered by a regression test.
- Dependencies: Slice 1.3.
- Risk: HIGH-RISK (Project membership / security boundary — mandatory per CLAUDE.md §13).
- Demo/Verification: cross-project access attempt is rejected (403/404) in an HTTP test.
- Status: DONE. Merged to main (commit 664ac8f), CI green.

### Slice 1.6 — Authorization Regression Tests
- Goal: Close out negative-path coverage for authentication/authorization.
- IN SCOPE: tests for missing token, invalid/expired token, and project-mismatch scenarios.
- OUT OF SCOPE: new product behavior.
- Definition of Done: all listed negative scenarios are covered and green.
- Dependencies: Slices 1.3–1.5.
- Risk: NORMAL.
- Demo/Verification: test run output showing each scenario covered.
- Status: DONE. Merged to main (commit bb49d7e), CI green.

### Slice 1.7 — Frontend Bootstrap
- Goal: Create the frontend project skeleton with no feature UI yet.
- IN SCOPE: Vite + React + TypeScript + React Router + TanStack Query scaffolding under `src/ProjectCopilot.Web`, ESLint/Prettier config, typed API client scaffold.
- OUT OF SCOPE: any feature screen.
- Definition of Done: `npm run build`, lint, and typecheck succeed with no feature code yet.
- Dependencies: none (can run in parallel with backend slices).
- Risk: SMALL.
- Demo/Verification: clean build/lint/typecheck output.
- Status: NOT STARTED.

### Slice 1.8 — Login UI + Session Handling
- Goal: Real login screen with token storage and protected routing.
- IN SCOPE: Login screen, session/token handling, protected routes.
- OUT OF SCOPE: Projects/Tasks UI.
- Definition of Done: login works against the real backend; unauthenticated users cannot reach protected routes.
- Dependencies: Slices 1.2–1.4, 1.7.
- Risk: NORMAL.
- Demo/Verification: manual/automated login flow against the real API.
- Status: NOT STARTED.

### Slice 1.9 — Projects UI (List + Create)
- Goal: Projects screen and Create Project UI.
- IN SCOPE: list/create against the real API; loading/error/empty states.
- OUT OF SCOPE: dashboard, tasks.
- Definition of Done: projects can be listed and created through the real UI; all three UX states present.
- Dependencies: Slices 1.5, 1.8.
- Risk: NORMAL.
- Demo/Verification: create a project in the UI and see it listed.
- Status: NOT STARTED.

### Slice 1.10 — Project Dashboard UI
- Goal: Basic Project Dashboard.
- IN SCOPE: dashboard screen showing real project data.
- OUT OF SCOPE: risk/intelligence widgets (Phase 5).
- Definition of Done: dashboard renders real data for a selected project.
- Dependencies: Slice 1.9.
- Risk: SMALL.
- Demo/Verification: open dashboard for a real seeded project.
- Status: NOT STARTED.

### Slice 1.11 — Tasks UI (List/Create/Edit)
- Goal: Tasks UI for a project.
- IN SCOPE: list/create/edit against the real API; loading/error/empty states.
- OUT OF SCOPE: documents.
- Definition of Done: all three task operations work end-to-end through the UI.
- Dependencies: Slice 1.9.
- Risk: NORMAL.
- Demo/Verification: create, list and edit a task in the UI.
- Status: NOT STARTED.

### Slice 1.12 — Documents Metadata Backend
- Goal: Documents Metadata entity/API (metadata only, no ingestion).
- IN SCOPE: entity, migration, CRUD API for metadata.
- OUT OF SCOPE: file ingestion/processing (Phase 2).
- Definition of Done: metadata CRUD works and is project-scoped; covered by tests.
- Dependencies: Slice 1.5.
- Risk: NORMAL.
- Demo/Verification: create/list document metadata via API for a real project.
- Status: NOT STARTED.

### Slice 1.13 — Documents Metadata UI
- Goal: UI for Documents Metadata.
- IN SCOPE: list/display of document metadata; loading/error/empty states.
- OUT OF SCOPE: upload/ingestion.
- Definition of Done: UI displays real metadata from the backend.
- Dependencies: Slice 1.12.
- Risk: SMALL.
- Demo/Verification: view document metadata for a real seeded project.
- Status: NOT STARTED.

### Slice 1.14 — Frontend Quality Gates + CI
- Goal: Enforce frontend quality and add it to CI.
- IN SCOPE: TypeScript strict mode verified, ESLint/Prettier configured and enforced, frontend build/test step added to GitHub Actions.
- OUT OF SCOPE: new product features.
- Definition of Done: CI includes a green frontend build/test/lint step.
- Dependencies: Slices 1.7–1.13.
- Risk: NORMAL.
- Demo/Verification: green CI run including the frontend job.
- Status: NOT STARTED.

Phase 1 is not closed, and Phase 2 must not start, until Slices 1.1–1.14 are all DONE and the Phase 1 Definition of Done above passes.

### Stop Point

After Phase 1 the application should already be presentable as a real Full Stack C# application.

---

# 15. PHASE 2 — Project Memory

**Estimate:** 5–7 days

## Deliverable

A user can ask a question about project documents and receive an answer backed by real project sources.

## Required Work

### Upload

- File Upload API
- file size validation
- file type validation
- secure generated storage filename

### Processing State

Document processing states:

- Uploaded
- Processing
- Ready
- Failed

### Processing

- text extraction
- text normalization
- configurable chunking

### Embeddings

- Embedding service
- pgvector setup
- DocumentChunk entity
- store embeddings

### Retrieval

- vector search
- strict project-scoped retrieval

A retrieval query for Project A must never retrieve Project B chunks.

### Ask Project Memory

- Ask Project Memory endpoint
- return citations/sources
- Project Memory UI

Answers must be grounded in retrieved sources.

No source means the system must not fabricate a project-specific answer.

### Demo Data

- demo documents in Seed

Required demo verification:

> Why did we choose PostgreSQL?

The system should answer from seeded project evidence and expose its source.

## Phase 2 Definition of Done

- upload validation works;
- processing state is visible;
- content is extracted and chunked;
- embeddings are stored;
- pgvector retrieval works;
- retrieval is project scoped;
- answer includes real citations;
- seeded demo is reproducible;
- no cross-project retrieval leakage;
- CI remains green.

## Phase 2 Slice Breakdown

This breakdown organizes the Phase 2 requirements above into small, independently closable Slices. It does not add, remove, or change any Phase 2 requirement.

### Slice 2.1 — Document Upload API
- Goal: Real file upload for a project's documents.
- IN SCOPE: upload entity/API, file size validation, file type validation, secure generated storage filename.
- OUT OF SCOPE: processing, extraction, embeddings.
- Definition of Done: valid uploads succeed; oversized/invalid-type uploads are rejected; covered by tests.
- Dependencies: Slice 1.12, Slice 1.5.
- Risk: NORMAL.
- Demo/Verification: upload a real file via API and see it rejected/accepted per validation rules.

### Slice 2.2 — Processing State Machine
- Goal: Track document processing lifecycle.
- IN SCOPE: Uploaded/Processing/Ready/Failed states and transitions.
- OUT OF SCOPE: the processing logic itself.
- Definition of Done: state is visible via API and transitions correctly on success/failure.
- Dependencies: Slice 2.1.
- Risk: NORMAL.
- Demo/Verification: observe state transition through the API for a real document.

### Slice 2.3 — Extraction, Normalization, Chunking
- Goal: Turn an uploaded document into normalized, chunked text.
- IN SCOPE: text extraction, normalization, configurable chunking.
- OUT OF SCOPE: embeddings.
- Definition of Done: a real uploaded document is deterministically chunked; covered by tests.
- Dependencies: Slice 2.2.
- Risk: NORMAL.
- Demo/Verification: process a real document and inspect the resulting chunks.

### Slice 2.4 — Embedding Service + pgvector + DocumentChunk Storage
- Goal: Store real embeddings for document chunks.
- IN SCOPE: embedding service, pgvector setup, DocumentChunk entity, embedding storage.
- OUT OF SCOPE: retrieval.
- Definition of Done: chunks have stored embeddings; migration applies; no real external AI key required in CI.
- Dependencies: Slice 2.3.
- Risk: HIGH-RISK (new infrastructure + migration + potential external provider secret).
- Demo/Verification: inspect stored embeddings for a real processed document.

### Slice 2.5 — Project-Scoped Vector Retrieval
- Goal: Retrieve relevant chunks for a query, strictly scoped to the authorized project.
- IN SCOPE: vector search, project-scoping enforcement.
- OUT OF SCOPE: the Ask endpoint/UI.
- Definition of Done: a retrieval query for Project A never returns Project B chunks; covered by a dedicated regression test.
- Dependencies: Slice 2.4, Slice 1.5.
- Risk: HIGH-RISK (cross-project data leakage is a security boundary).
- Demo/Verification: regression test proving no cross-project leakage.

### Slice 2.6 — Ask Project Memory Endpoint + Citations + UI
- Goal: Let a user ask a question and get a grounded answer with real citations.
- IN SCOPE: Ask endpoint, citations in the response, Project Memory UI.
- OUT OF SCOPE: AI agent/tool calling (Phase 3).
- Definition of Done: answers include real sources; when no source is found, the system does not fabricate a project-specific answer.
- Dependencies: Slice 2.5.
- Risk: NORMAL.
- Demo/Verification: ask a real question against seeded documents and see a cited answer.

### Slice 2.7 — Demo Documents in Seed
- Goal: Make Phase 2 reproducibly demoable.
- IN SCOPE: demo documents added to the Seed.
- OUT OF SCOPE: new product behavior.
- Definition of Done: "Why did we choose PostgreSQL?" is answered from seeded evidence with a real source, reproducibly from a clean database.
- Dependencies: Slice 2.6.
- Risk: SMALL.
- Demo/Verification: run the demo seed from empty and ask the required demo question.

Phase 3 must not start until Slices 2.1–2.7 are all DONE and the Phase 2 Definition of Done above passes.

### Stop Point

After Phase 2: Full Stack + RAG with real Sources.

---

# 16. PHASE 3 — AI Copilot

**Estimate:** 4–6 days

## Deliverable

An Agent uses real Tools to answer questions about project state.

## Required Work

### Model Abstraction

- `IAiModelClient`
- exactly one real provider implementation initially
- `FakeAiModelClient` for tests

Do not build multiple providers just for abstraction theater.

### Agent Runtime

- Agent Loop
- maximum 6 tool rounds
- Tool Registry

The max-round limit must be enforced deterministically.

### Tool Context

ToolContext includes trusted:

- AuthenticatedUserId
- AuthorizedProjectId

### Read Tools

Implement:

- `get_project_tasks`
- `get_blocked_tasks`
- `get_overdue_tasks`
- `search_project_memory`

Read Tools may execute automatically after authorization/validation.

### Persistence

- AiConversation persistence
- AiMessage persistence
- AiToolExecution persistence
- tool execution logs

### Security

If the model supplies a `projectId` that differs from AuthorizedProjectId:

- reject;
- execute nothing;
- create Audit/security log;
- no silent fallback.

### Tests

- Agent tests without external API calls
- use FakeAiModelClient

### UI

- Copilot UI
- Tool activity UI

## Phase 3 Definition of Done

- provider abstraction exists;
- one real provider works;
- fake provider covers CI tests;
- agent loop works;
- max 6 tool rounds enforced;
- tools read real project data;
- tool executions are persisted/logged;
- project mismatch is rejected/audited;
- Copilot UI displays responses/tool activity;
- no write tool mutates DB;
- CI remains green.

## Phase 3 Slice Breakdown

This breakdown organizes the Phase 3 requirements above into small, independently closable Slices. It does not add, remove, or change any Phase 3 requirement.

### Slice 3.1 — Model Abstraction + FakeAiModelClient
- Goal: Provider-agnostic model interface with a deterministic fake for tests.
- IN SCOPE: `IAiModelClient`, `FakeAiModelClient`.
- OUT OF SCOPE: a real provider, the agent loop.
- Definition of Done: interface exists; fake client is used in tests; CI makes no real external AI call.
- Dependencies: none beyond Phase 2 completion.
- Risk: NORMAL.
- Demo/Verification: unit tests exercising the fake client.

### Slice 3.2 — Real Provider Wiring
- Goal: Exactly one real provider implementation.
- IN SCOPE: one real `IAiModelClient` implementation, secret configuration.
- OUT OF SCOPE: a second provider ("abstraction theater" is explicitly disallowed).
- Definition of Done: the real provider works; no key is committed to the repository.
- Dependencies: Slice 3.1.
- Risk: HIGH-RISK (production secrets / external AI call).
- Demo/Verification: a real call succeeds in a non-CI environment with a configured key.

### Slice 3.3 — Agent Loop + Tool Registry
- Goal: Deterministic agent loop with a bounded number of tool rounds.
- IN SCOPE: agent loop, tool registry, maximum 6 tool rounds enforced deterministically.
- OUT OF SCOPE: ToolContext security enforcement (next slice), specific tools.
- Definition of Done: the round limit is enforced and covered by a test that would otherwise loop indefinitely.
- Dependencies: Slice 3.1.
- Risk: HIGH-RISK (AI agent execution control).
- Demo/Verification: test proving the loop stops at the round limit.

### Slice 3.4 — ToolContext + Project Mismatch Rejection/Audit
- Goal: Trusted tool context and rejection of any mismatched project scope.
- IN SCOPE: ToolContext carrying AuthenticatedUserId/AuthorizedProjectId; rejection + audit log when a tool-supplied projectId does not match.
- OUT OF SCOPE: the read tools themselves.
- Definition of Done: a mismatched projectId is rejected, nothing executes, and an audit/security event is written; no silent fallback.
- Dependencies: Slice 1.5, Slice 3.3.
- Risk: HIGH-RISK (AI trust boundary — mandatory per MASTER_PLAN §5/§7).
- Demo/Verification: test simulating a mismatched tool call and verifying rejection + audit entry.

### Slice 3.5 — Read Tools
- Goal: Real read tools backed by real project data.
- IN SCOPE: `get_project_tasks`, `get_blocked_tasks`, `get_overdue_tasks`, `search_project_memory`.
- OUT OF SCOPE: any write tool.
- Definition of Done: each tool returns real data, executes automatically only after authorization, and is covered by tests.
- Dependencies: Slice 2.5 (for `search_project_memory`), Slice 3.4.
- Risk: HIGH-RISK (AI Tools).
- Demo/Verification: Copilot answers a real question using a real tool call, visible in tool execution logs.

### Slice 3.6 — Conversation/Message/ToolExecution Persistence
- Goal: Persist Copilot interactions and tool activity.
- IN SCOPE: AiConversation, AiMessage, AiToolExecution persistence and logs.
- OUT OF SCOPE: UI.
- Definition of Done: a real conversation and its tool calls are persisted and retrievable.
- Dependencies: Slices 3.3–3.5.
- Risk: NORMAL.
- Demo/Verification: inspect persisted conversation/tool-execution records after a real Copilot interaction.

### Slice 3.7 — Copilot UI + Tool Activity UI
- Goal: User-facing Copilot experience.
- IN SCOPE: Copilot UI, tool activity UI.
- OUT OF SCOPE: write/approval flows (Phase 4).
- Definition of Done: UI displays real responses and real tool activity.
- Dependencies: Slices 3.5–3.6.
- Risk: NORMAL.
- Demo/Verification: ask the Copilot a real question in the UI and see tool activity rendered.

Phase 4 must not start until Slices 3.1–3.7 are all DONE and the Phase 3 Definition of Done above passes.

### Stop Point

After Phase 3: Full Stack + RAG + Agent with real Tools.

---

# 17. PHASE 4 — Human-in-the-Loop

**Estimate:** 3–5 days

## Deliverable

AI can propose a change, but the database changes only after explicit human approval.

## Required Work

### Proposed Action Model

- AiProposedAction entity

Statuses:

- Pending
- Approved
- Rejected
- Executed
- Failed

### Write Tools

Implement:

- `update_task_priority`
- `create_task`
- `mark_task_blocked`

Critical rule:

**Write Tools do not directly change the DB when initially called by the Agent.**

They create a proposed action.

### Approval API

- Approve endpoint
- Reject endpoint

### Revalidation at Approval Time

Approval must repeat:

- Authorization
- payload validation
- entity existence validation
- project ownership validation

Do not assume a proposal is still safe just because it was safe when created.

### Stale Actions

Implement stale action handling.

### Audit

- audit log for the action

### UI

- Approval UI
- Reject UI

### Test

- End-to-End test for an approved AI action

## Phase 4 Definition of Done

- AI proposals do not directly mutate DB;
- pending actions are persisted;
- explicit approve/reject works;
- authorization is rechecked at approval;
- payload is revalidated;
- entity/project ownership is rechecked;
- stale action behavior is safe;
- execution is audited;
- E2E approved-action test passes.

## Phase 4 Slice Breakdown

This breakdown organizes the Phase 4 requirements above into small, independently closable Slices. It does not add, remove, or change any Phase 4 requirement.

### Slice 4.1 — AiProposedAction Entity + Statuses
- Goal: Persistence for proposed AI actions.
- IN SCOPE: AiProposedAction entity/migration; Pending/Approved/Rejected/Executed/Failed statuses.
- OUT OF SCOPE: the write tools themselves.
- Definition of Done: migration applies; statuses are persisted and queryable.
- Dependencies: Slice 3.6.
- Risk: HIGH-RISK (schema underpinning the approval security boundary).
- Demo/Verification: create a proposed action record directly and verify status transitions.

### Slice 4.2 — Write Tools (Proposal-Only)
- Goal: Let the agent propose changes without ever mutating the database directly.
- IN SCOPE: `update_task_priority`, `create_task`, `mark_task_blocked`, each creating a proposed action only.
- OUT OF SCOPE: approval/execution.
- Definition of Done: calling any write tool never changes the database directly; it only creates a Pending proposal.
- Dependencies: Slice 4.1, Slice 3.4.
- Risk: HIGH-RISK (Human-in-the-Loop / AI Tools — mandatory).
- Demo/Verification: invoke a write tool and confirm the DB is unchanged until approval.

### Slice 4.3 — Approve/Reject API + Full Revalidation
- Goal: Human approval gate with full revalidation at approval time.
- IN SCOPE: Approve endpoint, Reject endpoint; re-check authorization, payload validation, entity existence, and project ownership at approval time (not just at proposal time).
- OUT OF SCOPE: stale-action handling, UI.
- Definition of Done: approving a proposal re-validates everything and only then executes; a proposal that is no longer valid is rejected even if it was valid when created.
- Dependencies: Slice 4.2.
- Risk: HIGH-RISK (mandatory revalidation rule — explicit in MASTER_PLAN §17).
- Demo/Verification: approve a valid proposal (executes) and approve a since-invalidated proposal (rejected).

### Slice 4.4 — Stale Action Handling
- Goal: Safe behavior for proposals that have gone stale.
- IN SCOPE: stale-action detection and handling.
- OUT OF SCOPE: UI.
- Definition of Done: a stale proposal cannot be silently approved/executed.
- Dependencies: Slice 4.3.
- Risk: HIGH-RISK (part of the approval security boundary).
- Demo/Verification: attempt to approve a deliberately staled proposal and confirm safe rejection.

### Slice 4.5 — Execution Audit Log
- Goal: Auditability of proposed-action execution.
- IN SCOPE: audit log entries for approve/reject/execute.
- OUT OF SCOPE: UI.
- Definition of Done: every approve/reject/execute is audited.
- Dependencies: Slice 4.3.
- Risk: NORMAL.
- Demo/Verification: inspect the audit log after a real approve/reject cycle.

### Slice 4.6 — Approve/Reject UI
- Goal: Human-facing approval UI.
- IN SCOPE: Approval UI, Reject UI.
- OUT OF SCOPE: new backend behavior.
- Definition of Done: a human can approve/reject a real proposal through the UI.
- Dependencies: Slice 4.3.
- Risk: NORMAL.
- Demo/Verification: approve/reject a real proposed action through the UI.

### Slice 4.7 — End-to-End Approved-Action Test
- Goal: Prove the full propose → approve → execute path works safely.
- IN SCOPE: one E2E test covering an approved AI action end-to-end.
- OUT OF SCOPE: new product behavior.
- Definition of Done: the E2E test passes.
- Dependencies: Slices 4.1–4.6.
- Risk: NORMAL (mandatory closing gate for the Phase).
- Demo/Verification: the E2E test run.

Phase 5 must not start until Slices 4.1–4.7 are all DONE and the Phase 4 Definition of Done above passes.

### Stop Point

After Phase 4: Full Stack AI Agent + Human-in-the-loop.

---

# 18. PHASE 5 — Project Intelligence

**Estimate:** 4–6 days

## Deliverable

Real Project Health:

- Healthy
- At Risk
- Critical

with understandable reasons and recommendations.

## Required Work

- Risk Engine
- Task dependency analysis
- Deadline analysis
- Blocked task analysis
- Overdue task analysis
- Project Health calculation
- Healthy / At Risk / Critical
- ProjectRisk entity/API
- AI recommendations
- Dashboard risk widgets
- explainable risk reasons
- tests for Risk logic

## Design Rule

The health state must come from real project data and deterministic risk logic.

Do not let the LLM invent the project health classification.

AI may help explain/recommend, but the underlying risk facts must come from real data.

## Phase 5 Definition of Done

- deterministic risk engine exists;
- dependencies/deadlines/blocked/overdue tasks are analyzed;
- health state is reproducible;
- risk reasons are explainable;
- ProjectRisk is persisted/exposed as required;
- dashboard reflects real data;
- AI recommendations are grounded in real risk data;
- risk tests pass.

## Phase 5 Slice Breakdown

This breakdown organizes the Phase 5 requirements above into small, independently closable Slices. It does not add, remove, or change any Phase 5 requirement.

### Slice 5.1 — Risk Engine Core
- Goal: Deterministic analysis of task dependencies, deadlines, blocked and overdue tasks.
- IN SCOPE: dependency analysis, deadline analysis, blocked-task analysis, overdue-task analysis.
- OUT OF SCOPE: health classification, AI recommendations.
- Definition of Done: each analysis is deterministic, reproducible, and covered by tests.
- Dependencies: Phase 1 Projects/Tasks data.
- Risk: NORMAL.
- Demo/Verification: run the risk engine against real seeded data and inspect results.

### Slice 5.2 — Project Health + ProjectRisk Entity/API
- Goal: Compute and expose Healthy/At Risk/Critical project health.
- IN SCOPE: ProjectRisk entity/migration/API, health calculation from Slice 5.1 outputs.
- OUT OF SCOPE: AI-generated classification (the LLM must not invent the health state).
- Definition of Done: health state is reproducible from real data and exposed via API.
- Dependencies: Slice 5.1.
- Risk: NORMAL.
- Demo/Verification: query a real project's health and confirm it matches the deterministic inputs.

### Slice 5.3 — Grounded AI Recommendations
- Goal: Let AI explain/recommend based on real risk facts, never invent them.
- IN SCOPE: recommendation generation grounded in Slice 5.2 output.
- OUT OF SCOPE: changing the risk/health computation itself.
- Definition of Done: recommendations reference real risk facts; the underlying health state is never produced by the model.
- Dependencies: Slice 5.2, Slice 3.2.
- Risk: NORMAL.
- Demo/Verification: inspect a real recommendation and confirm it traces to real risk data.

### Slice 5.4 — Dashboard Risk Widgets UI
- Goal: Surface project health/risk on the dashboard.
- IN SCOPE: risk widgets on the Project Dashboard.
- OUT OF SCOPE: new backend logic.
- Definition of Done: widgets display real health/risk data.
- Dependencies: Slice 5.2.
- Risk: SMALL.
- Demo/Verification: view the dashboard for a real at-risk project.

### Slice 5.5 — Risk Logic Tests
- Goal: Regression coverage for risk/health logic.
- IN SCOPE: tests for Slices 5.1–5.3.
- OUT OF SCOPE: new product behavior.
- Definition of Done: risk logic tests are green.
- Dependencies: Slices 5.1–5.3.
- Risk: SMALL.
- Demo/Verification: test run output.

Phase 6 must not start until Slices 5.1–5.5 are all DONE and the Phase 5 Definition of Done above passes.

---

# 19. PHASE 6 — GitHub Integration

**Estimate:** 5–8 days

## Deliverable

The application uses real GitHub data as part of Project Intelligence and Copilot.

## Required Work

- Repository connection flow
- GitHub client
- Issues sync
- Pull Requests sync
- Reviews sync
- Commit Activity sync
- persist relevant GitHub data locally
- GitHub sync status
- GitHub error handling
- Agent read tools for GitHub
- Open PR analysis
- Pending review analysis
- integration tests with mock/fake GitHub client

## Security

GitHub tokens are secrets.

Never expose or log them.

Never give the LLM unrestricted direct GitHub credentials.

## Phase 6 Definition of Done

- repository can be connected;
- relevant real GitHub data is synchronized;
- sync status/errors are visible;
- persisted data is scoped to the correct project;
- Copilot can read authorized GitHub-derived data through Tools;
- PR/review analysis works;
- integration tests do not depend on uncontrolled external state;
- CI remains green.

## Phase 6 Slice Breakdown

This breakdown organizes the Phase 6 requirements above into small, independently closable Slices. It does not add, remove, or change any Phase 6 requirement.

### Slice 6.1 — Repository Connection + GitHub Client + Secret Handling
- Goal: Connect a real GitHub repository safely.
- IN SCOPE: repository connection flow, GitHub client, secret storage/handling.
- OUT OF SCOPE: data sync.
- Definition of Done: a repository can be connected; the token is never logged or exposed.
- Dependencies: none beyond Phase 1 auth.
- Risk: HIGH-RISK (GitHub credentials — mandatory).
- Demo/Verification: connect a real repository and confirm the token is not present in logs/responses.

### Slice 6.2 — Issues/PRs/Reviews/Commit Activity Sync
- Goal: Pull real GitHub data into the product.
- IN SCOPE: Issues sync, Pull Requests sync, Reviews sync, Commit Activity sync, local persistence scoped to the correct project.
- OUT OF SCOPE: AI tools over this data.
- Definition of Done: real synced data is visible and correctly scoped to its project.
- Dependencies: Slice 6.1.
- Risk: HIGH-RISK (external integration + schema).
- Demo/Verification: sync a real repository and inspect persisted data.

### Slice 6.3 — Sync Status + Error Handling
- Goal: Visibility into sync health.
- IN SCOPE: sync status API/UI, GitHub error handling.
- OUT OF SCOPE: retries/scheduling (Phase 7).
- Definition of Done: sync status and errors are visible after a real sync attempt.
- Dependencies: Slice 6.2.
- Risk: NORMAL.
- Demo/Verification: trigger a sync and observe status/error reporting.

### Slice 6.4 — GitHub Read Tools
- Goal: Let the Copilot read authorized GitHub-derived data.
- IN SCOPE: Agent read tools for GitHub, open PR analysis, pending review analysis.
- OUT OF SCOPE: write tools over GitHub.
- Definition of Done: tools return real synced data, respecting ToolContext/project scoping.
- Dependencies: Slice 6.2, Slice 3.4.
- Risk: HIGH-RISK (AI Tools + external integration).
- Demo/Verification: ask the Copilot about open PRs for a real connected project.

### Slice 6.5 — Integration Tests with Mock/Fake GitHub Client
- Goal: Deterministic test coverage without depending on live GitHub state.
- IN SCOPE: integration tests using a mock/fake GitHub client.
- OUT OF SCOPE: new product behavior.
- Definition of Done: tests are green and do not depend on uncontrolled external state.
- Dependencies: Slices 6.1–6.4.
- Risk: NORMAL.
- Demo/Verification: test run output using the fake client.

Phase 7 must not start until Slices 6.1–6.5 are all DONE and the Phase 6 Definition of Done above passes.

---

# 20. PHASE 7 — Automation

**Estimate:** 3–5 days

## Deliverable

The product performs useful work automatically:

- Daily Brief
- automatic analysis
- scheduled sync

## Required Work

- Background job infrastructure
- Document background processing
- Embedding background processing
- Daily Brief generation
- Automatic project analysis
- Scheduled GitHub sync
- Job failure logging
- Retry policy
- Daily Brief UI
- Background job tests

## Infrastructure Rule

Background-job infrastructure is allowed here because real consumers now exist.

Do not introduce unrelated queue/cache infrastructure without a concrete consumer.

## Phase 7 Definition of Done

- background jobs have real consumers;
- failures are logged;
- retry behavior is explicit;
- document/embedding processing can execute in background;
- Daily Brief is generated from real project data;
- scheduled GitHub sync works;
- UI displays Daily Brief;
- tests cover important background workflows.

## Phase 7 Slice Breakdown

This breakdown organizes the Phase 7 requirements above into small, independently closable Slices. It does not add, remove, or change any Phase 7 requirement.

### Slice 7.1 — Background Job Infrastructure
- Goal: Introduce background execution, justified by real consumers in this same Phase.
- IN SCOPE: background job infrastructure with an immediate real consumer (not speculative).
- OUT OF SCOPE: unrelated queue/cache infrastructure.
- Definition of Done: a real job runs in the background end-to-end.
- Dependencies: Phase 2 processing pipeline existing.
- Risk: HIGH-RISK (background execution — mandatory).
- Demo/Verification: trigger a real background job and observe completion.

### Slice 7.2 — Document/Embedding Background Processing
- Goal: Move document/embedding processing to the background.
- IN SCOPE: background execution of Phase 2's extraction/chunking/embedding pipeline.
- OUT OF SCOPE: new processing logic.
- Definition of Done: a real upload is processed asynchronously and reaches Ready/Failed correctly.
- Dependencies: Slice 7.1, Slice 2.4.
- Risk: HIGH-RISK.
- Demo/Verification: upload a document and observe background processing to completion.

### Slice 7.3 — Job Failure Logging + Retry Policy
- Goal: Make background failures visible and recoverable.
- IN SCOPE: failure logging, explicit retry policy.
- OUT OF SCOPE: UI.
- Definition of Done: a deliberately failing job is logged and retried per policy.
- Dependencies: Slice 7.1.
- Risk: NORMAL.
- Demo/Verification: force a job failure and observe logging/retry behavior.

### Slice 7.4 — Daily Brief Generation
- Goal: Generate a real Daily Brief from real project data.
- IN SCOPE: automatic project analysis, Daily Brief generation.
- OUT OF SCOPE: GitHub sync scheduling (Slice 7.5).
- Definition of Done: a Daily Brief is generated from real data for a real project.
- Dependencies: Slice 5.2, Slice 3.2.
- Risk: NORMAL.
- Demo/Verification: generate and inspect a real Daily Brief.

### Slice 7.5 — Scheduled GitHub Sync
- Goal: Keep GitHub data fresh automatically.
- IN SCOPE: scheduled sync using Slice 6.2's sync logic.
- OUT OF SCOPE: new GitHub data types.
- Definition of Done: a scheduled sync runs and updates persisted GitHub data without manual trigger.
- Dependencies: Slice 7.1, Slice 6.2.
- Risk: HIGH-RISK (background execution + GitHub credentials).
- Demo/Verification: observe an automatic sync occurring on schedule.

### Slice 7.6 — Daily Brief UI
- Goal: Show the Daily Brief to the user.
- IN SCOPE: Daily Brief UI.
- OUT OF SCOPE: new backend logic.
- Definition of Done: UI displays a real generated Daily Brief.
- Dependencies: Slice 7.4.
- Risk: SMALL.
- Demo/Verification: view a real Daily Brief in the UI.

### Slice 7.7 — Background Job Tests
- Goal: Regression coverage for background workflows.
- IN SCOPE: tests for Slices 7.1–7.5.
- OUT OF SCOPE: new product behavior.
- Definition of Done: background workflow tests are green.
- Dependencies: Slices 7.1–7.5.
- Risk: NORMAL.
- Demo/Verification: test run output.

Phase 8 must not start until Slices 7.1–7.7 are all DONE and the Phase 7 Definition of Done above passes.

---

# 21. PHASE 8 — Production Polish

**Estimate:** 5–8 days

## Deliverable

A polished Portfolio/Production version that is deployable and ready for a real interview demo.

## Required Work

### Infrastructure — Only If Justified

- Redis only if a real consumer exists
- Embedding cache only if justified
- SignalR only if a real use case exists

### Protection / Performance

- Rate limiting
- Performance review
- Security review

### Real-Time

If justified:

- SignalR
- real-time agent progress

Do not add SignalR merely because it appears in the roadmap.

### Observability

- Advanced observability
- AI cost tracking
- AI latency metrics
- Tool latency metrics

### Deployment

- Cloud deployment
- Production secrets management
- Demo environment
- Seed verified in demo environment

### Product Polish

- UI polish
- Responsive polish

### Portfolio Material

- complete README
- architecture diagram
- documented demo flow
- demo video
- final CI/CD review

## Phase 8 Definition of Done

- production configuration is safe;
- secrets are externally managed;
- deployment is reproducible;
- demo environment works;
- demo Seed works there;
- rate limiting exists;
- security/performance review completed;
- observability captures meaningful signals;
- cost/latency can be inspected;
- UI is polished/responsive;
- README explains setup and architecture;
- architecture diagram exists;
- demo flow is documented;
- CI/CD has final review;
- any Redis/SignalR/cache introduced has a proven consumer.

## Phase 8 Slice Breakdown

This breakdown organizes the Phase 8 requirements above into small, independently closable Slices. It does not add, remove, or change any Phase 8 requirement.

### Slice 8.1 — Conditional Infrastructure Decision Gate
- Goal: Decide, with evidence, whether Redis/SignalR/caching are justified.
- IN SCOPE: review of real consumers from Phases 1–7; explicit go/no-go per item.
- OUT OF SCOPE: implementation itself (handled by 8.2/8.4 if approved).
- Definition of Done: a documented decision exists for each conditional item, backed by a named real consumer or an explicit "not justified yet."
- Dependencies: Phases 1–7.
- Risk: NORMAL (review only).
- Demo/Verification: the decision record itself.

### Slice 8.2 — Rate Limiting
- Goal: Protect the API from abuse.
- IN SCOPE: rate limiting on relevant endpoints.
- OUT OF SCOPE: unrelated infrastructure.
- Definition of Done: rate limiting is enforced and verified.
- Dependencies: none beyond Phase 1 auth.
- Risk: NORMAL.
- Demo/Verification: exceed the rate limit and observe the expected response.

### Slice 8.3 — Security + Performance Review
- Goal: Full-system review before production polish is considered complete.
- IN SCOPE: security review, performance review across the application.
- OUT OF SCOPE: fixing unrelated feature gaps outside findings.
- Definition of Done: review completed; findings triaged and material ones fixed.
- Dependencies: Phases 1–7 complete.
- Risk: HIGH-RISK (full security audit).
- Demo/Verification: the review report and resulting fixes.

### Slice 8.4 — SignalR Real-Time (only if approved in 8.1)
- Goal: Real-time updates, only if a real use case exists.
- IN SCOPE: SignalR wiring for the approved use case only.
- OUT OF SCOPE: SignalR for any use case not explicitly approved in Slice 8.1.
- Definition of Done: the approved real-time use case works end-to-end.
- Dependencies: Slice 8.1 (approval).
- Risk: HIGH-RISK (new real-time infrastructure).
- Demo/Verification: observe a real-time update for the approved use case.

### Slice 8.5 — Observability (AI cost/latency, tool latency)
- Goal: Make AI cost and latency inspectable.
- IN SCOPE: AI cost tracking, AI latency metrics, tool latency metrics, advanced observability.
- OUT OF SCOPE: unrelated metrics.
- Definition of Done: real cost/latency signals are captured and viewable.
- Dependencies: Phase 3 AI usage.
- Risk: NORMAL.
- Demo/Verification: inspect real captured metrics after real AI/tool usage.

### Slice 8.6 — Cloud Deployment + Production Secrets
- Goal: Deployable, reproducible production configuration.
- IN SCOPE: cloud deployment, production secrets management.
- OUT OF SCOPE: unrelated infra.
- Definition of Done: deployment is reproducible; secrets are externally managed, not committed.
- Dependencies: Slice 8.3.
- Risk: HIGH-RISK (production secrets / cloud deployment — mandatory).
- Demo/Verification: a reproducible deployment to the target environment.

### Slice 8.7 — Demo Environment + Seed Verification
- Goal: A working, reproducible demo environment.
- IN SCOPE: demo environment, Seed verified there.
- OUT OF SCOPE: unrelated infra.
- Definition of Done: the demo seed runs successfully in the demo environment.
- Dependencies: Slice 8.6.
- Risk: NORMAL.
- Demo/Verification: run the seed in the demo environment from empty.

### Slice 8.8 — UI/Responsive Polish
- Goal: Production-quality UI.
- IN SCOPE: UI polish, responsive polish.
- OUT OF SCOPE: new features.
- Definition of Done: UI is polished and responsive across target viewport sizes.
- Dependencies: all prior UI slices.
- Risk: SMALL.
- Demo/Verification: visual review across viewport sizes.

### Slice 8.9 — Portfolio Material
- Goal: Complete portfolio-ready documentation.
- IN SCOPE: complete README, architecture diagram, documented demo flow, demo video, final CI/CD review.
- OUT OF SCOPE: new product behavior.
- Definition of Done: all listed artifacts exist and are accurate.
- Dependencies: all prior Phase 8 slices.
- Risk: SMALL.
- Demo/Verification: review the README/diagram/demo flow against the actual running product.

This is the final Phase. After Slices 8.1–8.9 are all DONE and the Phase 8 Definition of Done above passes, the full SDD v1.2 roadmap is complete.

---

# 22. Roadmap Summary

| Phase | Name | Estimate | Core Deliverable |
|---|---|---:|---|
| 0 | Backend Foundation | 4–6 days | Real Swagger backend + reproducible Seed |
| 1 | Full-Stack Core | 5–7 days | Authenticated C# + React product |
| 2 | Project Memory | 5–7 days | RAG over project documents with Sources |
| 3 | AI Copilot | 4–6 days | Agent with real read Tools |
| 4 | Human-in-the-Loop | 3–5 days | Approved AI write actions |
| 5 | Project Intelligence | 4–6 days | Explainable Project Health |
| 6 | GitHub Integration | 5–8 days | Real GitHub data in Copilot/Intelligence |
| 7 | Automation | 3–5 days | Daily Brief + background workflows |
| 8 | Production Polish | 5–8 days | Deployable polished portfolio product |

**Total SDD estimate:** 38–58 days.

---

# 23. Current Active Phase

Current active phase:

```text
Phase 1 — Full-Stack Core
```

However, before significant Phase 1 work, inspect the existing repository and confirm the completed Phase 0 artifacts still satisfy the SDD gate.

Do not rebuild Phase 0 if the requirement is already correctly satisfied.

If a Phase 0 gate item is objectively missing, report it clearly and fix only the missing gap before continuing.

---

# 24. Phase 1 Recommended Order

Phase 1 should be executed as vertical slices, not as one giant implementation.

Recommended sequence:

1. Verify Phase 0 gate.
2. Introduce ASP.NET Core Identity model/persistence.
3. Implement authentication API.
4. Implement JWT access token.
5. Implement refresh token flow.
6. Implement Project membership authorization.
7. Add authorization regression tests.
8. Create React + TypeScript + Vite frontend.
9. Add React Router.
10. Add TanStack Query.
11. Configure typed API client.
12. Add Login UI.
13. Implement authenticated Projects flow.
14. Implement Create Project UI.
15. Implement Project Dashboard.
16. Implement Tasks UI.
17. Implement Create/Edit Task UI.
18. Implement Documents Metadata entity/API.
19. Implement Documents Metadata UI.
20. Add Loading/Error/Empty states.
21. Enable strict TypeScript.
22. Configure ESLint/Prettier.
23. Add frontend build/test to CI.
24. Run full phase verification.
25. Stop and report Phase 1 DoD.

Do not start Phase 2 until the user explicitly approves Phase 1 completion.

---

# 25. Completion Report Template

At the end of each phase Claude Code should respond in this structure:

```text
PHASE X COMPLETION REPORT

Deliverable:
[what is now demonstrably working]

Completed requirements:
- ...
- ...

Verification performed:
- command:
- result:

Tests:
- ...

CI:
- ...

Security checks:
- ...

Reproducibility:
- ...

Remaining technical debt:
- ...

Definition of Done:
PASS / NOT YET PASS

If NOT YET PASS:
- exact missing items

Next phase:
Do not start until user approval.
```

---

# 26. Final Instruction to Claude Code

Treat this file as an implementation roadmap, not permission to implement everything at once.

The active phase is the only implementation scope.

Future phases are context only.

Do not optimize for the number of features built.

Optimize for:

- correctness;
- real data;
- security;
- clean architecture;
- reproducibility;
- testability;
- demoability;
- honest production-oriented engineering.

When in doubt, choose the smallest implementation that fully satisfies the current SDD requirement.