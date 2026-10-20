# Project conventions for AI agents

Invoice approval demo app: ASP.NET Core 8 Web API with a small static web UI and an in-memory store.
Read this file before you change anything. Every section is here because an agent arriving cold would otherwise get it wrong.

## Approval rules (the contract for approval limits by role)

This branch implements approval limits by role. The spec is the owner of these rules: `specs/001-role-approval-limits/spec.md` (FR-001 … FR-011). Working notes and test matrix: `.claude/skills/approval-rules/SKILL.md`.

| ID | Rule |
| --- | --- |
| AL-1 | A **Manager** may give the **final** approval up to **50,000.00** (inclusive). Above 500,000.00 a Manager may give the **first** of two approvals, never the second. |
| AL-2 | A **Director** may approve invoices up to **500,000.00** (inclusive) alone. |
| AL-3 | Above **500,000.00** needs **two distinct approvers**, and the **second must be a Director**. The first approval keeps the invoice `PendingApproval` and records who gave it. |
| AL-4 | The **same user never counts as both approvers** on one invoice (usernames compared trimmed and case-insensitive). |
| AL-5 | A **refused attempt returns a clear reason** (HTTP 403, `{ "error": "<reason>" }`) and **nothing about the invoice changes**. |
| AL-6 | A role with no limit (Admin, unknown, empty; roles match exactly) **cannot approve**, and gets a reason saying so. Fail closed. |

When several rules refuse one attempt, the reason names the first in this order: role cannot approve → same person → second not a Director → over the limit.

AL-4 and AL-5 are where generated code looks right and is not:

- The repositories hand out the stored `Invoice` object itself. Setting a field and then throwing **is** a change, even without `Update`. Run every check first; assign fields last.
- `firstApprover == approver` lets `Demo.Director` and `demo.director` both approve. Normalise before comparing.

## Layering

```text
wwwroot UI ──HTTP──> Api/Controllers ──> Domain: ApprovalService, ApprovalLimits ──> IInvoiceRepository
                     Api/Auth: session -> username + role                         Api: InMemoryInvoiceRepository
```

- `backend/src/InvoiceApp.Domain`: every business rule, the limits table and domain exceptions. No ASP.NET, no storage code.
- `backend/src/InvoiceApp.Api`: controllers, sign-in (`Auth/`), the in-memory repository and the web UI (`wwwroot/`). Controllers only translate HTTP to a service call.
- The approver's username and role come from the session (`HttpContext.GetSession()`), never from a request body.
- `ApiControllerBase.Handle(...)` maps service errors: `KeyNotFoundException` → 404, `InvalidOperationException` → 409, `ArgumentException` → 400. Approval denials get their own exception mapped to 403 with the reason.

## Layout

- `backend/tests/InvoiceApp.Tests`: xUnit tests, one test class per class under test, named `<ClassUnderTest>Tests`.
- `e2e`: Playwright end-to-end tests (`tests/`) and page objects (`pages/`).
- `specs/`: Spec Kit specifications, plans and task lists, one folder per feature.
- `docs/use-cases`: short briefs for features we may build. `docs/mutation`: recorded Stryker reports.
- `.claude/`: agents, skills, hooks and settings for AI agents (see "AI tooling" below). `ai-logs/`: the AI interaction log.

## Conventions

- Controllers live in `Controllers/`, one per resource, route `api/[controller]`. Request bodies are `record` types declared inside the controller.
- Controllers inherit `ApiControllerBase` and use `Handle(...)`.
- Every endpoint is behind sign-in. Restrict actions with `[RequireRole("...")]`: destructive actions are Admin only; Reject is Manager, Director or Admin. **Approve has no role filter on purpose**: the domain decides who may approve, so a refused user gets the reason instead of a bare 403.
- Business rules go in the Domain project, never in controllers. Numbers such as 50,000 and 500,000 appear once, in the limits table.
- Register services in `Program.cs`. Repositories are interfaces in Domain with an in-memory implementation in Api.
- Use clear names; no one-letter variables or `flag` parameters. Money is `decimal`; format it with the invariant culture in messages.
- End-to-end tests use page objects and role-based locators (`getByRole`, `getByLabel`), never CSS or XPath selectors.

## Commands

Requires the .NET 8 SDK and Node 20+.

| What | Command |
| --- | --- |
| Build | `dotnet build backend/InvoiceApp.sln` |
| Unit tests | `dotnet test backend/InvoiceApp.sln` |
| One test class | `dotnet test backend/InvoiceApp.sln --filter "FullyQualifiedName~ApprovalServiceTests"` |
| Run the app | `dotnet run --project backend/src/InvoiceApp.Api`, then open http://localhost:5000 |
| End-to-end tests | `cd e2e && npm install && npx playwright install chromium && npm test` |
| Mutation tests | `cd backend/tests/InvoiceApp.Tests && dotnet-stryker` (install once: `dotnet tool install -g dotnet-stryker --version 4.13.0`) |
| Format C# | `dotnet format whitespace backend --folder` |
| Enable git hooks (once per clone) | `git config core.hooksPath .githooks` |
| AI log report | `node scripts/ai-log/render-markdown.mjs` |

## Tests: what "done" means

- Every rule has a positive and a negative test at its boundary (50,000.00 allowed, 50,000.01 denied). Names follow `Method_Scenario_ExpectedResult`.
- A denial test asserts the **reason text** and that **nothing changed**: every invoice field, and the fake repository's update count.
- Review every generated test: name the bug it would catch. A test that still passes with the rule deleted is removed or fixed.
- Mutation score on `ApprovalService.cs` never drops below the recorded baseline (`docs/mutation/baseline`, 65.00%; it is 100% now), and Stryker fails the run below 70% for the Domain project (`break` in `stryker-config.json`). Surviving mutants in changed code are killed or explained.
- A user-visible rule gets one Playwright test through the UI.

## Workflow for a feature

1. **Specify** with Spec Kit: `/speckit-specify`, `/speckit-clarify`, `/speckit-plan`, `/speckit-tasks`. Commit the generated files.
2. **Implement in slices** (`/deliver-slice`): one slice per commit, each with its tests and a green build. Never the whole feature in one prompt.
3. **Prove** it: one Playwright test, then a Stryker run compared with the baseline.
4. **Review**: run the `code-reviewer` sub-agent (or `/pr-review`) on the branch diff, then judge every finding.

Commit messages (enforced by `.claude/hooks/guard-commit-message.mjs` and `.githooks/commit-msg`):

```text
<type>(<scope>): <what changed, imperative, max 72 chars>

<what changed, two to four lines>

Why: <the reason this change is needed>
```

## Review expectations

- Every PR is reviewed by the `code-reviewer` sub-agent with the `pr-review` skill before a human reviews it.
- The PR description lists **every** finding with a verdict: **Fixed** (name the commit), **Dismissed** (reason), or **Deferred** (reason and where it is tracked). No row is dropped.
- Blocker and Major findings are Fixed, or the human accepts the written reason.
- Reviewers check spec conformance first, then correctness, tests, security, layering and UX, and report with `path:line` and a concrete failing input.

## AI tooling in this repo

| Kind | Name | Purpose |
| --- | --- | --- |
| Lead agent | `feature-orchestrator` | Plans the feature, delegates each stage, checks gates. Run with `claude --agent feature-orchestrator`. |
| Sub-agent | `spec-writer` | Spec Kit spec, clarifications, plan and tasks. Never writes code. |
| Sub-agent | `domain-implementer` | Implements one slice of tasks in the backend. |
| Sub-agent | `test-author` | Writes and critically reviews unit tests. |
| Sub-agent | `e2e-author` | Playwright tests and page objects. |
| Sub-agent | `mutation-analyst` | Runs Stryker, explains survivors. Read-only. |
| Sub-agent | `code-reviewer` | Reviews the PR diff into the fixed findings table. Read-only. |
| Skill | `approval-rules`, `deliver-slice`, `pr-review`, `mutation-testing`, `e2e-playwright`, `ai-interaction-log` | Know-how loaded when the task matches (`.claude/skills/`). |
| Skill | `speckit-*` (Spec Kit 1.2.3) | `/speckit-specify`, `-clarify`, `-plan`, `-tasks`, `-analyze` … for Claude (`.claude/skills/`) and Copilot (`.github/skills/`). Constitution: `.specify/memory/constitution.md`. |
| Hook | `session-context` | SessionStart: briefs the agent on branch, active spec and gates. |
| Hook | `log-ai-interaction` | Logs every prompt, tool call, sub-agent result and reply to `ai-logs/ai-interactions.jsonl`. |
| Hook | `guard-commit-message` | Blocks `git commit` messages that miss the format or the `Why:` line. |
| Hook | `format-csharp` | Runs `dotnet format whitespace` on each C# file the agent edits. |
| Hook | `enforce-review-format` | Sends a review back if it is not in the findings table. |

GitHub Copilot reads this file, `.claude/skills/` and `.claude/agents/` too; `.github/copilot-instructions.md` points here.

## Demo accounts (not real credentials)

- `demo.manager` / `ChangeMe123!` (role Manager)
- `demo.director` / `ChangeMe123!` (role Director)
- `demo.director2` / `ChangeMe123!` (role Director; the second, different Director for invoices above 500,000)
- `demo.admin` / `ChangeMe123!` (role Admin; can reject and delete, cannot approve)

## Never

- Commit real credentials, tokens or customer data (the AI log masks them, but do not rely on it).
- Add business logic to `Program.cs` or controllers.
- Ask an agent for a whole feature in one prompt, or merge a generated test you have not read.
- Force-push, or rewrite history on a branch with an open PR.
