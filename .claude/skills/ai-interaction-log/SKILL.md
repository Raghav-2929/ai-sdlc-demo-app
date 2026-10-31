---
name: ai-interaction-log
description: How every AI interaction in this repo is logged (hooks write ai-logs/ai-interactions.jsonl in schema ai-log/v1), how to import a session that ran without the hooks, and how to render the readable report. Use when asked about the AI log, to export or summarise AI interactions, or to check what an agent did.
allowed-tools: Read Grep Glob Bash(node scripts/ai-log/*)
---

# AI interaction log

## What writes it

Hooks in `.claude/settings.json` call `.claude/hooks/log-ai-interaction.mjs` on SessionStart, UserPromptSubmit, PostToolUse, PostToolUseFailure, SubagentStart, SubagentStop, Stop, PreCompact and SessionEnd. The commit guard and the review-format hook also log when they block something. Every line has the same fields:

```json
{"schema":"ai-log/v1","ts":"…","source":"claude-code-hook","session_id":"…","event":"PostToolUse","actor":"tool","agent":"main","tool":"Bash","summary":"Bash: dotnet test backend/InvoiceApp.sln","detail":"Passed! …","outcome":"ok","branch":"feature/role-approval-limits"}
```

- `actor`: `user`, `assistant`, `tool`, `subagent` or `system`.
- `agent`: `main`, or the sub-agent name (`code-reviewer`, `test-author`, …).
- `outcome`: `ok`, `error` or `blocked`.
- `summary` is capped at 300 characters and `detail` at 4,000. Secrets, bearer tokens and email addresses are masked before writing.

## Sessions that ran without the hooks

A session started outside this folder does not load the repository hooks. Import its transcript so the log is still complete:

```bash
node scripts/ai-log/export-transcript.mjs "%USERPROFILE%/.claude/projects/<project>/<session-id>.jsonl" --label "What the session did"
```

Sub-agent transcripts in `<session-id>/subagents/` are included automatically.

## Readable report

```bash
node scripts/ai-log/render-markdown.mjs      # writes ai-logs/AI_INTERACTIONS.md
```

## Rules

- Never edit log lines by hand; append only.
- Never paste real credentials into prompts; the mask is a safety net, not a licence.
