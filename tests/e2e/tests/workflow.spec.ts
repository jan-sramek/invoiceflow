import { randomUUID } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { expect, test } from '@playwright/test';
import { ensureSignedInAs, signOutOrSwitch } from './helpers/auth.js';
import {
  fillRequiredFieldsForSubmit,
  saveInvoiceFields,
  waitForDetailStatus,
} from './helpers/invoice-ui.js';

test.describe('invoice workflow', { tag: '@workflow' }, () => {
  test.setTimeout(180_000);

  test('clerk uploads, submits; approver approves; finance marks paid', async ({ page }) => {
    const run = randomUUID();
    const fileName = `workflow-${run}.pdf`;
    const fixture = join(test.info().config.rootDir, '..', 'fixtures', 'invoice-smoke.pdf');
    const pdf = Buffer.concat([await readFile(fixture), Buffer.from(`%workflow-run ${run}\n`)]);

    await ensureSignedInAs(page, 'clerk');

    await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('link', { name: 'Upload' }).click();
    await expect(page).toHaveURL(/\/invoices\/upload$/);

    await page.locator('input[type="file"]').setInputFiles({
      name: fileName,
      mimeType: 'application/pdf',
      buffer: pdf,
    });

    const upload = page.getByRole('listitem').filter({ hasText: fileName });
    await expect(upload).toContainText('Uploaded');
    const openLink = upload.getByRole('link', { name: 'Open invoice' });
    const href = await openLink.getAttribute('href');
    expect(href).toMatch(/^\/invoices\/[0-9a-f-]+$/i);

    await openLink.click();
    await expect(page).toHaveURL(new RegExp(href!.replace('/', '\\/') + '$'));

    // Fake extractor: Uploaded → Processing → NeedsReview with empty fields (see README).
    await waitForDetailStatus(page, 'NeedsReview');

    await fillRequiredFieldsForSubmit(page, run);
    await saveInvoiceFields(page);

    await page.getByRole('button', { name: 'Submit for approval' }).click();
    await waitForDetailStatus(page, 'PendingApproval');

    await signOutOrSwitch(page, 'approver');
    await page.goto(href!);
    await expect(page.getByRole('button', { name: 'Approve' })).toBeVisible({ timeout: 15_000 });
    await page.getByRole('button', { name: 'Approve' }).click();
    await waitForDetailStatus(page, 'Approved');

    await signOutOrSwitch(page, 'finance');
    await page.goto(href!);
    await expect(page.getByRole('button', { name: 'Mark as paid' })).toBeVisible({ timeout: 15_000 });
    await page.getByRole('button', { name: 'Mark as paid' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await dialog.getByLabel('Payment reference').fill(`e2e-${run.slice(0, 8)}`);
    await dialog.getByRole('button', { name: 'Mark as paid' }).click();
    await waitForDetailStatus(page, 'Paid');

    await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('link', { name: 'Invoices' }).click();
    await expect(page).toHaveURL(/\/invoices$/);
    const row = page.getByRole('row').filter({ has: page.locator(`a[href="${href}"]`) });
    await expect(row.locator('[data-status="Paid"]')).toBeVisible();
  });
});
