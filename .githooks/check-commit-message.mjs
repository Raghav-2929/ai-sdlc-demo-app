#!/usr/bin/env node
// Called by .githooks/commit-msg with the path of the message file git is about to commit.
import { readFileSync } from 'node:fs';
import { COMMIT_TEMPLATE, validateCommitMessage } from '../.claude/hooks/lib/commit-format.mjs';

const { ok, problems } = validateCommitMessage(readFileSync(process.argv[2], 'utf8'));
if (!ok) {
  console.error(['Commit message rejected:', ...problems.map((problem) => `- ${problem}`), '', 'Expected format:', COMMIT_TEMPLATE].join('\n'));
  process.exit(1);
}
