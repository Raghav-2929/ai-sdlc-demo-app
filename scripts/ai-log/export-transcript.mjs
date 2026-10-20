#!/usr/bin/env node
// Converts a Claude Code session transcript (~/.claude/projects/<project>/<session>.jsonl) and its
// sub-agent transcripts into the same ai-log/v1 records the hooks write. Use it for a session that
// ran where the repository hooks were not loaded, so the log still holds every AI interaction.
//
//   node scripts/ai-log/export-transcript.mjs <session.jsonl> [--out ai-logs/ai-interactions.jsonl] [--label "what this session did"]
//
// Thinking blocks and system attachments are left out; secrets are masked by the shared redact().

import { existsSync, readFileSync, readdirSync, appendFileSync, mkdirSync } from 'node:fs';
import { basename, dirname, join } from 'node:path';
import { buildLogRecord, describeToolInput, projectDir } from '../../.claude/hooks/lib/common.mjs';

const args = process.argv.slice(2);
const transcriptPath = args.find((arg) => !arg.startsWith('--'));
const outPath = optionValue('--out') || join(projectDir(), 'ai-logs', 'ai-interactions.jsonl');
const label = optionValue('--label');

if (!transcriptPath || !existsSync(transcriptPath)) {
  console.error('Usage: node scripts/ai-log/export-transcript.mjs <session.jsonl> [--out file] [--label text]');
  process.exit(1);
}

function optionValue(name) {
  const index = args.indexOf(name);
  return index >= 0 ? args[index + 1] : null;
}

function readJsonLines(path) {
  return readFileSync(path, 'utf8').split('\n').filter(Boolean).flatMap((line) => {
    try {
      return [JSON.parse(line)];
    } catch {
      return [];
    }
  });
}

function blocksOf(entry) {
  const content = entry?.message?.content;
  if (typeof content === 'string') return [{ type: 'text', text: content }];
  return Array.isArray(content) ? content : [];
}

function resultText(block) {
  if (typeof block.content === 'string') return block.content;
  if (Array.isArray(block.content)) return block.content.filter((part) => part.type === 'text').map((part) => part.text).join('\n');
  return '';
}

function isHumanPrompt(entry) {
  return entry.type === 'user' && !entry.isMeta && entry.origin?.kind !== 'task-notification'
    && blocksOf(entry).some((block) => block.type === 'text') && !blocksOf(entry).some((block) => block.type === 'tool_result');
}

/** Turns one transcript into ai-log/v1 records. `agent` is "main" or the sub-agent type. */
function convert(entries, agent) {
  const records = [];
  const pendingTools = new Map();
  const sessionInput = { session_id: entries.find((entry) => entry.sessionId)?.sessionId ?? null };
  const record = (fields, timestamp) => records.push(buildLogRecord({ input: sessionInput, agent, source: 'transcript-export', timestamp, ...fields }));

  for (const entry of entries) {
    if (entry.type === 'assistant') {
      for (const block of blocksOf(entry)) {
        if (block.type === 'text' && block.text.trim()) {
          record({ event: 'AssistantMessage', actor: agent === 'main' ? 'assistant' : 'subagent', summary: block.text, detail: block.text }, entry.timestamp);
        } else if (block.type === 'tool_use') {
          pendingTools.set(block.id, { name: block.name, input: block.input, timestamp: entry.timestamp });
        }
      }
    } else if (entry.type === 'user') {
      if (isHumanPrompt(entry) && agent === 'main') {
        const text = blocksOf(entry).filter((block) => block.type === 'text').map((block) => block.text).join('\n');
        record({ event: 'UserPromptSubmit', actor: 'user', summary: text, detail: text }, entry.timestamp);
        continue;
      }
      if (isHumanPrompt(entry) && agent !== 'main' && records.length === 0) {
        const text = blocksOf(entry).filter((block) => block.type === 'text').map((block) => block.text).join('\n');
        record({ event: 'SubagentStart', actor: 'assistant', summary: `Delegated to ${agent}: ${text}`, detail: text }, entry.timestamp);
        continue;
      }
      for (const block of blocksOf(entry).filter((item) => item.type === 'tool_result')) {
        const call = pendingTools.get(block.tool_use_id);
        if (!call) continue;
        pendingTools.delete(block.tool_use_id);
        record({
          event: block.is_error ? 'PostToolUseFailure' : 'PostToolUse',
          actor: 'tool',
          tool: call.name,
          outcome: block.is_error ? 'error' : 'ok',
          summary: `${call.name}: ${describeToolInput(call.name, call.input)}`,
          detail: resultText(block),
        }, entry.timestamp);
      }
    }
  }
  return records;
}

const mainEntries = readJsonLines(transcriptPath);
const sessionId = basename(transcriptPath, '.jsonl');
let records = convert(mainEntries, 'main');

// Repository agents (.claude/agents) that may be run through a generic agent type; the description names the role.
const REPO_AGENTS = ['feature-orchestrator', 'spec-writer', 'domain-implementer', 'test-author', 'e2e-author', 'mutation-analyst', 'code-reviewer'];

const subagentDir = join(dirname(transcriptPath), sessionId, 'subagents');
if (existsSync(subagentDir)) {
  for (const file of readdirSync(subagentDir).filter((name) => /^agent-.*\.jsonl$/.test(name))) {
    const metaPath = join(subagentDir, file.replace(/\.jsonl$/, '.meta.json'));
    let agentType = 'subagent';
    try {
      const meta = JSON.parse(readFileSync(metaPath, 'utf8'));
      agentType = REPO_AGENTS.find((name) => (meta.description || '').includes(name)) || meta.agentType || agentType;
    } catch {
      // Keep the generic name.
    }
    const subRecords = convert(readJsonLines(join(subagentDir, file)), agentType);
    records = records.concat(subRecords.map((item) => ({ ...item, session_id: item.session_id ?? sessionId })));
  }
}

records.sort((left, right) => left.ts.localeCompare(right.ts));
if (label) {
  records.unshift(buildLogRecord({ input: { session_id: sessionId }, event: 'SessionStart', actor: 'system', source: 'transcript-export', timestamp: records[0]?.ts, summary: label }));
}

// Re-running the export appends only what is new, so a session can be exported again as it grows.
const identity = (item) => [item.session_id, item.ts, item.event, item.agent, item.tool, item.summary].join('|');
const alreadyLogged = new Set(existsSync(outPath) ? readJsonLines(outPath).map(identity) : []);
const newRecords = records.filter((item) => !alreadyLogged.has(identity(item)));

mkdirSync(dirname(outPath), { recursive: true });
appendFileSync(outPath, newRecords.map((item) => JSON.stringify(item)).join('\n') + (newRecords.length ? '\n' : ''), 'utf8');
console.log(`Exported ${newRecords.length} new records (${records.length - newRecords.length} already logged) from ${sessionId} to ${outPath}`);
