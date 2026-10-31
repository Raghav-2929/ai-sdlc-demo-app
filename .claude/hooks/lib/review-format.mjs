// The one output format for agent code reviews (see .claude/skills/pr-review/SKILL.md).
// The PR description copies these rows and adds a verdict to each, so the shape must not drift.

export const REVIEW_TABLE_HEADER = '| ID | Severity | Category | Location | Finding | Suggested fix |';
export const SEVERITIES = ['Blocker', 'Major', 'Minor', 'Nit'];

/** Returns a list of problems; an empty list means the review is in the required format. */
export function checkReviewFormat(message) {
  const text = String(message ?? '');
  const problems = [];
  if (!/^## Review findings\s*$/m.test(text)) problems.push('Missing the "## Review findings" heading.');
  if (!text.includes(REVIEW_TABLE_HEADER)) problems.push(`Missing the table header: ${REVIEW_TABLE_HEADER}`);

  const rows = text.split(/\r?\n/).filter((line) => /^\|\s*R\d+\s*\|/.test(line));
  for (const row of rows) {
    const cells = row.replace(/\\\|/g, '/').split('|').slice(1, -1).map((cell) => cell.trim());
    if (cells.length !== 6) problems.push(`Row ${cells[0]} has ${cells.length} cells; it needs 6.`);
    else if (!SEVERITIES.includes(cells[1])) problems.push(`Row ${cells[0]} severity "${cells[1]}" must be one of ${SEVERITIES.join(', ')}.`);
  }
  if (rows.length === 0 && !/No findings\./.test(text)) problems.push('Add at least one R-numbered row, or write "No findings." under the table.');
  return problems;
}
