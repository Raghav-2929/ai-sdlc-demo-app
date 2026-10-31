---
name: mutation-testing
description: Run Stryker.NET mutation testing on the Domain project, read the score and surviving mutants, and record before/after scores. Use when asked to run mutation tests, check test strength, explain a mutation score, or kill surviving mutants.
allowed-tools: Read Grep Glob Bash(dotnet-stryker *) Bash(dotnet test *) Bash(python *) Bash(node *)
---

# Mutation testing with Stryker.NET

A mutation test changes the production code on purpose (a *mutant*: `>` becomes `>=`, a line is deleted, a string is emptied) and runs the tests. If a test fails, the mutant is **killed** (good). If every test still passes, the mutant **survived**: the tests would not notice that bug.

**Mutation score = (killed + timeout) / (killed + timeout + survived + no coverage).**

## Install (once per machine)

Stryker on .NET 8 needs this exact version; newer releases target a later runtime and fail to install:

```bash
dotnet tool install -g dotnet-stryker --version 4.13.0
```

## Run

```bash
cd backend/tests/InvoiceApp.Tests
dotnet-stryker                       # uses stryker-config.json in this folder
dotnet-stryker --open-report         # same, and opens the HTML report while it runs
```

`stryker-config.json` mutates `InvoiceApp.Domain` and writes HTML, JSON and a clear-text table to `StrykerOutput/<timestamp>/reports/` (git-ignored). A full run takes about a minute. The `break` threshold is 70: the run exits non-zero if the Domain score falls below 70% (it is 74.63% today; the baseline was 33.33%).

## Read the result

- The last line prints the score. The clear-text table shows it per file.
- Open `StrykerOutput/<timestamp>/reports/mutation-report.html` and filter by **Survived**. Each survivor shows the exact change that no test caught.
- `NoCoverage` means no test even runs that code: add a test before anything else.

## Kill a survivor

1. Read the mutant: what bug does it simulate?
2. Decide: is that bug possible and does it matter? If yes, write the test that fails on it (assert on the value, the reason text, or the repository update count). If it is an equivalent mutant (same behaviour), note it instead.
3. Re-run Stryker and confirm it is killed.

Known lesson in this repo: in the baseline run, deleting `_repository.Update(invoice)` survived three times because `FakeInvoiceRepository.Update` does nothing, so no test can tell whether a decision was saved. The fix is a fake that counts updates and tests that assert on the count, which is also how "a denied attempt changes nothing" is proven.

## Record before and after

Scores go into `ASSIGNMENT.md` as pasted text. Keep the JSON reports under `docs/mutation/<label>/` so the numbers can be checked later:

```bash
python scripts/mutation/summarize.py docs/mutation/<label>/mutation-report.json
```
