---

description: "Task list for approval limits by role"
---

# Tasks: Approval Limits by Role

**Input**: Design documents from `/specs/001-role-approval-limits/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/approvals-api.md, quickstart.md

**Tests**: Requested by the brief (unit tests for every rule including the two negative rules, one Playwright test, mutation scores). Test tasks come first in each story and must fail before the code that makes them pass. Every rule test carries `[Trait("Rule", "AL-n")]`.

**Organization**: One phase per user story; each story ends in a green build and **one commit**. US1 and US2 are the two implementation steps the brief asks for (single-approver limits first, then the two-approver rule).

**Revision**: regenerated after `/speckit-analyze` (findings and fixes in [analysis.md](analysis.md)).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: User story from spec.md (US1, US2, US3)

## Phase 1: Setup

**Purpose**: Measure before changing anything, and commit the spec before code

- [x] T001 Record the baseline Stryker report in docs/mutation/baseline/ (ApprovalService.cs 65.00%, Domain total 33.33%)
- [x] T002 [P] Add backend/tests/InvoiceApp.Tests/stryker-config.json and ignore StrykerOutput/ in .gitignore
- [x] T003 Commit the Spec Kit set (.specify/, specs/001-role-approval-limits/, speckit skills, spec-writer agent) as its own `docs(spec)` commit before any code (Constitution V)

---

## Phase 2: Foundational (blocking prerequisites for US1 and US2)

**Purpose**: The types and test support both rule sets need. Lands in the US1 commit.

- [x] T004 [P] Add `record Approver(string Username, string Role)` in backend/src/InvoiceApp.Domain/Approver.cs
- [x] T005 [P] Add `ApprovalDeniedException : InvalidOperationException` in backend/src/InvoiceApp.Domain/ApprovalDeniedException.cs
- [x] T006 [P] Add `ApprovalLimits` (ManagerRole, DirectorRole, ManagerLimit 50_000m, DirectorLimit 500_000m, `LimitFor(role)` exact match, null for any other role; DirectorLimit is also the two-approver threshold) in backend/src/InvoiceApp.Domain/ApprovalLimits.cs
- [x] T007 Map `ApprovalDeniedException` to 403 `{ error }` before the `InvalidOperationException` catch in backend/src/InvoiceApp.Api/Controllers/ApiControllerBase.cs
- [x] T008 [P] Add `UpdateCount` to backend/tests/InvoiceApp.Tests/FakeInvoiceRepository.cs, keeping shared-reference semantics (research R7)

**Checkpoint**: Solution builds; existing 7 tests still pass.

---

## Phase 3: User Story 1 - Approvals stay within each role's limit (Priority: P1) 🎯 MVP · implementation step 1

**Goal**: Managers give final approval up to 50,000.00, Directors up to 500,000.00, other roles cannot approve; refusals give a reason and change nothing. Above 500,000.00 every approval is refused (interim, until US2).

**Independent Test**: `dotnet test backend/InvoiceApp.sln --filter "Rule=AL-1|Rule=AL-2|Rule=AL-5|Rule=AL-6"`

### Tests for User Story 1 (write first, see them fail)

- [x] T009 [US1] Update existing approve tests to pass an `Approver`; assert the not-pending reason text and that nothing changed in backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs
- [x] T010 [US1] AL-1: Manager approves exactly 50,000.00 → Approved, DecidedBy = manager, UpdateCount 1
- [x] T011 [US1] AL-1/AL-5: Manager at 50,000.01 → refused, exact reason, every field unchanged, UpdateCount 0
- [x] T012 [US1] AL-1/AL-2: 120,000.00 refused for Manager, approved by Director
- [x] T013 [US1] AL-1/AL-2: 500,000.00 refused for Manager; approved by a Director alone
- [x] T014 [US1] AL-6/AL-5: Admin, retired Approver, unknown and empty roles refused with "cannot approve invoices. A Manager or Director must approve it."; unchanged
- [x] T015 [US1] Interim: a Director at 500,000.01 is refused with "need two approvers" and nothing changes (replaced in US2 by T026)
- [x] T016 [US1] Null approver → ArgumentNullException; blank username → ArgumentException; both change nothing
- [x] T017 [US1] FR-009: a Manager can reject a 750,000.00 invoice (limits do not apply to rejecting)

### Implementation for User Story 1

- [x] T018 [US1] `ApprovalService.Approve(string invoiceId, Approver approver)`: argument → find → pending → role limit → amount; reasons per contracts/approvals-api.md; money "N2" invariant
- [x] T019 [US1] backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs: pass `new Approver(session.Username, session.Role)`; no role filter on Approve; `[RequireRole("Manager", "Director", "Admin")]` on Reject only
- [x] T020 [US1] Demo users demo.manager, demo.director, demo.director2, demo.admin in backend/src/InvoiceApp.Api/appsettings.json; seed `DecidedBy` demo.approver → demo.manager in InMemoryInvoiceRepository.cs; demo accounts in README.md and AGENTS.md

**Checkpoint**: All tests green → commit `feat(approvals): enforce single-approver limits by role`.

---

## Phase 4: User Story 2 - Two different people approve anything above 500,000 (Priority: P2) · implementation step 2

**Goal**: Above 500,000.00 the first approval is recorded, the second must be a different person and a Director; the queue shows the waiting state.

**Independent Test**: `dotnet test backend/InvoiceApp.sln --filter "Rule=AL-3|Rule=AL-4"`

### Tests for User Story 2 (write first, see them fail)

- [x] T021 [US2] Extend the `InvoiceState` snapshot with FirstApprovedBy and FirstApprovedAt (analysis G1), so every refusal test also proves no first approval was written — done by design: the test snapshot serialises every public property, so the new fields are covered automatically (suggested by the test-author review)
- [x] T022 [US2] AL-3: Director first on 750,000.00 → PendingApproval, FirstApprovedBy/At set, DecidedBy/At and DecisionNote still null, UpdateCount 1
- [x] T023 [US2] AL-3: different Director second → Approved, DecidedBy = second, FirstApprovedBy kept, UpdateCount 2
- [x] T024 [US2] AL-3: Manager first at 500,000.01, then Director → Approved
- [x] T025 [US2] AL-4/AL-5: same Director twice → exact reason, unchanged; same name with case/space differences on either side (theory) → refused, unchanged
- [x] T026 [US2] AL-2/AL-3: Director alone at 500,000.01 only records the first approval (replaces interim T015)
- [x] T027 [US2] AL-3/AL-5: Manager as second approver after a different Director → exact reason, unchanged
- [x] T028 [US2] AL-4 precedence (FR-011): same Manager twice → "different person" reason, not "must be a Director"
- [x] T029 [US2] AL-6/AL-5: Admin/unknown as first or second approver above 500,000.00 → role reason, unchanged
- [x] T030 [US2] FR-009: reject after a first approval → Rejected, first-approval record kept

### Implementation for User Story 2

- [x] T031 [US2] Add `FirstApprovedBy` and `FirstApprovedAt` to backend/src/InvoiceApp.Domain/Invoice.cs
- [x] T032 [US2] Two-approver path in `ApprovalService.Approve`: all checks before any assignment; `IsSamePerson` trimmed, OrdinalIgnoreCase; precedence role → same person → Director second
- [x] T033 [P] [US2] backend/src/InvoiceApp.Api/wwwroot/app.js + styles.css: in the pending list only, show "First approval by X · needs a second approval from a different Director"

**Checkpoint**: All tests green → commit `feat(approvals): require two distinct approvers above 500,000`.

---

## Phase 5: User Story 3 - The approver sees why an approval was refused (Priority: P3) · e2e proof

**Goal**: One Playwright test proves the over-limit refusal and its reason through the UI.

**Independent Test**: `cd e2e && npm test`

- [x] T034 [P] [US3] Page objects e2e/pages/LoginPage.ts, e2e/pages/InvoicesPage.ts, e2e/pages/ApprovalsPage.ts (role-based locators only)
- [x] T035 [US3] Playwright test e2e/tests/over-limit-approval.spec.ts: Manager approves a 75,000.00 invoice → alert shows the exact Manager-limit reason, the invoice is still Pending Approval

**Checkpoint**: `npm test` → 1 passed → commit `test(e2e): prove an over-limit approval is refused in the UI`.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [x] T036 Run Stryker after the feature; record docs/mutation/after/ with per-file scores for every changed domain file (ApprovalService.cs ≥ baseline 65.00%, new decision code ≥ 75%); kill or explain survivors
- [x] T037 Run the `code-reviewer` sub-agent on the branch diff; fix, dismiss or defer every finding with a reason
- [x] T041 [P] From the review: API tests for the approvals HTTP contract in backend/tests/InvoiceApp.Tests/ApprovalsControllerTests.cs (WebApplicationFactory)
- [ ] T038 [P] Write docs/PR_DESCRIPTION.md: summary, clarifications Q1–Q5 for the product owner, known limitations (concurrency, sub-cent amounts), review findings with verdicts
- [ ] T039 [P] Write ASSIGNMENT.md (write-up, mutation scores before/after as text, verdicts, video link)
- [ ] T040 [P] Export the AI interaction log and render ai-logs/AI_INTERACTIONS.md

---

## Dependencies & Execution Order

- Phase 1 (incl. the spec commit T003) → Phase 2 → US1 → US2 → US3 → Polish.
- US2 depends on US1 (it reuses `Approver`, `ApprovalLimits`, the refusal exception and the 403 mapping) and replaces US1's interim refusal above 500,000.00.
- US3 needs US1 (the reason text); it does not need US2.

## Parallel Opportunities

- T004, T005, T006, T008 touch different files and can be written in parallel.
- T033 (UI) can be written in parallel with T031–T032.
- T038, T039 and T040 are independent.

```text
# Phase 2 in parallel:
Task: "Add Approver record in backend/src/InvoiceApp.Domain/Approver.cs"
Task: "Add ApprovalDeniedException in backend/src/InvoiceApp.Domain/ApprovalDeniedException.cs"
Task: "Add ApprovalLimits in backend/src/InvoiceApp.Domain/ApprovalLimits.cs"
Task: "Add UpdateCount in backend/tests/InvoiceApp.Tests/FakeInvoiceRepository.cs"
```

## Implementation Strategy

1. Commit the spec set (T003).
2. Phase 2 + US1 → stop, run all tests, commit (MVP: limits already protect the business).
3. US2 → stop, run all tests, commit.
4. US3 → run Playwright, commit.
5. Polish: mutation after, agent review with verdicts, write-up, AI log.

Never hand an agent more than one phase per prompt.
