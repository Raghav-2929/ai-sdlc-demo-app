#!/usr/bin/env node
// Claude Code hook: appends one structured line per AI interaction to ai-logs/ai-interactions.jsonl.
// Wired to SessionStart, UserPromptSubmit, PostToolUse, PostToolUseFailure, SubagentStart,
// SubagentStop, Stop, PreCompact and SessionEnd in .claude/settings.json.
// It only observes: it always exits 0 and never changes what the agent does.

import { appendLog, buildLogRecord, describeToolInput, readHookInput } from './lib/common.mjs';

const input = readHookInput();
const event = input.hook_event_name || process.argv[2] || 'Unknown';
const agent = input.agent_type || 'main';

function summarizeToolResponse(response) {
  if (response === null || response === undefined) return null;
  if (typeof response === 'string') return response;
  if (response.stdout !== undefined || response.stderr !== undefined) {
    return [response.stdout, response.stderr].filter(Boolean).join('\n');
  }
  if (response.filePath || response.file_path) return `file: ${response.filePath || response.file_path}`;
  return JSON.stringify(response);
}

function recordFor() {
  switch (event) {
    case 'SessionStart':
      return buildLogRecord({ input, event, actor: 'system', agent, summary: `Session ${input.source || 'start'}${input.model ? ` (${input.model})` : ''}` });
    case 'UserPromptSubmit':
      return buildLogRecord({ input, event, actor: 'user', agent, summary: input.prompt, detail: input.prompt });
    case 'PostToolUse':
      return buildLogRecord({
        input, event, actor: 'tool', agent, tool: input.tool_name,
        summary: `${input.tool_name}: ${describeToolInput(input.tool_name, input.tool_input)}`,
        detail: summarizeToolResponse(input.tool_response),
      });
    case 'PostToolUseFailure':
      return buildLogRecord({
        input, event, actor: 'tool', agent, tool: input.tool_name, outcome: 'error',
        summary: `${input.tool_name} failed: ${describeToolInput(input.tool_name, input.tool_input)}`,
        detail: input.error || summarizeToolResponse(input.tool_response),
      });
    case 'SubagentStart':
      return buildLogRecord({ input, event, actor: 'subagent', agent, summary: `Sub-agent ${agent} started` });
    case 'SubagentStop':
      return buildLogRecord({ input, event, actor: 'subagent', agent, summary: `Sub-agent ${agent} finished`, detail: input.last_assistant_message });
    case 'Stop':
      return buildLogRecord({ input, event, actor: 'assistant', agent, summary: input.last_assistant_message || 'Turn finished', detail: input.last_assistant_message });
    case 'PreCompact':
      return buildLogRecord({ input, event, actor: 'system', agent, summary: `Context compaction (${input.trigger || 'auto'})` });
    case 'SessionEnd':
      return buildLogRecord({ input, event, actor: 'system', agent, summary: `Session ended (${input.reason || 'unknown'})` });
    default:
      return buildLogRecord({ input, event, actor: 'system', agent, summary: event, detail: JSON.stringify(input) });
  }
}

appendLog(recordFor());
process.exit(0);
