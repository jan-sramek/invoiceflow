import { Routes } from '@angular/router';
import { NotFoundPageComponent } from '@core/layout';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'invoices' },
  {
    path: 'invoices',
    loadChildren: () => import('@features/invoices').then((m) => m.INVOICES_ROUTES),
  },
  { path: '**', component: NotFoundPageComponent },
];
