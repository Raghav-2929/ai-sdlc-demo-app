import { expect, type Locator, type Page } from '@playwright/test';

/** The Approvals tab: the queue of pending invoices and the decision message. */
export class ApprovalsPage {
  constructor(private readonly page: Page) {}

  private get panel(): Locator {
    return this.page.getByRole('tabpanel', { name: 'Approvals' });
  }

  async open() {
    await this.page.getByRole('tab', { name: 'Approvals' }).click();
    await expect(this.panel).toBeVisible();
  }

  pendingItemFor(customerName: string): Locator {
    return this.panel.getByRole('listitem').filter({ hasText: customerName });
  }

  async approve(customerName: string) {
    await this.pendingItemFor(customerName).getByRole('button', { name: /^Approve invoice / }).click();
  }

  /** The alert that shows why the last approve or reject did not go through. */
  get decisionMessage(): Locator {
    return this.panel.getByRole('alert');
  }
}
