import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import type { Result } from 'axe-core';

async function ensureClaraClerk(page: Page): Promise<void> {
  await page.goto('/');

  const signIn = page.getByRole('button', { name: /Sign in/i });
  const switcher = page.getByRole('button', { name: /Acting as/ });
  if (await signIn.isVisible().catch(() => false)) {
    const password = process.env['E2E_CLERK_PASSWORD'];
    if (!password) {
      throw new Error('Set E2E_CLERK_PASSWORD to the Keycloak clerk user password (see make e2e-a11y).');
    }
    await signIn.click();
    await expect(page).toHaveURL(/auth\.invoiceflow\.localhost/, { timeout: 30_000 });
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
}

function formatViolations(violations: Result[]): string {
  if (violations.length === 0) {
    return '';
  }
  return violations
    .map((v) => {
      const nodes = v.nodes.map((n) => `  - ${n.target.join(' ')}: ${n.failureSummary ?? n.html}`).join('\n');
      return `[${v.impact}] ${v.id} (${v.help})\n${nodes}`;
    })
    .join('\n\n');
}

async function expectNoSeriousOrCriticalViolations(page: Page): Promise<void> {
  const results = await new AxeBuilder({ page }).analyze();
  const blocking = results.violations.filter((v) => v.impact === 'serious' || v.impact === 'critical');
  expect(blocking, formatViolations(blocking)).toEqual([]);
}

test.describe('accessibility', { tag: '@a11y' }, () => {
  test.beforeEach(async ({ page }) => {
    await ensureClaraClerk(page);
  });

  test('invoices list has no serious or critical axe violations', async ({ page }) => {
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await expectNoSeriousOrCriticalViolations(page);
  });

  test('upload page has no serious or critical axe violations', async ({ page }) => {
    await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('link', { name: 'Upload' }).click();
    await expect(page).toHaveURL(/\/invoices\/upload$/);
    await expect(page.getByRole('heading', { name: 'Upload invoices' })).toBeVisible();
    await expectNoSeriousOrCriticalViolations(page);
  });
});
