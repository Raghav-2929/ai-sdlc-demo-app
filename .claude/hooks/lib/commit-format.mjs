// The one commit message format for this repository, shared by the Claude Code
// PreToolUse guard and the plain git commit-msg hook in .githooks/.
//
//   <type>(<scope>): <what changed, imperative, max 72 chars>
//
//   <body: what changed, in a few lines>
//
//   Why: <the reason this change is needed>

export const COMMIT_TYPES = ['feat', 'fix', 'test', 'docs', 'chore', 'refactor', 'perf', 'build', 'ci', 'style', 'revert'];

const HEADER = new RegExp(`^(${COMMIT_TYPES.join('|')})(\\([a-z0-9][a-z0-9./-]*\\))?!?: \\S.*$`);
const MAX_HEADER_LENGTH = 72;

export const COMMIT_TEMPLATE = [
  'feat(approvals): enforce role approval limits',
  '',
  'Managers may approve up to 50,000 and Directors up to 500,000.',
  'Over-limit attempts are refused with a clear reason.',
  '',
  'Why: the business needs spending authority enforced in the domain.',
].join('\n');

/** Returns { ok, problems } for a full commit message. Comment lines (#) are ignored, as git does. */
export function validateCommitMessage(message) {
  const lines = String(message ?? '')
    .replace(/\r\n/g, '\n')
    .split('\n')
    .filter((line) => !line.startsWith('#'));
  const header = (lines[0] ?? '').trim();
  const problems = [];

  if (/^(Merge|Revert) /.test(header) || /^(fixup|squash)! /.test(header)) {
    return { ok: true, problems };
  }
  if (!HEADER.test(header)) {
    problems.push(`First line "${header}" must look like "<type>(<scope>): <summary>" with type one of ${COMMIT_TYPES.join(', ')}.`);
  }
  if (header.length > MAX_HEADER_LENGTH) {
    problems.push(`First line is ${header.length} characters; keep it at ${MAX_HEADER_LENGTH} or fewer.`);
  }
  if (lines.length > 1 && lines[1].trim() !== '') {
    problems.push('Leave a blank line between the first line and the body.');
  }
  if (!lines.some((line) => /^Why: \S/.test(line.trim()))) {
    problems.push('Add a "Why: <reason>" line so the commit says why the change was made.');
  }
  return { ok: problems.length === 0, problems };
}
