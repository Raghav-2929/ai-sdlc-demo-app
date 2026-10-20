---
name: pr-review
description: The review prompt for this repository. Reviews the pull request diff (current branch against main) for spec conformance, correctness, tests, security and conventions, and outputs findings in one fixed table that the PR description copies with a verdict per row. Use when asked to review a PR, a branch, a diff, or "my changes".
argument-hint: "[base branch, default main]"
allowed-tools: Read Grep Glob Bash(git diff *) Bash(git log *) Bash(git show *) Bash(git merge-base *)
---

# Pull request review

You are reviewing a pull request in the invoice approval app. Be the reviewer who catches the bug before production, not the one who comments on style. Every finding must be specific, evidenced and actionable.

## 1. Gather the change

```bash
git log --oneline ${ARGUMENTS:-main}..HEAD
git diff --stat ${ARGUMENTS:-main}...HEAD
git diff ${ARGUMENTS:-main}...HEAD
```

Then read, in this order: `AGENTS.md`, the active spec folder under `specs/` (`spec.md`, `plan.md`, `tasks.md`), and every changed file in full (not just the hunks), plus the tests that cover them.

## 2. Check, in this order

1. **Spec conformance.** For every rule in the spec (AL-1 … AL-6 for approval limits), find the code that enforces it and the test that proves it. Check boundaries exactly (`<` vs `<=`, 50,000.00 vs 50,000.01).
2. **Correctness.** State changed before validation; checks in the wrong order; null / empty / whitespace input; case and whitespace in identities; culture-dependent formatting; exceptions mapped to the wrong HTTP status; behaviour of existing endpoints that the change touches.
3. **Tests.** Would each test fail if the rule it names were broken? Are both negative rules tested (same user twice; denied attempt changes nothing, including the repository update count)? Are assertions specific (reason text, every field) rather than "does not throw"? Does the e2e test use page objects and role-based locators?
4. **Security and authorisation.** Identity and role come from the session, never the request body. Unknown roles fail closed. Controller role filters and domain checks agree.
5. **Layering and conventions** (AGENTS.md). Business rules only in Domain; controllers use `Handle(...)`; clear names, no one-letter variables or flag parameters; no secrets.
6. **UX.** The user sees the denial reason, and the screen shows the invoice still pending.

Do not report: formatting the formatter already handles, personal preference, or praise.

## 3. Output exactly this

```markdown
## Review findings

| ID | Severity | Category | Location | Finding | Suggested fix |
|----|----------|----------|----------|---------|---------------|
| R1 | Major | correctness | `backend/src/InvoiceApp.Domain/ApprovalService.cs:42` | What is wrong, with the concrete input that breaks it. | The smallest change that fixes it. |

## Notes for the author

- Up to five bullets for risks that are not tied to one line, or questions about the spec.
```

- **ID**: R1, R2, … in order of severity.
- **Severity**: `Blocker` (wrong money decision or security hole, must not merge), `Major` (a rule or test is wrong or missing), `Minor` (works, but fragile or unclear), `Nit` (small polish).
- **Category**: one of `spec`, `correctness`, `tests`, `security`, `layering`, `conventions`, `ux`, `maintainability`, `performance`.
- **Location**: `` `path:line` `` from the diff. Escape any `|` inside a cell as `\|`.
- If there is nothing to report, keep the table header and write `No findings.` under it.

A SubagentStop hook (`.claude/hooks/enforce-review-format.mjs`) sends the review back if this table is missing or malformed.

## 4. After the review (the author's job, not the reviewer's)

Copy every row into the PR description and add a **Verdict** column: `Fixed` (name the commit), `Dismissed` (give the reason), or `Deferred` (give the reason and where it is tracked). Never drop a row.
