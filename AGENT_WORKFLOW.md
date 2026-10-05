# AI Project Copilot — Agent Workflow

This file defines the operational multi-agent workflow for Claude Code.

Source-of-truth priority:

SDD v1.2 > MASTER_PLAN.md > CLAUDE.md > AGENT_WORKFLOW.md > ad-hoc task wording

## Roles

Project-local agents:

1. gatekeeper
2. researcher
3. architect
4. developer
5. reviewer-logic
6. reviewer-security
7. reviewer-quality
8. qa
9. release-manager

The main Claude Code session acts as Team Lead.

The three reviewers should run independently and, when supported, in parallel.

---

# Core Principle

Use the smallest workflow that gives sufficient confidence.

Do NOT repeat completed workflow stages merely because:

- a new workflow file was added
- agent definitions changed
- a role was formalized later
- the current session was restarted
- a prior stage was not performed under the exact current agent name

Evidence from a previously completed equivalent stage remains valid unless:

- the implementation materially changed
- the previous evidence is stale or incomplete
- a blocker requires re-verification
- the user explicitly asks for a full re-audit

Never run a full workflow retroactively only for procedural completeness.

---

# Context Efficiency Rules

Agents must minimize context usage.

Before reading large files, first determine whether they are actually needed.

Prefer:

- git diff
- targeted files
- changed-file lists
- specific methods/classes
- previous agent summaries
- targeted test output

Avoid repeatedly reading:

- the full MASTER_PLAN.md
- the full CLAUDE.md
- the full AGENT_WORKFLOW.md
- unrelated source files
- unchanged migrations
- unrelated tests

If the active Phase, Slice, scope, and constraints are already clearly established by the Team Lead, downstream agents should use that summary instead of re-reading all planning documents.

For re-review, inspect only:

- the original finding
- the fix diff
- directly affected files
- directly relevant tests

Do not re-audit the whole Slice for a local fix.

---

# Workflow Classification

## SMALL

Use for low-risk, local, reversible changes with:

- no security impact
- no database schema change
- no public API contract change
- no authentication/authorization impact
- no external integration impact

Examples:

- documentation changes
- copy/text changes
- formatting
- minor UI layout changes
- isolated test cleanup

Workflow:

Developer
→ QA
→ Release Manager
→ explicit user approval
→ Push

Gatekeeper may be skipped when scope is obvious.

No Researcher or Architect unless specifically needed.

---

## NORMAL

Default workflow for ordinary feature slices.

Workflow:

Gatekeeper
→ Researcher
→ Architect only when there is a meaningful design decision
→ Developer
→ relevant Reviewer(s)
→ Fixes
→ QA
→ Release Manager
→ explicit user approval
→ Push
→ CI verification

Do not automatically run all three reviewers.

Choose only relevant reviewers:

- logic for correctness/state behavior
- security for security-sensitive code
- quality for architecture/maintainability concerns

---

## HIGH-RISK

Mandatory for:

- authentication
- authorization
- ASP.NET Core Identity
- JWT
- refresh tokens
- database schema/migrations with meaningful impact
- project ownership/security boundaries
- AI tools
- ToolContext
- human approval flow
- prompt-injection/security boundaries
- GitHub credentials/integration
- background execution
- production secrets
- cloud deployment
- destructive operations

Workflow:

Gatekeeper
→ Researcher
→ Architect
→ Developer
→ reviewer-logic
→ reviewer-security
→ reviewer-quality
→ Fixes
→ targeted re-review only if required
→ QA
→ Release Manager
→ explicit user approval
→ Push
→ CI verification

For HIGH-RISK work, the three reviewers should run independently and, when supported, in parallel.

Important:

A HIGH-RISK workflow does NOT mean every stage may repeat indefinitely.

Each major stage should normally run once.

---

# Team Lead Responsibilities

The main Claude Code session must:

- preserve the active Phase
- preserve the active Slice
- choose the correct risk level
- provide concise context to agents
- avoid duplicate agent work
- synthesize findings
- reject out-of-scope recommendations
- reject disproportionate recommendations
- ensure accepted findings are fixed before QA
- stop before the next Slice
- stop before git push until explicit user approval

The Team Lead must track which stages are already complete.

Do not run a stage again unless there is a concrete reason.

---

# Gatekeeper

Gatekeeper should determine:

- active Phase
- active Slice
- risk level
- in-scope work
- explicit deferred work
- completion evidence required
- important security/architecture constraints

Gatekeeper should NOT perform a deep code audit.

Gatekeeper should remain concise.

Target output:

- Phase
- Slice
- Risk
- In scope
- Deferred
- Required evidence
- Next agent

---

# Researcher

Researcher should inspect the actual codebase and produce an evidence map.

Researcher should focus only on files relevant to the active Slice.

Researcher should NOT:

- perform a second full architecture review
- run broad unrelated searches
- read the whole repository
- implement changes

Researcher output should be concise and reusable by Architect and Developer.

---

# Architect

Architect should use:

- Gatekeeper summary
- Researcher summary
- only the relevant source files

Architect should not repeat Researcher's entire investigation.

Architect should decide:

- architecture boundaries
- layer ownership
- persistence design
- migration impact
- API contract
- security boundaries
- failure behavior
- testing strategy
- proportionality

Architect must produce a short approved implementation plan.

After Architect approval, do not re-run Architect unless:

- the Developer materially deviates from the plan
- a reviewer finds an architecture-level BLOCKER
- database/security design materially changes

---

# Developer

Developer implements only the approved Slice.

Before editing, Developer should state:

- files to change
- intended behavior
- tests to add/change

Developer should avoid re-reading planning documents unless needed.

Developer should use the Researcher + Architect summaries as the primary context.

Developer does not push.

---

# Reviewer Strategy

Reviewers run once after implementation.

For HIGH-RISK slices:

- reviewer-logic
- reviewer-security
- reviewer-quality

should run in parallel when possible.

Each reviewer should receive:

- active Slice summary
- Architect-approved design summary
- changed-file list
- git diff
- relevant tests

Reviewers should not independently rediscover the entire project.

---

# Finding Severity

Use:

- BLOCKER
- HIGH
- MEDIUM
- LOW
- NOTE

Only BLOCKER and HIGH automatically require re-review after a fix.

MEDIUM/LOW fixes normally go directly to QA unless the Team Lead sees material risk.

---

# Re-Review Policy

This section is mandatory.

Do NOT run a full reviewer again after every fix.

Re-review is required only when:

- a BLOCKER was fixed
- a HIGH finding was fixed
- the fix materially changes architecture/security behavior
- the reviewer explicitly requests verification of a risky fix

For re-review, provide ONLY:

- original finding
- fix diff
- directly affected files
- directly relevant tests

The reviewer must verify the fix only.

Do not repeat the whole review.

Do not re-read the whole project.

Do not re-run unrelated checks.

Expected targeted re-review size should be much smaller than the original review.

---

# Fixes Stage

Developer fixes only accepted findings.

Do not expand scope while fixing review findings.

After fixes:

- BLOCKER/HIGH → targeted re-review
- MEDIUM/LOW → normally proceed to QA
- NOTE → no fix required unless Team Lead chooses

---

# QA

QA is the final technical verification gate.

QA should verify the final state once.

QA should reuse evidence from prior stages where appropriate, but must execute required final verification itself.

Typical backend final QA:

dotnet build .\ProjectCopilot.slnx

dotnet test .\ProjectCopilot.slnx --configuration Release

For DB changes:

- migration applies
- application starts
- relevant schema exists
- no unintended schema changes

For API changes:

- real endpoint success
- relevant negative case
- persistence behavior

For auth:

- register
- duplicate/invalid register
- login
- invalid login
- token returned
- token claims/expiry
- no secrets exposed

QA should not rerun architecture or reviewer analysis.

QA output:

PASS / FAIL / BLOCKED

---

# Commit Policy

Do not create multiple unnecessary commits during review iterations.

Preferred flow:

Implementation
→ Review
→ Fixes
→ QA
→ Final commit

If a commit already exists before review, accepted fixes may be committed as a focused follow-up commit rather than rewriting history, unless the user explicitly wants squashing.

Do not redo completed review stages solely because a commit exists.

---

# Release Manager

Release Manager performs:

- git status review
- final diff review
- secret check
- migration check
- documentation check
- QA confirmation
- release summary

Release Manager should not re-run Researcher, Architect, Reviewers, or QA.

Release Manager must always stop before git push.

It must ask:

מוכן ל-push. האם לבצע git push?

No push without explicit approval in the current conversation.

---

# Push Policy

After explicit approval:

git push

Then verify:

- remote commit
- CI status

If CI fails:

- identify failing area
- invoke only the agent needed for that failure
- do not restart the full workflow

Examples:

- compile/test failure → Developer/QA
- security regression → Security Reviewer
- architecture problem → Architect
- migration issue → Developer + QA

---

# CI Failure Policy

Never restart:

Gatekeeper
→ Researcher
→ Architect
→ all Reviewers

just because CI failed.

Start from the smallest stage relevant to the failure.

---

# Session Restart Policy

If Claude Code session restarts:

Do not automatically rerun completed agents.

First reconstruct state from:

- git status
- git log
- existing commits
- current diff
- previous agent summaries if available
- test state

Then continue from the first incomplete workflow stage.

---

# Workflow Migration Rule

If AGENT_WORKFLOW.md or agent definitions are changed while a Slice is already in progress:

Do NOT retroactively rerun already completed equivalent stages.

Apply the new workflow only from the next incomplete stage onward.

Example:

If a Slice already completed:

Research
→ Architecture
→ Development
→ Review
→ QA

and a new Gatekeeper definition is added afterward:

Do not restart the Slice.

Continue to Release Manager.

---

# Phase / Slice Boundary

Completion of one Slice does not authorize the next Slice.

After successful:

QA
→ Release
→ Push
→ CI

STOP.

Report completion.

Wait for explicit user approval before beginning the next Slice.

---

# SDD Rules

All workflows must enforce:

1. Every Feature has a real data source.
2. Read Tools may run automatically only when authorized.
3. Write Tools require Approval.
4. LLM never bypasses Authorization.
5. Tool arguments always pass validation.
6. No half-finished Feature.
7. No fake shown as real.
8. No Infrastructure without a consumer.
9. Every Phase must be Demoable.
10. Every Demo must be reproducible.
11. Authorization mismatch is rejected and audited.
12. No silent fallback during a security event.

---

# Current Phase

Current active Phase:

Phase 1 — Full-Stack Core

# Current Slice

Phase 1 is broken into Slices 1.1–1.14. See `MASTER_PLAN.md` §14 "Phase 1 Slice Breakdown" for the authoritative Scope/DoD of each Slice.

Current status:

- Slice 1.1 — Identity Persistence: DONE
- Slice 1.2 — Register/Login + JWT Issuance: DONE
- Slice 1.3 — JWT Bearer Enforcement: NOT STARTED ← current active Slice
- Slices 1.4–1.14: NOT STARTED

Slice 1.3 is HIGH-RISK.

Slices 1.1 and 1.2 already completed equivalent Research, Architecture, Development, Review, Fix, and QA work before the optimized workflow was introduced.

Do NOT restart those stages merely to satisfy the new workflow format.

Continue from the first genuinely incomplete gate for Slice 1.3 only.