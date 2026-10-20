#!/usr/bin/env node
// Claude Code PostToolUse hook for Edit, Write and MultiEdit.
// After the agent changes a C# file, run `dotnet format whitespace` on just that file so every
// file the agent touches comes out in the same layout as hand-written code. Skips quietly when
// the .NET SDK is not on PATH.

import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { relative } from 'node:path';
import { appendLog, buildLogRecord, projectDir, readHookInput } from './lib/common.mjs';

const input = readHookInput();
const filePath = input.tool_input?.file_path ?? '';
if (!/\.cs$/i.test(filePath)) process.exit(0);

const root = projectDir();
const relativePath = relative(root, filePath).replace(/\\/g, '/');
if (relativePath.startsWith('..') || !relativePath.startsWith('backend/')) process.exit(0);

const before = safeRead(filePath);
try {
  // --include is relative to the workspace folder, so strip the "backend/" prefix.
  execFileSync('dotnet', ['format', 'whitespace', 'backend', '--folder', '--include', relativePath.slice('backend/'.length)], {
    cwd: root,
    stdio: ['ignore', 'pipe', 'pipe'],
    timeout: 60_000,
  });
} catch (error) {
  if (error.code === 'ENOENT') process.exit(0); // No .NET SDK on PATH.
  appendLog(buildLogRecord({ input, event: 'PostToolUse', actor: 'tool', tool: 'dotnet-format', outcome: 'error', summary: `dotnet format failed for ${relativePath}`, detail: String(error.stderr || error.message) }));
  process.exit(0);
}

if (safeRead(filePath) !== before) {
  appendLog(buildLogRecord({ input, event: 'PostToolUse', actor: 'tool', tool: 'dotnet-format', summary: `Formatted ${relativePath}` }));
  process.stdout.write(JSON.stringify({
    hookSpecificOutput: {
      hookEventName: 'PostToolUse',
      additionalContext: `${relativePath} was reformatted by dotnet format whitespace. Read it again before the next edit.`,
    },
  }));
}
process.exit(0);

function safeRead(path) {
  try {
    return readFileSync(path, 'utf8');
  } catch {
    return null;
  }
}
