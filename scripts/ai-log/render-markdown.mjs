#!/usr/bin/env node
// Renders ai-logs/ai-interactions.jsonl as a readable Markdown report (ai-logs/AI_INTERACTIONS.md):
// totals per event and per agent, then every interaction in time order, grouped by session.
//
//   node scripts/ai-log/render-markdown.mjs [in.jsonl] [out.md]

import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { projectDir } from '../../.claude/hooks/lib/common.mjs';

const inPath = process.argv[2] || join(projectDir(), 'ai-logs', 'ai-interactions.jsonl');
const outPath = process.argv[3] || join(projectDir(), 'ai-logs', 'AI_INTERACTIONS.md');

const records = readFileSync(inPath, 'utf8').split('\n').filter(Boolean).map((line) => JSON.parse(line));

const countBy = (key) => records.reduce((counts, item) => ({ ...counts, [item[key]]: (counts[item[key]] || 0) + 1 }), {});
const table = (counts, heading) => [
  `| ${heading} | Count |`,
  '| --- | ---: |',
  ...Object.entries(counts).sort((left, right) => right[1] - left[1]).map(([name, count]) => `| ${name} | ${count} |`),
].join('\n');
const oneLine = (text) => String(text ?? '').replace(/\s+/g, ' ').replace(/\|/g, '\\|').slice(0, 220);
const quote = (text) => String(text ?? '').split('\n').map((line) => `> ${line}`).join('\n');

const sessions = [...new Set(records.map((item) => item.session_id || 'unknown'))];
const lines = [
  '# AI interaction log',
  '',
  `Generated from \`ai-logs/ai-interactions.jsonl\` (${records.length} records, schema \`ai-log/v1\`).`,
  'Every line in the JSONL file has the same fields: `ts, source, session_id, event, actor, agent, tool, summary, detail, outcome, branch`.',
  '',
  '## Totals',
  '',
  table(countBy('event'), 'Event'),
  '',
  table(countBy('agent'), 'Agent'),
  '',
  table(countBy('outcome'), 'Outcome'),
  '',
];

for (const session of sessions) {
  const items = records.filter((item) => (item.session_id || 'unknown') === session);
  const opening = items.find((item) => item.event === 'SessionStart');
  lines.push(`## Session \`${session}\``, '');
  if (opening) lines.push(`${opening.summary}`, '');
  lines.push(`${items[0].ts} → ${items[items.length - 1].ts} · ${items.length} records · source: ${[...new Set(items.map((item) => item.source))].join(', ')}`, '');

  let previousWasListItem = false;
  const block = (...blockLines) => {
    if (previousWasListItem) lines.push('');
    lines.push(...blockLines, '');
    previousWasListItem = false;
  };

  for (const item of items) {
    const time = item.ts.replace('T', ' ').slice(0, 19);
    if (item.event === 'UserPromptSubmit') {
      block(`### ${time} · Prompt`, '', quote(item.detail || item.summary));
    } else if (item.event === 'AssistantMessage' || item.event === 'Stop' || item.event === 'SubagentStop') {
      block(`**${time} · ${item.agent === 'main' ? 'Agent' : `Sub-agent \`${item.agent}\``}${item.outcome !== 'ok' ? ` (${item.outcome})` : ''}:** ${oneLine(item.summary)}`);
    } else if (item.event === 'SubagentStart') {
      block(`**${time} · Delegated to \`${item.agent}\`:** ${oneLine(item.detail || item.summary)}`);
    } else {
      const marker = item.outcome === 'ok' ? '' : ` **${item.outcome}**`;
      lines.push(`- ${time} · \`${item.agent}\` · ${item.event}${marker} · ${oneLine(item.summary)}`);
      previousWasListItem = true;
    }
  }
  lines.push('');
}

writeFileSync(outPath, `${lines.join('\n')}\n`, 'utf8');
console.log(`Wrote ${outPath} (${records.length} records, ${sessions.length} sessions)`);
