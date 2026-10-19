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

    // Cluster: Keycloak OIDC. Local Windows: development persona switcher.
    const signIn = page.getByRole('button', { name: /Sign in/i });
    const switcher = page.getByRole('button', { name: /Acting as/ });
    if (await signIn.isVisible().catch(() => false)) {
      const password = process.env['E2E_CLERK_PASSWORD'];
      if (!password) {
        throw new Error('Set E2E_CLERK_PASSWORD to the Keycloak clerk user password (see make e2e-smoke).');
      }
      await signIn.click();
      await page.locator('#username').fill('clerk');
      await page.locator('#password').fill(password);
      await page.locator('#kc-login').click();
      await expect(page.getByRole('button', { name: /Clara Clerk/i })).toBeVisible({ timeout: 30_000 });
    } else {
      await expect(page).toHaveURL(/\/invoices$/);
      await expect(switcher).toBeVisible();
      if (!(await switcher.innerText()).includes('Clara Clerk')) {
        await switcher.click();
        await page.getByRole('menuitem', { name: 'Clara Clerk' }).click();
      }
      await expect(switcher).toContainText('Clara Clerk');
    }

    await expect(page).toHaveURL(/\/invoices$/);

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
