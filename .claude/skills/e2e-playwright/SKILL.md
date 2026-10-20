---
name: e2e-playwright
description: Write and run the Playwright end-to-end tests in e2e/ using page objects and role-based locators. Use when adding or fixing a UI test, debugging a failing e2e run, or proving a rule through the web UI (for example a rejected over-limit approval).
allowed-tools: Read Grep Glob Edit Write Bash(npx playwright *) Bash(npm *)
---

# End-to-end tests with Playwright

## Layout

- `e2e/pages/` page objects, one class per screen area: `LoginPage`, `InvoicesPage`, `ApprovalsPage`. Tests never touch locators directly.
- `e2e/tests/` one spec file per user journey, named `<journey>.spec.ts`.
- `e2e/playwright.config.ts` starts the API (`dotnet run --project ../backend/src/InvoiceApp.Api`) on http://localhost:5000, or reuses one that is already running.

## Rules

- Locate by role and accessible name: `getByRole('button', { name: 'Approve invoice INV-1006' })`, `getByLabel('Amount')`, `getByRole('tabpanel', { name: 'Approvals' })`. Never CSS or XPath selectors.
- Narrow a list with `.filter({ hasText: customerName })`, then locate by role inside it.
- Create the data a test needs with a unique customer name (`Date.now()`), because the in-memory store keeps state while a reused server runs.
- Assert what the user sees: the alert text with the reason, and the invoice still listed as `Pending Approval`.
- Demo accounts (not real credentials) are listed in `AGENTS.md`.

## Run

```bash
cd e2e
npm install                       # first time
npx playwright install chromium   # first time
npm test                          # headless, list reporter
npx playwright test --headed      # watch the browser
npx playwright test --ui          # step through with the Playwright UI
npx playwright show-report        # open the last HTML report
```

If the port is busy, stop the other app or let Playwright reuse it (it does by default).
