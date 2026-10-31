# AI interaction log

| File | What it is |
| --- | --- |
| `ai-interactions.jsonl` | The log. One JSON object per line, schema `ai-log/v1`, appended by the Claude Code hooks in `.claude/settings.json` (and by `scripts/ai-log/export-transcript.mjs` for sessions that ran without them). |
| `AI_INTERACTIONS.md` | The same log rendered for reading: `node scripts/ai-log/render-markdown.mjs`. |

Every line has the same fields:

| Field | Meaning |
| --- | --- |
| `schema` | Always `ai-log/v1`. |
| `ts` | UTC timestamp. |
| `source` | `claude-code-hook` (written live) or `transcript-export` (imported from a session transcript). |
| `session_id` | Claude Code session. |
| `event` | `SessionStart`, `UserPromptSubmit`, `PostToolUse`, `PostToolUseFailure`, `SubagentStart`, `SubagentStop`, `AssistantMessage`, `Stop`, `PreToolUse` (only when a guard blocks), `PreCompact`, `SessionEnd`. |
| `actor` | `user`, `assistant`, `tool`, `subagent` or `system`. |
| `agent` | `main`, or the sub-agent that acted (`code-reviewer`, `test-author`, …). |
| `tool` | Tool name for tool events, otherwise `null`. |
| `summary` | One line, at most 300 characters. |
| `detail` | Prompt, reply or tool output, at most 4,000 characters. |
| `outcome` | `ok`, `error` or `blocked`. |
| `branch` | Git branch when the line was written. |

Secrets, bearer tokens and email addresses are masked before a line is written. The model's private reasoning is not logged.
