---
name: deliver-slice
description: Land one small, self-contained slice of a Spec Kit feature as its own commit, with tests, in this repo. Use when asked to implement the next task group from tasks.md, "do step 1/2", or ship part of a feature without doing all of it at once.
argument-hint: "<task IDs or phase from tasks.md, e.g. Phase 3 / T010-T016>"
---

# Deliver one slice

A slice is the smallest change that is useful and safe on its own: production code, the tests that prove it, and nothing from the next slice. The approval-limits feature ships as separate commits: single-approver limits first, then the two-approver rule.

1. **Scope.** Read `specs/<feature>/tasks.md` and pick only the tasks named in `$ARGUMENTS`. Read the matching rules in `spec.md` and the `approval-rules` skill. If the slice needs something from a later slice, stop and say so.
2. **Tests first.** Write the failing xUnit tests for the slice's rules, positive and negative, at the boundaries. Run `dotnet test backend/InvoiceApp.sln` and see them fail for the right reason.
3. **Code.** Make them pass with the smallest change that follows `AGENTS.md` (rules in Domain, controllers thin, `Handle(...)`, clear names).
4. **Review your own tests.** For each test, name the bug it catches. Delete or fix any test that would still pass if the rule were removed.
5. **Green.** `dotnet build backend/InvoiceApp.sln` and `dotnet test backend/InvoiceApp.sln` both pass. Tick the finished tasks in `tasks.md` (`- [x]`).
6. **Commit.** One commit for the slice:

   ```text
   feat(approvals): <what changed, imperative, max 72 chars>

   <two to four lines: what changed>

   Why: <the reason, in business terms>
   ```

   The PreToolUse hook blocks messages without the header or the `Why:` line.
