# AI in SDLC — Assignment Write-up

**Author:** Raghav Agarwal (3216826) · **Feature:** approval limits by role · **Branch:** `feature/role-approval-limits`

| | Link |
| --- | --- |
| Demo video (8–10 min) | VIDEO_LINK_HERE |
| Pull request (on my fork) | PR_LINK_HERE |
| Specification | [`specs/001-role-approval-limits/`](specs/001-role-approval-limits/spec.md) |
| PR description with review verdicts | [`docs/PR_DESCRIPTION.md`](docs/PR_DESCRIPTION.md) |
| AI interaction log | [`ai-logs/ai-interactions.jsonl`](ai-logs/ai-interactions.jsonl) · readable: [`ai-logs/AI_INTERACTIONS.md`](ai-logs/AI_INTERACTIONS.md) |

## 1. What I did, in one paragraph

I took one feature — approval limits by role in the invoice approval service — from a written brief to a reviewed pull request, with an AI agent (Claude Code, Opus) doing the work and me holding the checks. First I gave a cold agent the context it needs (`AGENTS.md`, skills, hooks, sub-agents) and **proved it changes what the agent does**. Then I specified the feature with **GitHub Spec Kit**, where the analyze step caught two critical design errors before any code. I implemented it in **two steps**, each test-first and each its own commit, added **one Playwright test** that I proved fails when the rule is broken, and used **Stryker** to show the tests catch real mistakes (`ApprovalService.cs` 65.00% → **100.00%**). Finally an independent **code-reviewer sub-agent** reviewed the diff: it found two real bugs I had missed (secrets could leak into the AI log; the e2e test was blind to the main trap). I reproduced each finding, fixed what was right, deferred what belongs elsewhere with a reason, and recorded a verdict for every one.

## 2. Tooling and compliance

- **Agent:** Claude Code (Anthropic), used through my Nagarro-provided account. The setup also works with **GitHub Copilot** (the brief's suggestion): Copilot reads `AGENTS.md`, `.github/copilot-instructions.md`, `.claude/skills/`, `.claude/agents/`, and Spec Kit's Copilot skills in `.github/skills/`.
- **Spec-driven tool:** GitHub Spec Kit 1.2.3 (`specify` CLI), integrations for Claude and Copilot.
- **Tests:** xUnit (.NET 8 SDK 8.0.426) with `Microsoft.AspNetCore.Mvc.Testing` 8.0.32 for the API tests, Playwright 1.63 (Chromium), Stryker.NET **4.13.0** (`dotnet tool install -g dotnet-stryker --version 4.13.0`, as the brief requires).
- No real credentials or customer data anywhere; demo accounts only. The AI log masks secrets, tokens and email addresses.

## 3. The nine tasks

### 3.1 Context that earns its place — and proof that it matters

`AGENTS.md` now tells an agent arriving cold: the six approval rules (AL-1 … AL-6) and their precedence, **why AL-4 and AL-5 are traps** (the repository hands out live objects, so "assign then throw" is a real change; `==` lets `Demo.Director` approve twice), the **layering** (Domain owns rules, controllers are thin, identity comes from the session), every **command** (incl. the pinned Stryker version), what "done" means for **tests**, the **workflow**, the **commit format**, the **review expectations**, and a map of all AI tooling.

**Proof (A/B test, [`docs/context-experiment.md`](docs/context-experiment.md)):** two fresh sub-agents got the same 10 questions; each could read only one `AGENTS.md`.

| | Original AGENTS.md | Extended AGENTS.md |
| --- | --- | --- |
| Answers that were guesses | 9 / 10 | 0 / 10 |
| Correct | 4 (all guessed) | **10** |
| Partly right | 4 | 0 |
| Wrong | 2 | 0 |
| Worst mistakes | **Admin may approve any amount**; Stryker installed without the version (fails on .NET 8); no commit format; no verdict list for review findings | — |

### 3.2 Skills

[`.claude/skills/`](.claude/skills/): `approval-rules` (rules, boundaries, traps, test matrix), `deliver-slice` (one slice = tests first → code → green → one commit), `pr-review` (the review prompt and the fixed findings table), `mutation-testing`, `e2e-playwright`, `ai-interaction-log`; plus Spec Kit's 10 `speckit-*` skills. Sub-agents preload the skills they need (`skills:` in their frontmatter).

### 3.3 Hooks for structured, same-format output

[`.claude/settings.json`](.claude/settings.json) wires Node scripts in [`.claude/hooks/`](.claude/hooks/):

| Hook (event) | Same-format guarantee |
| --- | --- |
| `log-ai-interaction` (UserPromptSubmit, PostToolUse, PostToolUseFailure, SubagentStart/Stop, Stop, PreCompact, SessionEnd) | Every AI interaction becomes one JSON line in schema `ai-log/v1`, secrets masked |
| `guard-commit-message` (PreToolUse, Bash/PowerShell) | Blocks (exit 2) any `git commit` whose message lacks `<type>(<scope>): <summary>` and a `Why:` line |
| `format-csharp` (PostToolUse, Edit/Write) | Runs `dotnet format whitespace` on each C# file the agent edits |
| `enforce-review-format` (SubagentStop, `code-reviewer`) | Sends a review back until it has the exact findings table |
| `session-context` (SessionStart) | Every session starts with the same briefing: branch, active spec, gates |
| [`.githooks/commit-msg`](.githooks/commit-msg) (git) | Same commit check for people and any other tool |

All six were tested with simulated events (pass and block paths, Windows paths, redaction) before use.

### 3.4 Specification with Spec Kit

`specify init --integration claude` + `specify integration install copilot`, then constitution → specify → clarify → plan → tasks → analyze. Committed files: [`.specify/`](.specify/) (constitution, templates, scripts) and [`specs/001-role-approval-limits/`](specs/001-role-approval-limits/): `spec.md` (3 user stories, 17 acceptance scenarios, FR-001…FR-011, SC-001…SC-005, two clarification sessions, glossary), `plan.md`, `research.md` (11 decisions), `data-model.md`, `contracts/approvals-api.md`, `quickstart.md`, `checklists/requirements.md`, `tasks.md` (40 tasks in slices) and `analysis.md`.

**`/speckit-analyze` paid for itself:** run by the `spec-writer` sub-agent, it reported 24 issues, two critical — a class-level role filter would have answered Admin with a bare 403 (no reason, violating AL-5/AL-6), and FR-001 contradicted the first-approver clarification. All were fixed in the spec before the first line of feature code ([`analysis.md`](specs/001-role-approval-limits/analysis.md)).

### 3.5 Two implementation steps

| Step | Commit | Red → green |
| --- | --- | --- |
| 1 — single-approver limits (AL-1, AL-2, AL-5, AL-6) | `c650ef0 feat(approvals): enforce single-approver limits by role` | 8 failing / 9 passing → 26 passing |
| 2 — two distinct approvers above 500,000 (AL-3, AL-4) | `7881efa feat(approvals): require two distinct approvers above 500,000` | 11 failing / 30 passing → 41 passing |

Each message says what changed and `Why:`. The branch has ten commits; every one was checked out alone in a clean worktree and builds and passes its own tests (7, 7, 26, 41, 41, 45, 45, 46, 53, 53):

```text
98cbe3f docs(agents): give a cold agent the rules, layers, tools and checks
136cbdc docs(spec): specify approval limits by role with Spec Kit
c650ef0 feat(approvals): enforce single-approver limits by role          <- step 1
7881efa feat(approvals): require two distinct approvers above 500,000    <- step 2
f19a0cd test(e2e): prove an over-limit approval is refused in the UI
ae795b6 test(approvals): kill every mutant Stryker found surviving
e85d814 fix(hooks): mask secrets inside JSON output and catch every commit form   <- review R1, R4
9a43a47 fix(review): prove "still pending" from the server and close test gaps    <- review R2, R3, R5-R7
b6112ac test(api): cover the approvals HTTP contract with the app in memory       <- review note N1
(last)  docs(assignment): write-up, PR description, review record and AI log
```

### 3.6 Tests that mean something

**Unit tests** ([`ApprovalServiceTests.cs`](backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs), [`InvoiceTests.cs`](backend/tests/InvoiceApp.Tests/InvoiceTests.cs)) — tagged by rule, `dotnet test --filter Rule=AL-4`:

| Rule | Tests | Includes |
| --- | ---: | --- |
| AL-1 Manager ≤ 50,000.00 | 6 | 50,000.00 ✓, 50,000.01 ✗, 120,000 ✗, 500,000.00 ✗, first approver at 500,000.01, over HTTP |
| AL-2 Director ≤ 500,000.00 alone | 3 | 500,000.00 ✓, 120,000 ✓, 500,000.01 → first approval only |
| AL-3 two approvers, Director second | 6 | Director+Director, Manager+Director, Manager as second ✗, over HTTP |
| **AL-4 same person never twice** (negative) | 7 | same Director, same Manager (precedence), case/spaces on either side, over HTTP |
| **AL-5 refused → reason + nothing changes** (negative) | 23 | every refusal: exact reason, JSON snapshot of all invoice properties unchanged, nothing saved; over HTTP the invoice is read back |
| AL-6 roles without a limit | 12 | Admin, retired Approver, `manager`, `Manager `, empty, null; single, first or second approver; role reason wins over same person; over HTTP |

The two negative rules together: **24 tests**. Plus not-pending/not-found/argument guards, Submit/Reject behaviour, invoice defaults and the HTTP contract — **53 tests** (`ApprovalServiceTests` 45, `ApprovalsControllerTests` 7 with the app in memory, `InvoiceTests` 1).

**Reviewing every generated test:** a `test-author` sub-agent went through each step-1 test, named the bug it catches, and hand-applied ~40 mutants. It found real gaps (a Director limit raised to 600,000 survived; swapping the pending and limit checks survived; non-UTC timestamps; culture-dependent money) and proposed a JSON snapshot of all properties so new fields can never slip past the "nothing changes" check. All were added before the step-1 commit.

**Playwright** ([`e2e/tests/over-limit-approval.spec.ts`](e2e/tests/over-limit-approval.spec.ts), page objects in [`e2e/pages/`](e2e/pages/)): a Manager approves a 75,000.00 invoice through the UI → the alert shows the Manager-limit reason → the invoice is still Pending Approval. Role-based locators only. **Proven both ways:** with the Manager limit set to 100,000 the test fails; with the real rule it passes. After review R2 it reloads and reads the invoice back from the server, and it also fails when I plant "set Approved, then throw" in the service (before R2 it passed on that bug).

### 3.7 Agent review, judged

See section 5.

### 3.8 AI interaction log

- **Live:** the `log-ai-interaction` hook appends every prompt, tool call, tool failure, sub-agent start/finish and reply of any Claude Code session opened in this repo to [`ai-logs/ai-interactions.jsonl`](ai-logs/ai-interactions.jsonl) (schema `ai-log/v1`: `ts, source, session_id, event, actor, agent, tool, summary, detail, outcome, branch`).
- **This build:** it ran in a Claude Code session opened in the parent folder, where repo hooks do not load. Its full transcript — every prompt, assistant message, tool call and sub-agent run — was imported in the same format with [`scripts/ai-log/export-transcript.mjs`](scripts/ai-log/export-transcript.mjs) (`source: transcript-export`). Private model reasoning is not logged.
- **Readable:** [`ai-logs/AI_INTERACTIONS.md`](ai-logs/AI_INTERACTIONS.md) via `node scripts/ai-log/render-markdown.mjs`.

### 3.9 Agents and sub-agents

Defined in [`.claude/agents/`](.claude/agents/): lead agent `feature-orchestrator` (`claude --agent feature-orchestrator`) and sub-agents `spec-writer`, `domain-implementer`, `test-author`, `e2e-author`, `mutation-analyst` (read-only), `code-reviewer` (read-only). Each has least-privilege tools and preloaded skills.

Sub-agent runs during this work (all in the AI log):

| Sub-agent run | Role | Result |
| --- | --- | --- |
| Tool-format research | `claude-code-guide` | Confirmed hook/skill/agent formats and Windows behaviour before writing them |
| Cold agent A / B | `Explore` (Sonnet) ×2 | The AGENTS.md A/B proof |
| `/speckit-analyze` | `spec-writer` | 24 findings, 2 critical, all fixed before commit |
| Test review | `test-author` | ~40 hand mutants; gaps added before the step-1 commit |
| PR review | `code-reviewer` | 7 findings + 5 notes, two of them real bugs; section 5 |

## 4. Mutation scores before and after

Stryker.NET 4.13.0 on `InvoiceApp.Domain`, from `backend/tests/InvoiceApp.Tests`. Reports: [`docs/mutation/`](docs/mutation/README.md).

```text
BEFORE — baseline, original code and 7 tests (commit 66cb3f9)
File                             Score       Killed      Timeout     Survived   NoCoverage
ApprovalService.cs              65.00%           12            1            7            0
DiscountCalculator.cs            0.00%            0            0            0           17
Invoice.cs                       0.00%            0            0            2            0
TOTAL                           33.33%           12            1            9           17

AFTER THE FEATURE — both slices, 41 tests (commit f19a0cd)
ApprovalService.cs              85.42%           41            0            7            0
DiscountCalculator.cs            0.00%            0            0            0           17
Invoice.cs                       0.00%            0            0            2            0
TOTAL                           61.19%           41            0            9           17

AFTER KILLING SURVIVORS — 45 tests (commit ae795b6)
ApprovalService.cs             100.00%           48            0            0            0
DiscountCalculator.cs            0.00%            0            0            0           17
Invoice.cs                     100.00%            2            0            0            0
TOTAL                           74.63%           50            0            0           17

FINAL — after the review fixes, 53 tests (commit b6112ac); Stryker now fails below 70%
ApprovalService.cs             100.00%           48            0            0            0
DiscountCalculator.cs            0.00%            0            0            0           17
Invoice.cs                     100.00%            2            0            0            0
TOTAL                           74.63%           50            0            0           17
```

| | Before | After |
| --- | ---: | ---: |
| `ApprovalService.cs` (the approval rules) | **65.00%** | **100.00%** |
| Domain project | **33.33%** | **74.63%** |

What the survivors taught: in the baseline, deleting `_repository.Update(invoice)` survived because the test fake's `Update` did nothing — no test could tell whether a decision was saved. After the feature, the most important survivor was **Reject without its "still pending" check**: an already approved invoice could be rejected afterwards and no test noticed (a pre-existing gap). Both are fixed. `DiscountCalculator` (0%) is untested in the original repo and outside this feature; deferred.

## 5. Agent review and my verdicts

The `code-reviewer` sub-agent ran the repository review prompt (`.claude/skills/pr-review/SKILL.md`, my reconstruction — the workshop's prompt was not in the repo) on `git diff main...HEAD` at `ae795b6`. Its full output is in [`docs/review/code-review.md`](docs/review/code-review.md) and passes the `enforce-review-format` hook. I reproduced each finding before acting on it.

| ID | Severity | Finding | Verdict | Reason / evidence |
| --- | --- | --- | --- | --- |
| R1 | Major | AI-log hook serialised tool output before masking; JSON-escaped secrets reached the log | **Fixed** `e85d814` | Reproduced; `safeJson()` masks before serialising, the pattern handles escaped quotes. Fixed before any log was committed. |
| R2 | Major | E2E "still pending" checks read the old page — blind to "approve then throw" | **Fixed** `9a43a47` | Planted the bug: old test passed, new test fails; real code passes. |
| R3 | Minor | Queue not refreshed after a refusal | **Fixed** `9a43a47` | Refresh after showing the reason. |
| R4 | Minor | Commit guard missed `git commit -am` / `git -C dir commit` | **Fixed** `e85d814` | Reproduced; 11 pass/block cases re-run. |
| R5 | Minor | No test pins "role reason before same-person reason" | **Fixed** `9a43a47` | New theory row. |
| R6 | Nit | 120,000 Manager refusal not fully asserted | **Fixed** `9a43a47` | Full refusal check. |
| R7 | Nit | `FirstApprovedAt` retention not asserted | **Fixed** `9a43a47` | Asserted. |
| N1 | Note | Controller wiring untested | **Fixed** `b6112ac` | 7 in-memory API tests; a hard-coded role or a deleted 403 mapping now fails 3–4 tests. |
| N2 | Note | Race between check and assign on the shared invoice | **Deferred** | Belongs in the storage layer (version check), not a lock on a domain object; in-memory demo store. Listed as a known limitation. |
| N3 | Note | Mutation gate only manual | **Fixed** (threshold 70) `9a43a47` · **Deferred** (CI job) | A Stryker CI job changes the shared pipeline; to agree with the repo owners. |
| N4a | Note | Invoice creator can approve it (separation of duties) | **Deferred** | New rule outside the brief; question for the product owner in the PR. |
| N4b | Note | Same-person reason should say "a different Director" | **Fixed** `9a43a47` | Wording, contract, quickstart, skill updated. |
| N5 | Note | Uncommitted exporter change / untracked experiment write-up | **Dismissed** | Intentional work in progress; both are in the PR (`e85d814`, final docs commit). |

**Judgement in one line:** the reviewer was right about every code finding — two of them were real bugs I had missed — so those are fixed; the notes that ask for new behaviour outside the brief (locking, separation of duties, a CI change) are deferred with reasons rather than slipped into this PR.

## 6. Decisions to confirm and known limitations

Developer decisions (spec Clarifications; listed in the PR for the product owner): limits inclusive; a Manager may give the first of two approvals above 500,000; the Approver role is retired; Admin cannot approve but can still reject; limits do not apply to rejecting; first-approval history is kept.

Known limitations: no optimistic concurrency for simultaneous approvals (in-memory store; review N2); sub-cent amounts not rejected at creation; `DiscountCalculator` untested; separation of duties not specified (review N4a, question for the product owner).

## 7. How to run

```bash
dotnet test backend/InvoiceApp.sln                                  # 53 unit + API tests
cd e2e && npm install && npx playwright install chromium && npm test # 1 e2e test
cd backend/tests/InvoiceApp.Tests && dotnet-stryker                 # mutation run
dotnet run --project backend/src/InvoiceApp.Api                     # app on http://localhost:5000
git config core.hooksPath .githooks                                 # commit-message check
```

Demo accounts and the click-through are in [`AGENTS.md`](AGENTS.md) and [`quickstart.md`](specs/001-role-approval-limits/quickstart.md).

## 8. Demo video

VIDEO_LINK_HERE — 8–10 minutes; unit tests, the Stryker run and the Playwright test run live on screen.
