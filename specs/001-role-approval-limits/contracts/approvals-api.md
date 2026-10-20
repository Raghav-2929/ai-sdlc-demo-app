# Contract: Approvals API

All endpoints need `Authorization: Bearer <token>` from `POST /api/auth/login` (401 without it). The approver is the signed-in user; the body never names an approver or a role.

## POST /api/approvals/{invoiceId}/approve

**Who can call it**: any signed-in user. There is deliberately **no** role filter on this action: the domain decides who may approve (AL-6), so a user whose role cannot approve gets a 403 **with the reason** instead of a bare 403.

**Request body**: none.

| Outcome | Status | Body |
| --- | --- | --- |
| Approved (single approver within limit, or second approval by a different Director) | 200 | Invoice JSON, `status: "Approved"`, `decidedBy`, `decidedAt` set |
| First approval recorded (amount > 500,000.00) | 200 | Invoice JSON, `status: "PendingApproval"`, `firstApprovedBy`, `firstApprovedAt` set |
| Refused by an approval rule | 403 | `{ "error": "<reason>" }`; invoice unchanged |
| Invoice not pending | 409 | `{ "error": "Invoice INV-… is Approved; only pending invoices can be decided." }` |
| Invoice not found | 404 | `{ "error": "Invoice INV-… was not found." }` |

### Refusal reasons (stable wording; tests assert on them)

Shown in precedence order (FR-011): the first that applies wins.

| Rule | Reason |
| --- | --- |
| AL-6 role cannot approve | `demo.admin has role 'Admin', which cannot approve invoices. A Manager or Director must approve it.` |
| AL-4 same person | `demo.director already gave the first approval on invoice INV-1007; a different Director must give the second approval.` |
| AL-3 second not a Director | `Invoice INV-1007 is above 500,000.00, so the second approval must come from a Director; demo.manager is a Manager.` |
| AL-1 over the Manager limit | `Invoice INV-1006 is 75,000.00, which is above the Manager limit of 50,000.00. A Director must approve it.` |
| Interim, US1 only (above 500,000.00) | `Invoice INV-1007 is 750,000.00, which is above the Director limit of 500,000.00. Invoices above 500,000.00 need two approvers.` |

## POST /api/approvals/{invoiceId}/reject

**Allowed roles**: `Manager`, `Director`, `Admin` (others: 403 from the role filter). **Body**: `{ "reason": "…" }` (required). Behaviour unchanged: 200 with `status: "Rejected"`, 400 without a reason, 404, 409. A first-approval record, if any, is kept.

## Invoice JSON (additions)

```json
{
  "id": "INV-1007",
  "customerName": "Central Valley Schools",
  "amount": 750000.00,
  "status": "PendingApproval",
  "decidedBy": null,
  "decidedAt": null,
  "decisionNote": null,
  "firstApprovedBy": "demo.director",
  "firstApprovedAt": "2026-10-20T12:00:00Z"
}
```

## Accepted risk

The HTTP layer has no automated contract tests beyond the one Playwright journey: adding an API test project would add a package (Constitution: needs a written reason) and the brief asks for one end-to-end test. The 403 body, 404/409 mapping and session identity are checked with the `curl` steps in [quickstart.md](../quickstart.md).
