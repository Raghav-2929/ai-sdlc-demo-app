# /speckit-analyze report and resolution

**Run**: 2026-10-20 by the `spec-writer` sub-agent, read-only, after `/speckit-tasks` and before any code commit. Inputs: spec.md, plan.md, tasks.md, research.md, data-model.md, contracts/, quickstart.md, the constitution and AGENTS.md (it also checked claims against the current code and the baseline Stryker report, which it confirmed: ApprovalService.cs 65.00%, Domain 33.33%).

**Result**: 24 findings (2 Critical, 4 High, 12 Medium, 6 Low); coverage 15/15 requirements had at least one task. All findings were resolved in the spec set before it was committed, as below.

| ID | Sev. | Finding (short) | Resolution |
| --- | --- | --- | --- |
| C1 | Critical | Spec set not committed before code, though Constitution V requires it | New task T003; the spec set is committed as its own `docs(spec)` commit before the first code commit |
| C2 | Critical | Class-level `[RequireRole("Manager","Director")]` answers Admin with a bare 403, so the AL-6 reason never reaches the user | Approve has **no** role filter; the domain refuses with a reason. Contract, research R8, plan, FR-006 updated |
| I1 | High | FR-001 "refuse a Manager above 50,000" contradicts a Manager giving the first of two approvals above 500,000 | FR-001, SC-001, AL-1 reworded around *final* approval; the consequence is stated in Clarifications Q2 and the glossary |
| I2 | High | Admin silently loses the right to reject | Admin keeps Reject (`[RequireRole("Manager","Director","Admin")]` on Reject); Q4, FR-009, contract updated |
| U1 | High | Behaviour above 500,000 before US2 undefined | Follow-up clarification + "Interim behaviour" in US1, contract row, test T015 replaced by T026 |
| G1 | High | Refusal snapshot cannot see FirstApprovedBy/At | T021 extends the snapshot in the US2 commit; T025/T029 assert reason + unchanged |
| U2 | Medium | Where/when the "awaiting second approval" note shows | Pending list only (T033); retention of first-approval fields stated in data-model |
| G2 | Medium | No tests for reject above a limit, reject after first approval, not-pending reason | T017, T030, T009 |
| G3 | Medium | Missing cases: Manager at 500,000.00 / 500,000.01, null fields after first approval, case/space on either side, Admin as second, same Manager twice | T013, T022–T025, T028, T029 |
| G4 | Medium | No single way to tie tests to rule IDs | `[Trait("Rule","AL-n")]` (research R10); quickstart filter |
| C3 | Medium | Mutation gate names only ApprovalService.cs | T036: per-file scores for every changed domain file; target ≥ 75% for new decision code |
| C4 | Medium | Constitution "one e2e per user-visible rule" vs brief "one Playwright test" | Interpretation recorded in research R11 |
| A1 | Medium | Sub-cent amounts print rounded in reasons | Edge case + assumption; listed as a known limitation in the PR |
| U3 | Medium | Blank-username guard has no requirement / order | data-model: programming-error guard, checked first; T016 |
| I3 | Medium | AL-6 reason does not say what is needed | Reason ends with "A Manager or Director must approve it." |
| U4 | Medium | Clarifications promised to the product owner but no task | T038 puts Q1–Q5 and the known limitations in the PR description |
| G5 | Medium | HTTP contract untested | Accepted risk (research R11); curl checks in quickstart §3 |
| I5 | Medium | refused / denied / rejected and "Approver" overloaded | Glossary in spec.md |
| I4 | Low | `TwoApproverThreshold` only in data-model | Removed; DirectorLimit is the threshold (R1, T006) |
| A2 | Low | Role matching unspecified | Clarification: roles match exactly |
| I6 | Low | Two different texts for the queue note | One text in spec US3-AS2, T033, quickstart |
| I7 | Low | Commit 3 typed `test(e2e)` but contains UI code | UI note moved to US2 (T033); commit 3 is tests only |
| I8 | Low | Retired `demo.approver` still in seed data; plan tree incomplete | T020 updates the seed; plan tree completed |
| D1 | Low | Rule text repeated in spec, AGENTS.md and skill | spec.md is the owner; AGENTS.md and the skill point to it and were re-synced |
