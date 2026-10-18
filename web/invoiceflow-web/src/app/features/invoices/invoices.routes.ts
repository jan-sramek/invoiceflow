import { Routes } from '@angular/router';

/** Mounted at `/invoices`. `upload` comes before `:id` so it is not taken for an invoice id. */
export const INVOICES_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./feature-list').then((m) => m.InvoiceListPageComponent),
  },
  {
    path: 'upload',
    loadComponent: () => import('./feature-upload').then((m) => m.InvoiceUploadPageComponent),
  },
  {
    path: ':id',
    loadComponent: () => import('./feature-detail').then((m) => m.InvoiceDetailPageComponent),
  },
];
