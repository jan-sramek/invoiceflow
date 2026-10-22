import { expect, type Page } from '@playwright/test';

const STATUS_LABEL: Record<string, string> = {
  NeedsReview: 'Needs review',
  PendingApproval: 'Pending approval',
  Approved: 'Approved',
  Paid: 'Paid',
  Processing: 'Processing',
};

export function statusChip(page: Page) {
  return page.locator('if-invoice-status-chip .chip[data-status]');
}

/** Polls the invoice detail page until the status chip matches (worker extraction is async). */
export async function waitForDetailStatus(page: Page, status: keyof typeof STATUS_LABEL): Promise<void> {
  const label = STATUS_LABEL[status] ?? status;
  await expect
    .poll(
      async () => {
        await page.reload({ waitUntil: 'domcontentloaded' });
        return statusChip(page).getAttribute('data-status');
      },
      { timeout: 90_000, intervals: [500, 1_000, 2_000] },
    )
    .toBe(status);
  await expect(statusChip(page)).toContainText(label);
}

export async function fillRequiredFieldsForSubmit(page: Page, runId: string): Promise<void> {
  await page.getByLabel('Supplier').fill(`E2E Supplier ${runId}`);
  await page.getByLabel('Invoice number').fill(`WF-${runId.slice(0, 8)}`);
  await page.getByLabel('Invoice date').fill('2026-01-15');
  await page.getByLabel('Currency').fill('EUR');
  await page.getByLabel('Total').fill('100');
}

export async function saveInvoiceFields(page: Page): Promise<void> {
  const save = page.getByRole('button', { name: 'Save' });
  await expect(save).toBeEnabled();
  await save.click();
  await expect(save).toBeDisabled({ timeout: 30_000 });
}
