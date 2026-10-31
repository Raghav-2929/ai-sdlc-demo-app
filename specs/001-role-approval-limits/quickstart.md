# Quickstart: prove approval limits work

## Prerequisites

.NET 8 SDK, Node 20+, and once per machine:

```bash
dotnet tool install -g dotnet-stryker --version 4.13.0
cd e2e && npm install && npx playwright install chromium
```

## 1. Unit tests (every rule, positive and negative)

```bash
dotnet test backend/InvoiceApp.sln
dotnet test backend/InvoiceApp.sln --filter "Rule=AL-4"      # only the tests tagged with one rule
```

Expected: all tests pass. Every rule test carries `[Trait("Rule", "AL-n")]`, so each rule's tests can be run and counted on their own.

## 2. Try it in the browser

```bash
dotnet run --project backend/src/InvoiceApp.Api
```

Open http://localhost:5000. Demo accounts (not real credentials) are in `AGENTS.md`.

| Step | Sign in as | Do | Expect |
| --- | --- | --- | --- |
| 1 | `demo.manager` | Create a 75,000 invoice, submit it, Approvals tab → Approve | Red message: above the Manager limit of 50,000.00, a Director must approve it; invoice still Pending Approval |
| 2 | `demo.director` | Approve the same invoice | Approved |
| 3 | `demo.admin` | Create and submit a 100 invoice, Approve | Red message: role 'Admin' cannot approve invoices |
| 4 | `demo.director` | Create and submit a 750,000 invoice, Approve | Still pending; queue shows "First approval by demo.director · needs a second approval from a different Director" |
| 5 | `demo.director` | Approve it again | Refused: a different Director must give the second approval |
| 6 | `demo.manager` | Approve it | Refused: the second approval must come from a Director |
| 7 | `demo.director2` | Approve it | Approved |

## 3. HTTP contract checks (curl)

With the app running (bash; on Windows use Git Bash):

```bash
login() { curl -s -X POST localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d "{\"username\":\"$1\",\"password\":\"ChangeMe123!\"}" | sed -E 's/.*"token":"([^"]+)".*/\1/'; }
ADMIN=$(login demo.admin)
curl -s -w ' [%{http_code}]\n' -X POST localhost:5000/api/approvals/INV-1001/approve -H "Authorization: Bearer $ADMIN"
```

Expected: `{"error":"demo.admin has role 'Admin', which cannot approve invoices. A Manager or Director must approve it."} [403]`, and `GET /api/invoices/INV-1001` still shows `PendingApproval`.

## 4. End-to-end test

```bash
cd e2e && npm test
```

Expected: `over-limit-approval.spec.ts` passes (1 passed).

## 5. Mutation test

```bash
cd backend/tests/InvoiceApp.Tests && dotnet-stryker
```

Expected: `ApprovalService.cs` at or above the baseline in `docs/mutation/baseline` (65.00%); per-file scores for every changed domain file recorded in `docs/mutation/after/`.
