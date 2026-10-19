import { randomUUID } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { expect, test } from '@playwright/test';

test.describe('smoke', { tag: '@smoke' }, () => {
  test('a clerk uploads an invoice PDF and finds it in the list', async ({ page }) => {
    // Every run uploads different bytes, so the invoice is never a duplicate of an earlier run's.
    const run = randomUUID();
    const fileName = `smoke-${run}.pdf`;
    // rootDir is testDir (./tests); fixtures live next to playwright.config.ts
    const fixture = join(test.info().config.rootDir, '..', 'fixtures', 'invoice-smoke.pdf');
    const pdf = Buffer.concat([await readFile(fixture), Buffer.from(`%smoke-run ${run}\n`)]);

    await page.goto('/');
    await expect(page).toHaveURL(/\/invoices$/);

    // The development user comes from the switcher (see DevUserService); the default is the clerk.
    const switcher = page.getByRole('button', { name: /Acting as/ });
    await expect(switcher).toBeVisible();
    if (!(await switcher.innerText()).includes('Clara Clerk')) {
      await switcher.click();
      await page.getByRole('menuitem', { name: 'Clara Clerk' }).click();
    }
    await expect(switcher).toContainText('Clara Clerk');

    await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('link', { name: 'Upload' }).click();
    await expect(page).toHaveURL(/\/invoices\/upload$/);
    await expect(page.getByRole('heading', { name: 'Upload invoices' })).toBeVisible();

    // The file input is visually hidden; setting its files is what "Choose files" ends up doing.
    await page.locator('input[type="file"]').setInputFiles({
      name: fileName,
      mimeType: 'application/pdf',
      buffer: pdf,
    });

    const upload = page.getByRole('listitem').filter({ hasText: fileName });
    await expect(upload).toContainText('Uploaded');
    const openLink = upload.getByRole('link', { name: 'Open invoice' });
    const href = await openLink.getAttribute('href');
    expect(href, 'the finished upload links to the new invoice').toMatch(/^\/invoices\/[0-9a-f-]+$/i);

    // The list is sorted by upload time, newest first, so the new invoice is on the first page.
    await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('link', { name: 'Invoices' }).click();
    await expect(page).toHaveURL(/\/invoices$/);
    await expect(page.getByRole('main').locator(`a[href="${href}"]`)).toBeVisible();
  });
});
