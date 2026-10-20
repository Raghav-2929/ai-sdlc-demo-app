<!--
Sync Impact Report
- Version change: template → 1.0.0 (first ratification)
- Principles added: I. Domain Owns the Rules; II. Fail Closed, Change Nothing on Denial; III. Tests Prove Rules (NON-NEGOTIABLE);
  IV. Small Slices, Each a Commit; V. Spec Before Code, Review Before Merge; VI. Every AI Interaction on Record
- Sections added: Technology Constraints; Quality Gates; Governance
- Templates checked: plan-template.md (Constitution Check reads these gates) ✅, spec-template.md ✅, tasks-template.md ✅
- Deferred TODOs: none
-->

# Invoice Approval Demo App Constitution

## Core Principles

### I. Domain Owns the Rules

Every business rule lives in `backend/src/InvoiceApp.Domain`. Controllers translate HTTP into one service call through
`ApiControllerBase.Handle(...)` and contain no decisions. A rule's numbers (for example an approval limit) are defined
in exactly one place. Rationale: rules that are spread across layers drift apart, and a reviewer cannot see them whole.

### II. Fail Closed, Change Nothing on Denial

Authorisation defaults to "no": a role, user or state the rules do not name is refused. Identity and role come from the
signed-in session, never from request data. Every check runs before any state is assigned, so a refused operation leaves
data exactly as it was and returns a reason a person can act on. Rationale: the in-memory repository hands out live
objects, so "validate, then mutate" is the only way a refusal can be truly side-effect free.

### III. Tests Prove Rules (NON-NEGOTIABLE)

Every rule has a positive and a negative test at its boundary. A refusal test asserts the reason and that nothing changed.
Generated tests are read and kept only if each catches a nameable bug. The mutation score of changed domain files never
drops below the recorded baseline, and a user-visible rule has one end-to-end test through the UI. Rationale: tests that
pass whether or not the rule exists give false confidence, which is worse than no tests.

### IV. Small Slices, Each a Commit

Work lands in thin, self-contained slices: production code plus the tests that prove it, a green build, and one commit
whose message says what changed and why (`<type>(<scope>): <summary>` … `Why: …`). A feature is never requested from an
agent in a single prompt. Rationale: small slices are reviewable, revertible, and keep an agent's mistakes local.

### V. Spec Before Code, Review Before Merge

A feature starts as a Spec Kit specification with stable requirement IDs, explicit clarifications and assumptions, a plan
and a task list, all committed. Before merge, an independent agent review runs on the diff and every finding gets a
recorded verdict: Fixed, Dismissed with reason, or Deferred with reason. Rationale: the spec is the yardstick the review
measures against, and verdicts make the human's judgement visible.

### VI. Every AI Interaction on Record

Prompts, tool calls, sub-agent results and replies are logged in one structured format (`ai-logs/ai-interactions.jsonl`,
schema `ai-log/v1`) with secrets masked. Rationale: a reviewer must be able to see what the agent was asked and what it did.

## Technology Constraints

- .NET 8 (C# 12), ASP.NET Core Web API, xUnit; Playwright for end-to-end tests; Stryker.NET 4.13.0 for mutation tests.
- In-memory storage only; no database, no new NuGet or npm runtime dependencies without a written reason in the plan.
- Money is `decimal`, formatted with the invariant culture in user-facing reasons.

## Quality Gates

1. `dotnet build backend/InvoiceApp.sln` and `dotnet test backend/InvoiceApp.sln` pass on every commit.
2. `npm test` in `e2e/` passes for user-visible rules.
3. Stryker score on changed domain files ≥ baseline; surviving mutants in changed lines are killed or explained.
4. The `code-reviewer` findings table is in the PR description with a verdict on every row.

## Governance

This constitution overrides other practice documents; `AGENTS.md` is the day-to-day guide that applies it. Changes need a
pull request that updates the version (semantic: MAJOR for removed or redefined principles, MINOR for added ones, PATCH
for wording), the date, and the Sync Impact Report above. Every plan's Constitution Check and every review verifies
compliance; a justified exception is written in the plan's Complexity Tracking table.

**Version**: 1.0.0 | **Ratified**: 2026-10-20 | **Last Amended**: 2026-10-20
