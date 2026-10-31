# Implementation Plan: Approval Limits by Role

**Branch**: `feature/role-approval-limits` | **Date**: 2026-10-20 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-role-approval-limits/spec.md`

## Summary

Add role-based approval limits to `ApprovalService`: Managers approve up to 50,000.00, Directors up to 500,000.00, and anything above needs two different people with a Director last. All limits live in one domain table (`ApprovalLimits`); every refusal is a domain exception (`ApprovalDeniedException`) raised **before** any field is assigned, mapped to HTTP 403 with the reason. The approver's username and role come from the session, and Approve has no controller role filter so that every refusal, including "this role cannot approve", carries its reason. Delivery: the spec set first, then three slices — single-approver limits (US1), the two-approver rule with its queue note (US2), and one Playwright test proving the UI shows the refusal reason (US3).

## Technical Context

**Language/Version**: C# 12 on .NET 8 (SDK 8.0.4xx)

**Primary Dependencies**: ASP.NET Core 8 Web API (existing); no new packages

**Storage**: In-memory repository (`InMemoryInvoiceRepository`), unchanged design

**Testing**: xUnit 2.9 (unit), Playwright 1.49+ (end-to-end), Stryker.NET 4.13.0 (mutation)

**Target Platform**: Local Windows/Linux dev machines; CI on ubuntu-latest

**Project Type**: Web service with a static web UI (`wwwroot`)

**Performance Goals**: Not a concern: a rule check is a few comparisons on one in-memory object

**Constraints**: No business logic in controllers; refusals must be side-effect free; identity from session only

**Scale/Scope**: Demo app; 4 demo users, a handful of invoices

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | How this plan complies | Status |
| --- | --- | --- |
| I. Domain Owns the Rules | Limits table, decision logic and the refusal exception are all in `InvoiceApp.Domain`. The controller passes the session identity in and has **no** role filter on Approve, so the only approval-authority decision is in the domain (R8). | PASS |
| II. Fail Closed, Change Nothing on Denial | Unknown roles have no limit and are refused **with a reason**; every check runs before the first assignment; identity from `HttpContext.GetSession()`. | PASS |
| III. Tests Prove Rules | Test matrix in [research.md](research.md#r7-proving-nothing-changes); `FakeInvoiceRepository` gains an update counter; tests tagged `[Trait("Rule", "AL-n")]` (R10); one Playwright test for the user-visible refusal (interpretation in R11); Stryker before/after with per-file scores for every changed domain file. | PASS |
| IV. Small Slices, Each a Commit | Spec set first, then three slices (US1, US2, US3), each green and committed separately; see [tasks.md](tasks.md). | PASS |
| V. Spec Before Code, Review Before Merge | The spec/plan/tasks set is committed **before** the first code commit (T003); `code-reviewer` runs before the PR is opened; clarifications go to the product owner in the PR. | PASS |
| VI. Every AI Interaction on Record | Hooks in `.claude/settings.json` log to `ai-logs/ai-interactions.jsonl`. | PASS |

Re-check after Phase 1 design and after `/speckit-analyze` (see [analysis.md](analysis.md)): PASS. No new projects, packages or patterns. Accepted risk recorded in R11 (no automated HTTP contract tests).

## Project Structure

### Documentation (this feature)

```text
specs/001-role-approval-limits/
├── spec.md              # /speckit-specify + /speckit-clarify
├── plan.md              # This file (/speckit-plan)
├── research.md          # Phase 0: decisions and alternatives
├── data-model.md        # Phase 1: entities, fields, state transitions
├── quickstart.md        # Phase 1: how to prove it works
├── contracts/
│   └── approvals-api.md # Phase 1: HTTP contract for approve / reject
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── InvoiceApp.Domain/
│   │   ├── Approver.cs                    # NEW  record (Username, Role)
│   │   ├── ApprovalLimits.cs              # NEW  role -> limit table, the only home of 50,000 / 500,000
│   │   ├── ApprovalDeniedException.cs     # NEW  refusal with a clear reason
│   │   ├── ApprovalService.cs             # CHANGED  Approve(invoiceId, Approver): limits, then two-approver rule
│   │   └── Invoice.cs                     # CHANGED (US2)  FirstApprovedBy, FirstApprovedAt
│   └── InvoiceApp.Api/
│       ├── Controllers/ApiControllerBase.cs   # CHANGED  ApprovalDeniedException -> 403 { error }
│       ├── Controllers/ApprovalsController.cs # CHANGED  pass session username + role; no role filter on Approve;
│       │                                      #          Reject: Manager, Director, Admin
│       ├── InMemoryInvoiceRepository.cs       # CHANGED  seed DecidedBy demo.approver -> demo.manager
│       ├── appsettings.json                   # CHANGED  demo.manager, demo.director, demo.director2, demo.admin
│       └── wwwroot/app.js, styles.css         # CHANGED (US2)  "First approval by … · needs a second approval …"
└── tests/InvoiceApp.Tests/
    ├── ApprovalServiceTests.cs            # CHANGED  rule tests AL-1 … AL-6, [Trait("Rule", "AL-n")]
    └── FakeInvoiceRepository.cs           # CHANGED  UpdateCount

e2e/
├── pages/LoginPage.ts, InvoicesPage.ts, ApprovalsPage.ts   # NEW page objects
└── tests/over-limit-approval.spec.ts                       # NEW one Playwright test

AGENTS.md, README.md                        # CHANGED  rules, role filters, demo accounts
docs/mutation/after/                        # NEW  Stryker report after the feature
docs/PR_DESCRIPTION.md, ASSIGNMENT.md       # NEW  review verdicts, write-up
```

**Structure Decision**: Keep the existing three-project backend (Domain, Api, Tests) and the `e2e` folder. New domain types are small files next to `ApprovalService`, matching the current one-type-per-file layout.

## Complexity Tracking

No constitution violations; nothing to justify.
