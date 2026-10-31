# Mutation testing record (Stryker.NET 4.13.0)

Run from `backend/tests/InvoiceApp.Tests` with `dotnet-stryker` (config: `stryker-config.json`, mutation level Standard, project `InvoiceApp.Domain`). Score = (killed + timeout) / (killed + timeout + survived + no coverage). Each folder holds the HTML report (open in a browser), the JSON report and `summary.txt` from `python scripts/mutation/summarize.py`.

| Run | Commit | Tests | `ApprovalService.cs` | `Invoice.cs` | Domain total |
| --- | --- | ---: | ---: | ---: | ---: |
| [baseline](baseline/) — original code and tests | `66cb3f9` (main) | 7 | **65.00%** (12 killed, 1 timeout, 7 survived) | 0.00% (2 survived) | **33.33%** |
| [after-feature](after-feature/) — both rule slices and their tests | `f19a0cd` | 41 | 85.42% (41 killed, 7 survived) | 0.00% (2 survived) | 61.19% |
| [after](after/) — after killing the survivors | `ae795b6` | 45 | **100.00%** (48 killed, 0 survived) | **100.00%** (2 killed) | **74.63%** |
| [final](final/) — after the code-review fixes and API tests | `b6112ac` | 53 | **100.00%** | **100.00%** | **74.63%** (gate: `break` 70) |

`DiscountCalculator.cs` stays at 0% (17 mutants with no coverage): it has no tests in the original repo and is not part of this feature. It is the only reason the Domain total is below 100%; recorded as deferred work.

The new files `ApprovalLimits.cs`, `Approver.cs` and `ApprovalDeniedException.cs` produce no mutants at this mutation level (constants, a record and a constructor); the limit values and the role switch are exercised through `ApprovalService` tests, which fail if either limit moves by one cent.

## What the survivors taught us

**Baseline.** Three survivors were the deletion of `_repository.Update(invoice)` in Submit, Approve and Reject: the original `FakeInvoiceRepository.Update` did nothing, so no test could tell whether a decision was saved. The fake now records which invoices were saved, and tests assert it. The other survivors were error messages no test read.

**After the feature, first pass (7 survivors in ApprovalService.cs).** None was in the new limit or two-approver logic except one message text. They were:

| Line | Mutant | Real bug it simulates | Killed by |
| --- | --- | --- | --- |
| 27 | Submit refusal message → `""` | Draft-only error loses its reason | `Submit_RejectsInvoiceThatIsNotADraftWithItsStatusAndChangesNothing` |
| 31 | Submit `_repository.Update` removed | Submitted invoice never saved | `Submit_MovesDraftToPendingApproval` (asserts `UpdatedIds`) |
| 40 | Approver-name message → `""` | Programming-error guard loses its message | `Approve_WithoutApproverName_…` (asserts the message) |
| 66 | Reject-reason message → `""` | "Reason required" error loses its text | `Reject_RequiresAReason` |
| 70 | Reject `EnsurePending` removed | **An approved invoice could be rejected afterwards** | `Reject_InvoiceThatIsNotPending_IsRefusedWithItsStatusAndChangesNothing` (new) |
| 76 | Reject `_repository.Update` removed | Rejection never saved | `Reject_RecordsReasonAndApprover` (asserts `UpdatedIds`) |
| 82 | Not-found message → `""` | 404 without a reason | `Approve_ThrowsWhenInvoiceDoesNotExist` |

`Invoice.cs` had two survivors (`Id` and `CustomerName` defaults changed to `"Stryker was here!"`). That matters: `InMemoryInvoiceRepository.Add` only numbers an invoice whose `Id` is empty. Killed by the new `InvoiceTests`.

The 16 "Ignored" mutants in `ApprovalService.cs` are Stryker's "block already covered" filter (mutants inside a block whose removal is already tested), and the one compile error is a mutant the C# compiler rejects; neither counts towards the score.
