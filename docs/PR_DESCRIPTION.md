# Approval limits by role (Manager 50k, Director 500k, two approvers above)

## Summary

Implements `specs/001-role-approval-limits` (GitHub Spec Kit):

| Rule | Behaviour |
| --- | --- |
| AL-1 | A Manager gives the final approval up to 50,000.00 (inclusive). |
| AL-2 | A Director approves up to 500,000.00 alone. |
| AL-3 | Above 500,000.00: two different people; the first (Manager or Director) is recorded and the invoice stays pending; the second must be a Director. |
| AL-4 | The same person never counts twice (usernames compared trimmed and case-insensitive). |
| AL-5 | A refused attempt returns 403 `{ "error": "<reason>" }` and changes nothing on the invoice. |
| AL-6 | Roles without a limit (Admin, unknown) cannot approve, and get a reason. |

All rules live in `InvoiceApp.Domain` (`ApprovalLimits`, `ApprovalService`); every check runs before any field is assigned. Approve has no controller role filter on purpose, so even "your role cannot approve" comes back with a reason; Reject stays Manager, Director or Admin.

## Commits (each builds and passes its tests on its own)

| Commit | What | Tests |
| --- | --- | ---: |
| `98cbe3f` docs(agents) | AGENTS.md, skills, hooks, agents, AI-log tooling, baseline mutation report | 7 |
| `136cbdc` docs(spec) | Spec Kit set; `/speckit-analyze` found 24 issues, all fixed before commit | 7 |
| `c650ef0` feat(approvals) | **Step 1:** single-approver limits | 26 |
| `7881efa` feat(approvals) | **Step 2:** two distinct approvers above 500,000 | 41 |
| `f19a0cd` test(e2e) | One Playwright test: over-limit approval refused in the UI | 41 + e2e |
| `ae795b6` test(approvals) | Kill every mutant Stryker found surviving | 45 + e2e |
| `e85d814` fix(hooks) | Review R1, R4 | 45 + e2e |
| `9a43a47` fix(review) | Review R2, R3, R5, R6, R7 | 46 + e2e |
| `b6112ac` test(api) | Review note N1: HTTP contract tests | 53 + e2e |
| (last) docs(assignment) | Write-up, PR description, review record, AI log | 53 + e2e |

## How it was tested

- **Unit + API (xUnit): 53 tests** — `ApprovalServiceTests` 45, `ApprovalsControllerTests` 7 (app in memory), `InvoiceTests` 1. Rule tests are tagged `[Trait("Rule","AL-n")]`: AL-1 6, AL-2 3, AL-3 6, AL-4 7, AL-5 23, AL-6 12; the two negative rules (AL-4, AL-5) together: 24. Every refusal test checks the exact reason, a JSON snapshot of every invoice property, and that nothing was saved. Written test-first: step 1 went 8 red → green, step 2 went 11 red → green.
- **Test review:** a `test-author` sub-agent reviewed every step-1 test against ~40 hand-made mutants; its gaps were added before the step-1 commit.
- **End-to-end (Playwright):** `e2e/tests/over-limit-approval.spec.ts`. Fails when the Manager limit is changed to 100,000; after review R2 it also fails when the service approves and then throws (verified by planting that bug).
- **Mutation (Stryker.NET 4.13.0):** `ApprovalService.cs` 65.00% (baseline) → 85.42% (after both slices) → **100.00%** (after killing survivors; unchanged after the review fixes). Domain total 33.33% → 61.19% → 74.63%; the rest is `DiscountCalculator`, untested and out of scope. The run now fails below 70%. Details: `docs/mutation/README.md`.
- **Manual:** quickstart walk-through, curl contract checks, and the two-approver flow clicked through in the browser.

## For the product owner: decisions to confirm

The brief did not answer these; the developer chose and recorded the reason (spec "Clarifications"):

1. "Up to 50,000 / 500,000" is **inclusive**.
2. Above 500,000 the **first** approval may come from a **Manager** (the brief only constrains the second approver). Consequence: a Manager cannot give a final approval above 50,000 but can be first of two above 500,000.
3. The generic **Approver** role is retired; **Admin cannot approve** but **can still reject** (as before).
4. Limits do **not** apply to **rejecting**.
5. After a first approval the invoice stays **Pending Approval**; the first-approval record is kept after the final decision.
6. *(From the review, N4)* Should the person who **created** an invoice be barred from approving it (separation of duties)? Not in the brief; not implemented.

## Known limitations

- **Concurrent approvals (review N2):** `Approve` checks and assigns outside a lock. Two requests on the same large invoice at the same instant could both pass their checks; in the worst interleaving the record shows the same person as first approver and decider. The in-memory demo store has no concurrency control; a real store needs a version/ETag check on update.
- Amounts with more than two decimals are not rejected at creation; reasons print two decimals.
- `DiscountCalculator` has no tests (0% mutation score); not part of this feature.

## Agent review and verdicts

Reviewed by the `code-reviewer` sub-agent with the repository review prompt (`.claude/skills/pr-review/SKILL.md`) on `git diff main...HEAD` at `ae795b6`. Full output: [`docs/review/code-review.md`](review/code-review.md) (it passes the `enforce-review-format` hook). Every finding and every note gets a verdict.

> The workshop's own review prompt was not in the repository; `pr-review` is my reconstruction of it (spec conformance → correctness → tests → security → layering → UX, one fixed table).

| ID | Severity | Finding (short) | Verdict | Reason / evidence |
| --- | --- | --- | --- | --- |
| R1 | Major | AI-log hook serialised tool output before masking, so JSON-escaped secrets reached the log | **Fixed** in `e85d814` | Reproduced first (`"ChangeMe123!"` and an API key came through). `safeJson()` masks before serialising; the pattern also handles escaped quotes. Fixed before any log was committed. |
| R2 | Major | E2E "still pending" checks read the old page, blind to "approve then throw" | **Fixed** in `9a43a47` | Verified the gap by planting that exact bug: the old test **passed**. After the fix (reload, read from server) it **fails** on the planted bug and passes on the real code. |
| R3 | Minor | Queue not refreshed after a refusal, so the reason can contradict the screen | **Fixed** in `9a43a47` | `decide()` refreshes the queue after showing the reason. |
| R4 | Minor | Commit guard missed `git commit -am` and `git -C dir commit` | **Fixed** in `e85d814` | Reproduced both; guard now parses global options and combined flags. 11 pass/block cases re-run. |
| R5 | Minor | No test pins "role reason before same-person reason" | **Fixed** in `9a43a47` | New theory row `("demo.admin", "Admin")`. |
| R6 | Nit | 120,000 Manager refusal not fully asserted | **Fixed** in `9a43a47` | Uses `AssertDeniedAndUnchanged` + exact reason. |
| R7 | Nit | `FirstApprovedAt` retention not asserted | **Fixed** in `9a43a47` | Asserted equal after the final approval. |
| N1 | Note | Controller-to-domain wiring untested (hard-coded role or deleted 403 mapping pass every test) | **Fixed** in `b6112ac` | 7 `WebApplicationFactory` tests; both breaks now fail 3–4 tests. One package added (`Microsoft.AspNetCore.Mvc.Testing` 8.0.32), reason in research R11. |
| N2 | Note | Race between check and assign on the shared invoice | **Deferred** | Valid, but the fix belongs in the storage layer (version check on update) rather than a lock on a domain object, and the demo store is in-memory by design. Listed under Known limitations; follow-up when a real repository is introduced. |
| N3 | Note | Mutation gate was manual (`break: 0`, not in CI) | **Fixed** (threshold) in `9a43a47` · **Deferred** (CI job) | Stryker now fails below 70%. Adding a Stryker job to CI changes the shared pipeline and adds ~1 minute per run; to agree with the repo owners in a follow-up. |
| N4a | Note | Separation of duties: the creator of an invoice can approve it | **Deferred** | A new business rule outside the brief; raised as question 6 for the product owner above. |
| N4b | Note | Same-person reason should say "a different Director" | **Fixed** in `9a43a47` | Reason now reads "…a different Director must give the second approval." (only a Director can give it). Contract, quickstart and skill updated. |
| N5 | Note | Uncommitted exporter change and untracked `docs/context-experiment.md` in the working tree | **Dismissed** | Intentional work in progress at review time: the exporter change landed in `e85d814`, the experiment write-up in the final docs commit. Nothing was left out of the PR. |

Other reviews on this branch (also recorded): `/speckit-analyze` on the spec — 24 findings, all resolved before the spec commit ([analysis.md](../specs/001-role-approval-limits/analysis.md)); `test-author` review of the step-1 tests — gaps added before the step-1 commit.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
