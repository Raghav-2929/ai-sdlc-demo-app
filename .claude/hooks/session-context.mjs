#!/usr/bin/env node
// Claude Code SessionStart hook. Logs the session start and hands the agent a short,
// always-current briefing: branch, active Spec Kit feature, and the checks it must hold.

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { appendLog, buildLogRecord, currentBranch, projectDir, readHookInput } from './lib/common.mjs';

const input = readHookInput();
appendLog(buildLogRecord({ input, event: 'SessionStart', actor: 'system', agent: input.agent_type || 'main', summary: `Session ${input.source || 'start'}${input.model ? ` (${input.model})` : ''}` }));

let featureDirectory = null;
try {
  featureDirectory = JSON.parse(readFileSync(join(projectDir(), '.specify', 'feature.json'), 'utf8')).feature_directory;
} catch {
  // No active Spec Kit feature yet.
}

const briefing = [
  `Branch: ${currentBranch() ?? 'unknown'}.`,
  featureDirectory ? `Active Spec Kit feature: ${featureDirectory} (spec.md, plan.md, tasks.md).` : 'No active Spec Kit feature; start one with /speckit-specify.',
  'Approval rules, layering, commands and review expectations are in AGENTS.md. Read it before changing approval code.',
  'Before every commit: `dotnet test backend/InvoiceApp.sln` passes, and the message has a "<type>(<scope>): <summary>" line and a "Why:" line.',
  'Every prompt, tool call and sub-agent result in this session is logged to ai-logs/ai-interactions.jsonl.',
].join('\n');

process.stdout.write(JSON.stringify({ hookSpecificOutput: { hookEventName: 'SessionStart', additionalContext: briefing } }));
process.exit(0);
