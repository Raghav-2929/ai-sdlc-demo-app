# Instructions for GitHub Copilot

All instructions for AI agents in this repository live in [`AGENTS.md`](../AGENTS.md). Read it first: it holds the approval rules (AL-1 … AL-6), the layering, the commands and the review expectations.

- Skills: `.claude/skills/` (approval rules, slice delivery, PR review, mutation testing, Playwright, AI log). Spec Kit skills for Copilot are in `.github/skills/`.
- Custom agents: `.claude/agents/`.
- Commit messages: `<type>(<scope>): <summary>`, a body, and a `Why:` line. Enable the check once per clone with `git config core.hooksPath .githooks`.
- When reviewing a pull request, use the checklist and the output table in `.claude/skills/pr-review/SKILL.md`.
