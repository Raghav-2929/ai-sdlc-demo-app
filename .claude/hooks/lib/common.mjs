// Shared helpers for the Claude Code hooks in this folder.
// Every hook must stay fast, must never throw, and must never print secrets.

import { appendFileSync, mkdirSync, readFileSync, existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

export const LOG_SCHEMA = 'ai-log/v1';

const hooksDir = dirname(dirname(fileURLToPath(import.meta.url)));

/** Repository root: CLAUDE_PROJECT_DIR when Claude Code sets it, otherwise two levels above .claude/hooks. */
export function projectDir() {
  return process.env.CLAUDE_PROJECT_DIR || resolve(hooksDir, '..', '..');
}

/** Reads the JSON event Claude Code sends on stdin. Returns {} when stdin is empty or not JSON. */
export function readHookInput() {
  try {
    const raw = readFileSync(0, 'utf8');
    return raw.trim() ? JSON.parse(raw) : {};
  } catch {
    return {};
  }
}

export function currentBranch() {
  try {
    return execFileSync('git', ['rev-parse', '--abbrev-ref', 'HEAD'], {
      cwd: projectDir(),
      stdio: ['ignore', 'pipe', 'ignore'],
      timeout: 3000,
    }).toString().trim();
  } catch {
    return null;
  }
}

const SECRET_PATTERNS = [
  [/(Bearer\s+)[A-Za-z0-9._~+\/-]{8,}/g, '$1[REDACTED]'],
  [/\b(gh[pousr]_[A-Za-z0-9]{20,})/g, '[REDACTED_GITHUB_TOKEN]'],
  [/\b(sk-[A-Za-z0-9_-]{20,})/g, '[REDACTED_API_KEY]'],
  [/\b(AKIA[0-9A-Z]{16})\b/g, '[REDACTED_AWS_KEY]'],
  [/-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?-----END [A-Z ]*PRIVATE KEY-----/g, '[REDACTED_PRIVATE_KEY]'],
  // key: value / key = value, also when the quotes are JSON-escaped (\"Password\": \"…\").
  [/((?:\\?")?(?:password|passwd|pwd|secret|token|api[_-]?key|client[_-]?secret)(?:\\?")?\s*[:=]\s*)(\\"(?:[^"\\]|\\(?!"))*\\"|"[^"]*"|'[^']*'|[^\s,;}]+)/gi, '$1"[REDACTED]"'],
  [/\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b/g, '[REDACTED_EMAIL]'],
];

const SECRET_KEY = /password|passwd|pwd|secret|token|api[_-]?key|authorization/i;

/** Masks anything that looks like a credential or an email address before it reaches the log. */
export function redact(text) {
  if (text === null || text === undefined) return text;
  let result = String(text);
  for (const [pattern, replacement] of SECRET_PATTERNS) {
    result = result.replace(pattern, replacement);
  }
  return result;
}

/**
 * JSON.stringify that masks before serialising: values under secret-looking keys are replaced, and every
 * other string is passed through redact(). Serialising first would escape the quotes and hide secrets
 * from the patterns above.
 */
export function safeJson(value) {
  return JSON.stringify(value, (key, item) => {
    if (typeof item !== 'string') return item;
    return SECRET_KEY.test(key) ? '[REDACTED]' : redact(item);
  });
}

export function truncate(text, maxLength) {
  if (text === null || text === undefined) return null;
  const value = String(text);
  return value.length <= maxLength ? value : `${value.slice(0, maxLength)}… [+${value.length - maxLength} chars]`;
}

/** One line of the AI interaction log. Every hook and the transcript exporter use this same shape. */
export function buildLogRecord({ input = {}, event, actor, agent = 'main', tool = null, summary, detail = null, outcome = 'ok', source = 'claude-code-hook', timestamp = null }) {
  return {
    schema: LOG_SCHEMA,
    ts: timestamp || new Date().toISOString(),
    source,
    session_id: input.session_id || null,
    event,
    actor,
    agent,
    tool,
    summary: truncate(redact(summary), 300),
    detail: detail === null ? null : truncate(redact(detail), 4000),
    outcome,
    branch: currentBranch(),
  };
}

export function logFilePath() {
  return process.env.AI_LOG_FILE || join(projectDir(), 'ai-logs', 'ai-interactions.jsonl');
}

export function appendLog(record) {
  try {
    const file = logFilePath();
    mkdirSync(dirname(file), { recursive: true });
    appendFileSync(file, `${JSON.stringify(record)}\n`, 'utf8');
  } catch {
    // Logging must never break the agent's work.
  }
}

/** Shortens a tool call to something a reviewer can scan: the command, the file, or the pattern. */
export function describeToolInput(toolName, toolInput = {}) {
  switch (toolName) {
    case 'Bash':
    case 'PowerShell':
      return toolInput.command ?? '';
    case 'Read':
    case 'Write':
    case 'Edit':
    case 'MultiEdit':
    case 'NotebookEdit':
      return toolInput.file_path ?? toolInput.notebook_path ?? '';
    case 'Grep':
    case 'Glob':
      return `${toolInput.pattern ?? ''}${toolInput.path ? ` in ${toolInput.path}` : ''}`;
    case 'Agent':
    case 'Task':
      return `${toolInput.subagent_type ?? 'agent'}: ${toolInput.description ?? ''}`;
    case 'Skill':
      return `${toolInput.skill ?? toolInput.name ?? ''} ${toolInput.args ?? ''}`.trim();
    case 'WebFetch':
    case 'WebSearch':
      return toolInput.url ?? toolInput.query ?? '';
    default:
      return safeJson(toolInput);
  }
}

export function fileExists(path) {
  try {
    return existsSync(path);
  } catch {
    return false;
  }
}
