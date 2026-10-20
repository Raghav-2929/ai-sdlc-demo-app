import { expect, type Locator, type Page } from '@playwright/test';

/** The Invoices tab: create an invoice, submit it, read its status. */
export class InvoicesPage {
  constructor(private readonly page: Page) {}

  private get panel(): Locator {
    return this.page.getByRole('tabpanel', { name: 'Invoices' });
  }

  async open() {
    await this.page.getByRole('tab', { name: 'Invoices' }).click();
    await expect(this.panel).toBeVisible();
  }

  /** Reloads the page so everything shown is read again from the server (the session survives the reload). */
  async reloadFromServer() {
    await this.page.reload();
    await expect(this.panel).toBeVisible();
  }

  async create(customerName: string, amount: string) {
    await this.panel.getByLabel('Customer name').fill(customerName);
    await this.panel.getByLabel('Amount').fill(amount);
    await this.panel.getByRole('button', { name: 'Create invoice' }).click();
    await expect(this.rowFor(customerName)).toBeVisible();
  }

  async submitForApproval(customerName: string) {
    await this.rowFor(customerName).getByRole('button', { name: /^Submit invoice .* for approval$/ }).click();
    await expect(this.statusOf(customerName)).toHaveText('Pending Approval');
  }

  rowFor(customerName: string): Locator {
    return this.panel.getByRole('row').filter({ hasText: customerName });
  }

  /** The Status cell of the invoice's row (the fourth column). */
  statusOf(customerName: string): Locator {
    return this.rowFor(customerName).getByRole('cell').nth(3);
  }
}
