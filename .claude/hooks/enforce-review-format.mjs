#!/usr/bin/env node
// Claude Code SubagentStop hook, matcher "code-reviewer".
// The PR description copies the review table row by row and adds a verdict to each row, so every
// review must come out in the same shape. If the reviewer's final message is missing the table,
// block the stop (exit 2) and tell it exactly what to fix.

import { readFileSync } from 'node:fs';
import { appendLog, buildLogRecord, readHookInput } from './lib/common.mjs';
import { checkReviewFormat } from './lib/review-format.mjs';

const input = readHookInput();
if (input.stop_hook_active) process.exit(0); // Already sent back once; never loop.

const problems = checkReviewFormat(lastAssistantText());
if (problems.length === 0) process.exit(0);

appendLog(buildLogRecord({
  input,
  event: 'SubagentStop',
  actor: 'subagent',
  agent: input.agent_type || 'code-reviewer',
  outcome: 'blocked',
  summary: 'Review sent back: output not in the required table format',
  detail: problems.join('\n'),
}));
process.stderr.write([
  'Your review is not in the required format. Re-emit the whole review as described in .claude/skills/pr-review/SKILL.md:',
  ...problems.map((problem) => `- ${problem}`),
].join('\n'));
process.exit(2);

function lastAssistantText() {
  if (input.last_assistant_message) return input.last_assistant_message;
  try {
    const lines = readFileSync(input.agent_transcript_path, 'utf8').trim().split('\n').reverse();
    for (const line of lines) {
      const content = JSON.parse(line)?.message;
      if (content?.role !== 'assistant' || !Array.isArray(content.content)) continue;
      const text = content.content.filter((block) => block.type === 'text').map((block) => block.text).join('\n');
      if (text) return text;
    }
  } catch {
    // Nothing readable: treat as empty, which fails the format check.
  }
  return '';
}
