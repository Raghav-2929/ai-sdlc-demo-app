#!/usr/bin/env node
// Claude Code PreToolUse hook for Bash and PowerShell.
// When the agent runs `git commit`, it checks the message against lib/commit-format.mjs.
// A bad message is blocked with exit code 2 and the reason goes back to the agent, so every
// commit lands in the same format: "<type>(<scope>): <summary>", a body, and a "Why:" line.

import { readFileSync } from 'node:fs';
import { isAbsolute, join } from 'node:path';
import { appendLog, buildLogRecord, readHookInput } from './lib/common.mjs';
import { COMMIT_TEMPLATE, validateCommitMessage } from './lib/commit-format.mjs';

const input = readHookInput();
const command = input.tool_input?.command ?? '';

// `git [-C <dir>] [-c <key=value>] [--global-flag] commit …`
const commitCall = /\bgit(?:\s+(?:-[Cc]\s+(?:"[^"]*"|'[^']*'|\S+)|--?[\w-]+(?:=\S+)?))*\s+commit\b/.exec(command);
if (!commitCall) process.exit(0);
const commitArguments = command.slice(commitCall.index + commitCall[0].length);
const reusesMessage = /--no-edit\b|--reuse-message\b|--reedit-message\b|\s-[cC]\s|--fixup\b|--squash\b/.test(` ${commitArguments}`);
if (reusesMessage) process.exit(0);

function extractMessage(text) {
  const heredoc = text.match(/<<-?\s*['"]?(\w+)['"]?\r?\n([\s\S]*?)\r?\n\s*\1\b/);
  if (heredoc) return heredoc[2];

  const hereString = text.match(/@(['"])\r?\n([\s\S]*?)\r?\n\1@/);
  if (hereString) return hereString[2];

  const fileOption = text.match(/(?:\s-[a-zA-Z]*F|\s--file)(?:=|\s*)("([^"]+)"|'([^']+)'|(\S+))/);
  if (fileOption) {
    const path = fileOption[2] ?? fileOption[3] ?? fileOption[4];
    const fullPath = isAbsolute(path) ? path : join(input.cwd || process.cwd(), path);
    try {
      return readFileSync(fullPath, 'utf8');
    } catch {
      return null;
    }
  }

  // -m, combined short flags such as -am, and --message
  const messages = [...text.matchAll(/(?:\s-[a-zA-Z]*m|\s--message)(?:=|\s*)("((?:\\.|[^"\\])*)"|'([^']*)')/g)]
    .map((match) => (match[2] ?? match[3] ?? '').replace(/\\"/g, '"').replace(/\\n/g, '\n'));
  return messages.length ? messages.join('\n\n') : null;
}

const message = extractMessage(` ${commitArguments}`);
if (message === null) process.exit(0); // Message comes from an editor or a file we cannot read; the git commit-msg hook still checks it.

const { ok, problems } = validateCommitMessage(message);
if (ok) process.exit(0);

appendLog(buildLogRecord({
  input,
  event: 'PreToolUse',
  actor: 'tool',
  agent: input.agent_type || 'main',
  tool: input.tool_name,
  outcome: 'blocked',
  summary: 'Commit blocked: message does not follow the repository format',
  detail: problems.join('\n'),
}));

process.stderr.write([
  'Commit blocked by .claude/hooks/guard-commit-message.mjs. Fix the message and commit again:',
  ...problems.map((problem) => `- ${problem}`),
  '',
  'Expected format:',
  COMMIT_TEMPLATE,
].join('\n'));
process.exit(2);
