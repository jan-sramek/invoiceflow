import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'if-not-found-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MatButtonModule, TranslocoPipe],
  template: `
    <section class="card empty">
      <h1>{{ 'app.notFound.title' | transloco }}</h1>
      <p>{{ 'app.notFound.hint' | transloco }}</p>
      <a matButton="filled" routerLink="/invoices">{{ 'app.notFound.action' | transloco }}</a>
    </section>
  `,
  styles: `
    .empty {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: var(--if-space-3);
      padding: var(--if-space-6);
    }
    h1,
    p {
      margin: 0;
    }
  `,
})
export class NotFoundPageComponent {}
