---
name: approval-rules
description: The invoice approval-limit rules (Manager 50,000, Director 500,000, two distinct approvers above 500,000 with a Director second, same user never both, denied attempts change nothing), their boundaries, error contract and test matrix. Use whenever you read, change, test or review approval code in ApprovalService, ApprovalLimits, ApprovalsController or the Approvals UI.
---

# Approval rules

The source of truth is `specs/001-role-approval-limits/spec.md`. This skill is the short version an agent needs while coding or reviewing.

## The rules

| ID | Rule | Boundary |
| --- | --- | --- |
| AL-1 | A **Manager** may approve an invoice whose amount is **up to 50,000.00**. | 50,000.00 is allowed; 50,000.01 is denied. |
| AL-2 | A **Director** may approve an invoice whose amount is **up to 500,000.00** on their own. | 500,000.00 is a single approval; 500,000.01 needs two. |
| AL-3 | An invoice **above 500,000.00** needs **two distinct approvers**. The **second** approver must be a **Director**. | The first approval leaves the invoice `PendingApproval` and records `FirstApprovedBy` / `FirstApprovedAt`. |
| AL-4 | The **same user** never counts as both approvers on one invoice. | Compare usernames trimmed and case-insensitive (`demo.director` = ` Demo.Director `). |
| AL-5 | A **denied** attempt returns a **clear reason** and **nothing about the invoice changes**. | Status, `DecidedBy`, `DecidedAt`, `DecisionNote`, `FirstApprovedBy`, `FirstApprovedAt` all stay as they were, and the repository is not updated. |
| AL-6 | Any role that is not in the limits table (Admin, unknown, empty) **cannot approve**. | Fail closed. |

Assumptions recorded in the spec: the first approver above 500,000 may be a Manager or a Director (the brief only constrains the second); rejecting an invoice is not limited by amount; Admin administers invoices but does not decide them.

## Where each rule lives

- Limits table: `ApprovalLimits` in `backend/src/InvoiceApp.Domain` (the only place the numbers 50,000 and 500,000 appear in production code).
- Decision logic: `ApprovalService.Approve(invoiceId, Approver approver)`. All checks run **before** any field is assigned.
- Denial: throw `ApprovalDeniedException(reason)`. `ApiControllerBase.Handle` maps it to **403** with `{ "error": reason }`.
- The approver's identity and role come from the signed-in session (`HttpContext.GetSession()`), never from the request body.

## Why the last two rules are traps

1. **Mutation before validation.** `InMemoryInvoiceRepository` and `FakeInvoiceRepository` hand out the stored object itself. If code sets `FirstApprovedBy` (or `Status`) and *then* throws, the invoice has changed even though `Update` was never called. Validate everything first, assign last.
2. **Same user by reference, not by value.** A plain `==` lets `Demo.Director` and `demo.director` both approve. Normalise before comparing.
3. **Order of checks above 500,000.** A Manager as second approver must be denied even when the first approver was a different person; a Director repeating their own first approval must be denied even though their role is right. Test both orders.

## Reason messages (assert on these words in tests)

- Over a limit: `... is above the Manager limit of 50,000.00 ...`
- No authority: `... role 'Admin' cannot approve invoices.`
- Same user: `... already gave the first approval ... a different person must give the second approval.`
- Second approver not a Director: `... the second approval must come from a Director ...`

## Test matrix (each row is at least one test in `ApprovalServiceTests`)

| Rule | Positive | Negative |
| --- | --- | --- |
| AL-1 | Manager approves 50,000.00 | Manager denied at 50,000.01; reason names the limit |
| AL-2 | Director approves 500,000.00 alone | Director cannot finish 500,000.01 alone |
| AL-3 | Manager or Director first, different Director second → Approved | Manager second → denied |
| AL-4 | — | Same Director twice denied; same name with different case/spaces denied |
| AL-5 | — | Every denial leaves all invoice fields and the update count unchanged |
| AL-6 | — | Admin / unknown role denied |

Every test must fail if its rule is removed. Run `dotnet-stryker` to prove it (see the `mutation-testing` skill).
