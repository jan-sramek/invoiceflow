import { expect, type Page } from '@playwright/test';

export type E2ePersona = 'clerk' | 'approver' | 'finance';

const PERSONAS: Record<
  E2ePersona,
  { username: string; displayName: string; passwordEnv: string; makeHint: string }
> = {
  clerk: {
    username: 'clerk',
    displayName: 'Clara Clerk',
    passwordEnv: 'E2E_CLERK_PASSWORD',
    makeHint: 'make e2e-smoke',
  },
  approver: {
    username: 'approver',
    displayName: 'Adam Approver',
    passwordEnv: 'E2E_APPROVER_PASSWORD',
    makeHint: 'make e2e-workflow',
  },
  finance: {
    username: 'finance',
    displayName: 'Fiona Finance',
    passwordEnv: 'E2E_FINANCE_PASSWORD',
    makeHint: 'make e2e-workflow',
  },
};

function personaPassword(persona: E2ePersona): string {
  const spec = PERSONAS[persona];
  const password = process.env[spec.passwordEnv];
  if (!password) {
    throw new Error(`Set ${spec.passwordEnv} to the Keycloak ${spec.username} password (see ${spec.makeHint}).`);
  }
  return password;
}

async function keycloakSignIn(page: Page, persona: E2ePersona): Promise<void> {
  const { username, displayName } = PERSONAS[persona];
  const signIn = page.getByRole('button', { name: /Sign in/i });
  await signIn.click();
  await expect(page).toHaveURL(/auth\.invoiceflow\.localhost/, { timeout: 30_000 });
  await page.locator('#username').fill(username);
  await page.locator('#password').fill(personaPassword(persona));
  await page.locator('#kc-login').click();
  await expect(page.getByRole('button', { name: new RegExp(displayName, 'i') })).toBeVisible({
    timeout: 30_000,
  });
}

async function devUserSwitch(page: Page, persona: E2ePersona): Promise<void> {
  const { displayName } = PERSONAS[persona];
  const switcher = page.getByRole('button', { name: /Acting as/ });
  await expect(switcher).toBeVisible();
  if (!(await switcher.innerText()).includes(displayName)) {
    await switcher.click();
    await page.getByRole('menuitem', { name: displayName }).click();
  }
  await expect(switcher).toContainText(displayName);
}

/** Opens `/` and signs in as the persona (Keycloak OIDC or the development switcher). */
export async function ensureSignedInAs(page: Page, persona: E2ePersona): Promise<void> {
  await page.goto('/');

  const signIn = page.getByRole('button', { name: /Sign in/i });

  if (await signIn.isVisible().catch(() => false)) {
    await keycloakSignIn(page, persona);
  } else {
    await expect(page).toHaveURL(/\/invoices$/);
    await devUserSwitch(page, persona);
  }

  await expect(page).toHaveURL(/\/invoices$/);
}

/** Keycloak: sign out via the account menu. Development: switch persona (no OIDC session). */
export async function signOutOrSwitch(page: Page, nextPersona?: E2ePersona): Promise<void> {
  const signIn = page.getByRole('button', { name: /Sign in/i });
  const switcher = page.getByRole('button', { name: /Acting as/ });

  if (await switcher.isVisible().catch(() => false)) {
    if (nextPersona) {
      await devUserSwitch(page, nextPersona);
    }
    return;
  }

  const account = page.locator('header .tools button.trigger').filter({ hasText: /\S/ }).first();
  await account.click();
  await page.getByRole('menuitem', { name: /Sign out/i }).click();
  await expect(signIn).toBeVisible({ timeout: 30_000 });

  if (nextPersona) {
    await keycloakSignIn(page, nextPersona);
    await expect(page).toHaveURL(/\/invoices$/, { timeout: 30_000 });
  }
}
