# Research: Approval Limits by Role

Phase 0 of `/speckit-plan`. Each decision lists what was chosen, why, and what else was considered.

## R1. Where the limits live

- **Decision**: A static `ApprovalLimits` class in `InvoiceApp.Domain` with `ManagerRole`/`DirectorRole` names, `ManagerLimit = 50_000m`, `DirectorLimit = 500_000m`, and `LimitFor(role)` returning `decimal?` (`null` = no approval authority). Role names match exactly. "Above `DirectorLimit`" is also the two-approver threshold; there is no second constant.
- **Rationale**: The brief gives fixed numbers; one table in the domain satisfies FR-010 and Principle I, and `null` for unknown roles makes fail-closed (AL-6) the default.
- **Alternatives considered**: `appsettings.json` limits (adds a way to misconfigure money rules at deploy time with no test coverage; can be added later behind the same `LimitFor`); limits on the user record (mixes identity with policy).

## R2. How a refusal is signalled

- **Decision**: Throw `ApprovalDeniedException(reason)`, which derives from `InvalidOperationException`. `ApiControllerBase.Handle` catches it first and returns **403** with `{ "error": reason }`.
- **Rationale**: Matches the existing exception-to-status pattern in `Handle(...)`. Deriving from `InvalidOperationException` means that if a future endpoint forgets the 403 mapping, the client still gets a 409 with the reason, never a 500.
- **Alternatives considered**: A result object (`ApprovalResult`) — cleaner without exceptions but would change every caller and the `Handle` pattern for one feature; returning 409 — conflates "not allowed for you" with "wrong state"; 422 — the request is well-formed, the actor lacks authority.

## R3. HTTP status for a refusal

- **Decision**: 403 Forbidden with the reason in the body.
- **Rationale**: The user is signed in and the invoice is valid; this person is not permitted to make this decision. The UI already shows `error` from any non-OK response.
- **Alternatives considered**: 409 (state conflict) and 422 (validation) — both misdescribe the cause.

## R4. Recording the first approval above 500,000

- **Decision**: Two nullable fields on `Invoice`: `FirstApprovedBy` and `FirstApprovedAt`. Status stays `PendingApproval` until the second approval.
- **Rationale**: Exactly two approvals are ever needed; two fields are the simplest model that answers "who approved first". Keeping the status avoids changing the lifecycle, the status filter, and the UI colours.
- **Alternatives considered**: A new `AwaitingSecondApproval` status (touches every status check and the UI for little gain); a list of approval records (more general than the rule requires — YAGNI; recorded as possible future work for a full audit trail).

## R5. Comparing people

- **Decision**: `string.Equals(first.Trim(), second.Trim(), StringComparison.OrdinalIgnoreCase)`.
- **Rationale**: FR-004. Usernames are identifiers, not prose; ordinal comparison avoids culture surprises (e.g. Turkish i).
- **Alternatives considered**: Exact `==` (the trap the brief warns about).

## R6. Money in reasons

- **Decision**: `amount.ToString("N2", CultureInfo.InvariantCulture)` → `50,000.00`.
- **Rationale**: Reasons are asserted in tests and read by people; the server culture must not change them.

## R7. Proving "nothing changes"

- **Decision**: `FakeInvoiceRepository` keeps returning the stored object itself (like `InMemoryInvoiceRepository`) and gains `UpdateCount`. Every refusal test snapshots all invoice fields before the call and asserts they are equal afterwards **and** that `UpdateCount` is still 0. When US2 adds `FirstApprovedBy`/`FirstApprovedAt`, the snapshot gains both fields in the same commit (`/speckit-analyze` finding G1), otherwise "first approver written before the refusal" would slip through.
- **Rationale**: With shared references, "assign then throw" changes the stored invoice even without `Update`, so a field snapshot catches it. `UpdateCount` catches the opposite mistake (a refusal that still saves). The baseline mutation run showed `Update` calls could be deleted without any test failing; the counter fixes that too.
- **Alternatives considered**: Copy-on-read fake (would hide the real repository's shared-reference behaviour and let the bug through).

## R8. Roles, role filters and demo accounts

- **Decision**: Retire the generic `Approver` role. **Approve has no controller role filter**: every signed-in user reaches `ApprovalService`, which refuses roles without a limit with a reason (AL-6). **Reject** keeps an action-level filter for `Manager`, `Director` and `Admin` (Admin could reject before, FR-009). Demo users: `demo.manager` (Manager), `demo.director`, `demo.director2` (Director), `demo.admin` (Admin). The seeded approved invoice's `DecidedBy` changes from the retired `demo.approver` to `demo.manager`.
- **Rationale**: Clarification Q3/Q4 and `/speckit-analyze` finding C2: a class-level `[RequireRole("Manager","Director")]` answered Admin with a bare 403, so the AL-6 reason could never reach the user (breaks FR-005/FR-006 and Constitution II), and it duplicated an authorisation decision outside the domain (Constitution I). Sign-in is still enforced by `SessionAuthMiddleware`. Two Directors are needed to demonstrate AL-3 end to end.
- **Alternatives considered**: Keep the controller filter and teach the UI to explain bare 403s (the API would still give no reason); keep `Approver` as an alias for Manager (two names for one thing; confusing in reviews).

## R10. Tying tests to rules

- **Decision**: Tag every rule test with xUnit's built-in `[Trait("Rule", "AL-n")]` (several traits when a test proves several rules).
- **Rationale**: `/speckit-analyze` finding G4: per-rule coverage must be visible and runnable (`dotnet test --filter Rule=AL-4`). No new package.
- **Alternatives considered**: Rule IDs in comments only (not queryable); one test class per rule (splits shared helpers, against "one test class per class under test" in AGENTS.md).

## R11. One end-to-end test, and the HTTP contract

- **Decision**: Exactly one Playwright test, for AL-1 + AL-5 through the UI (the brief's requirement). AL-3, AL-4 and AL-6 reasons are verified at unit level and by the manual steps in quickstart.md; the 403 body is checked with curl.
- **Rationale**: `/speckit-analyze` findings C4 and G5. The constitution's "a user-visible rule has one end-to-end test" is read as "the feature's user-visible refusal has one", matching the brief's "one Playwright test". An API test project would add a package for one feature.
- **Accepted risk**: a regression in controller-to-domain wiring for AL-3/AL-4/AL-6 would be caught only by the quickstart, not by CI.
- **Update after the code review**: the `code-reviewer` showed the risk was real (hard-coding the role in the controller, or deleting the 403 mapping, passed every test). `ApprovalsControllerTests` now runs the API in memory with `Microsoft.AspNetCore.Mvc.Testing` 8.0.32 — the one new package, justified by closing that gap (Constitution: "no new dependencies without a written reason"). Both breaks now fail 3–4 tests. Still exactly one Playwright test.

## R9. End-to-end proof

- **Decision**: One Playwright test, `over-limit-approval.spec.ts`: sign in as `demo.manager`, create and submit a 75,000.00 invoice with a unique customer name, click Approve, expect the reason in the alert and the invoice still `Pending Approval`. Page objects and role-based locators only.
- **Rationale**: The brief asks for one UI test of a rejected over-limit approval; a unique customer name keeps the test independent of other data in the in-memory store.
