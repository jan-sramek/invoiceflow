import { Injectable, signal } from '@angular/core';

/** The personas the API accepts in `X-Development-User` until Keycloak sign-in replaces them. */
export const DEV_USERS = ['clerk', 'approver', 'finance'] as const;
export type DevUser = (typeof DEV_USERS)[number];

export const DEV_USER_HEADER = 'X-Development-User';
const STORAGE_KEY = 'invoiceflow.devUser';

export function isDevUser(value: unknown): value is DevUser {
  return typeof value === 'string' && (DEV_USERS as readonly string[]).includes(value);
}

/** Who the app acts as while there is no real sign-in. Remembered across reloads. */
@Injectable({ providedIn: 'root' })
export class DevUserService {
  private readonly state = signal<DevUser>(read());

  readonly user = this.state.asReadonly();

  select(user: DevUser): void {
    this.state.set(user);
    try {
      localStorage.setItem(STORAGE_KEY, user);
    } catch {
      // Storage may be blocked; the choice then lasts until reload.
    }
  }
}

function read(): DevUser {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    return isDevUser(stored) ? stored : 'clerk';
  } catch {
    return 'clerk';
  }
}
