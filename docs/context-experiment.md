# Does AGENTS.md matter? A cold-agent A/B test

**Question:** does the extended `AGENTS.md` change what an agent arriving cold would do?

**Method (2026-10-20):** two fresh Claude sub-agents (model: Sonnet, no prior conversation) were given the same ten questions about the approval-limits task. Each could read **one file only**:

- **A:** the original `AGENTS.md` from `main` (38 lines).
- **B:** the extended `AGENTS.md` from commit `98cbe3f` (131 lines).

They were told to answer as they would act if they started coding now, and to mark anything the file did not cover with "(guess)". Both runs are in the AI interaction log (`agent: Explore`).

## Results

| # | Question (short) | Correct answer | A: original | B: extended |
| --- | --- | --- | --- | --- |
| 1 | Manager approves 50,000.00 / 50,000.01? | Allowed / refused | ✅ correct, **guessed** | ✅ from file (AL-1) |
| 2 | What must happen for a 750,000.00 invoice? | Two different people, second a Director; first keeps it pending | ⚠️ "a sufficiently senior approver" — Director not named, **guessed** | ✅ from file (AL-3) |
| 3 | `demo.director` then `Demo.Director`? | Refused: same person | ✅ correct, **guessed** | ✅ from file (AL-4) |
| 4 | Status and body of a refusal; what may change? | 403 `{ "error": reason }`; nothing | ✅ correct, **guessed** | ✅ from file (AL-5) |
| 5 | Admin approves a 100.00 invoice? | Refused (fail closed) | ❌ **"treat Admin as unlimited, the approval succeeds"** | ✅ from file (AL-6) |
| 6 | Where does the limit check go; where do name and role come from? | Domain `ApprovalService` / `ApprovalLimits`; the session | ⚠️ Domain right; "user's claims" — the app has no claims, it uses a session store | ✅ from file |
| 7 | Unit-test command; mutation install and run? | `dotnet test backend/InvoiceApp.sln`; `dotnet tool install -g dotnet-stryker --version 4.13.0`, run in the test project | ⚠️ unit right; Stryker **without the version**, which fails to install on .NET 8 | ✅ from file |
| 8 | What must a commit message contain? | `<type>(<scope>): <summary>`, body, `Why:` line | ❌ "short imperative summary, maybe a ticket" | ✅ from file (and knows the hooks enforce it) |
| 9 | Six review findings: what before merge? | Every finding in the PR description with Fixed / Dismissed / Deferred and a reason | ⚠️ "triage, fix valid ones", no verdict list in the PR | ✅ from file |
| 10 | Most likely bug for "nothing changes", and the test? | Assign then throw on a shared object; snapshot all fields + update count | ✅ correct, **guessed** | ✅ from file, plus the case-insensitive trap |

**Score:** A: 4 correct (all guessed), 4 partly right, 2 wrong; 9 of 10 answers were guesses. B: 10 correct, all from the file.

## What this shows

- A capable model guesses general engineering well (403, case-insensitive names, validate-then-mutate).
- It guesses **project decisions** wrong, and those are the expensive ones: an Admin who can approve any amount (a spending-control hole), a mutation tool that will not install, commit messages without a reason, and review findings that are never recorded with a verdict.
- Every wrong or partial answer in A maps to a section added to `AGENTS.md`: the rules table (AL-1 … AL-6), the layering and session identity, the commands table with the pinned Stryker version, the commit format, and the review expectations.
