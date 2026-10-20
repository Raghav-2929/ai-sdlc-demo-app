import { expect, test } from '@playwright/test';
import { ApprovalsPage } from '../pages/ApprovalsPage';
import { InvoicesPage } from '../pages/InvoicesPage';
import { LoginPage } from '../pages/LoginPage';

// Demo account from AGENTS.md; not a real credential.
const manager = { username: 'demo.manager', password: 'ChangeMe123!' };

test('a Manager cannot approve an invoice above 50,000, sees why, and the invoice stays pending', async ({ page }) => {
  // A unique customer keeps the test independent of anything else in the in-memory store.
  const customerName = `Over Limit Academy ${Date.now()}`;
  const login = new LoginPage(page);
  const invoices = new InvoicesPage(page);
  const approvals = new ApprovalsPage(page);

  await login.open();
  await login.signInAs(manager.username, manager.password);
  await invoices.create(customerName, '75000');
  await invoices.submitForApproval(customerName);

  await approvals.open();
  await approvals.approve(customerName);

  await expect(approvals.decisionMessage).toHaveText(
    /is 75,000\.00, which is above the Manager limit of 50,000\.00\. A Director must approve it\./,
  );
  await expect(approvals.pendingItemFor(customerName)).toBeVisible();

  await invoices.open();
  await expect(invoices.statusOf(customerName)).toHaveText('Pending Approval');
});
