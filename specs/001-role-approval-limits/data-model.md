# Data Model: Approval Limits by Role

## Invoice (existing, extended in US2)

| Field | Type | Rules |
| --- | --- | --- |
| `Id` | string | Existing. |
| `CustomerName` | string | Existing. |
| `Amount` | decimal | Existing; > 0 (checked at creation). Drives which rule applies. |
| `Status` | `InvoiceStatus` | Existing: Draft → PendingApproval → Approved / Rejected. |
| `DecidedBy` | string? | Existing. The user whose approval or rejection completed the decision. |
| `DecidedAt` | DateTime? | Existing. UTC. |
| `DecisionNote` | string? | Existing. Rejection reason. |
| `FirstApprovedBy` | string? | **New (US2).** Username of the first approver, only for amounts > 500,000.00. Null until then. Kept after the invoice is Approved or Rejected (history). |
| `FirstApprovedAt` | DateTime? | **New (US2).** UTC time of the first approval. Kept like `FirstApprovedBy`. |

## Approver (new value type)

`record Approver(string Username, string Role)` — built by the controller from the signed-in session. A null approver throws `ArgumentNullException` and a blank username `ArgumentException`: both are programming errors (unreachable over HTTP, because the session always has a username) and are checked before anything else.

## ApprovalLimits (new, static)

| Role (exact match) | Limit for a final approval (inclusive) | First of two approvals above 500,000.00 | Second (final) approval above 500,000.00 |
| --- | --- | --- | --- |
| `Manager` | 50,000.00 | Yes | No |
| `Director` | 500,000.00 | Yes | Yes |
| anything else (`Admin`, unknown, empty) | none | No | No |

Amounts strictly above `ApprovalLimits.DirectorLimit` (500,000.00) need two approvers; there is no separate threshold constant.

## ApprovalDeniedException (new)

Derives from `InvalidOperationException`. Message = the reason shown to the user. Thrown before any field changes.

## State transitions for `Approve(invoiceId, approver)`

```text
PendingApproval, amount ≤ 500,000.00
  ├─ approver's role has no limit ─────────────────────────> refused (unchanged)                    [AL-6]
  ├─ approver's limit < amount ────────────────────────────> refused (unchanged)                    [AL-1/AL-2]
  └─ approver's limit ≥ amount ────────────────────────────> Approved (DecidedBy = approver)

PendingApproval, amount > 500,000.00, FirstApprovedBy = null
  ├─ approver's role has no limit ─────────────────────────> refused (unchanged)                    [AL-6]
  └─ Manager or Director ──────────────────────────────────> PendingApproval (FirstApprovedBy = approver)

PendingApproval, amount > 500,000.00, FirstApprovedBy = X
  ├─ approver's role has no limit ─────────────────────────> refused (unchanged)                    [AL-6]
  ├─ approver is X (ignoring case and spaces) ─────────────> refused (unchanged)                    [AL-4]
  ├─ approver is not a Director ───────────────────────────> refused (unchanged)                    [AL-3]
  └─ different Director ───────────────────────────────────> Approved (DecidedBy = approver; FirstApprovedBy kept)

Any status other than PendingApproval ─────────────────────> refused as today (InvalidOperationException, 409)
```

**Order of checks** (FR-011): approver argument → invoice exists (404) → is pending (409) → role has a limit (AL-6) → then either the limit (single-approver path) or same person (AL-4) and Director second (AL-3) on the two-approver path. All checks complete before the first assignment.
