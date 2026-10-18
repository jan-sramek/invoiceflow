import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { Language, LanguageService } from '../i18n';

/** EN | CS toggle, shown in the header so the language can be changed from the first screen. */
@Component({
  selector: 'if-language-switcher',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe],
  template: `
    <div class="group" role="group" [attr.aria-label]="'app.language.label' | transloco">
      @for (code of language.available; track code) {
        <button
          type="button"
          [class.active]="language.lang() === code"
          [attr.aria-pressed]="language.lang() === code"
          [attr.lang]="code"
          [attr.title]="('app.language.' + code) | transloco"
          (click)="select(code)"
        >
          {{ code.toUpperCase() }}
        </button>
      }
    </div>
  `,
  styles: `
    .group {
      display: inline-flex;
      border: 1px solid var(--if-header-border);
      border-radius: var(--if-radius-sm);
      overflow: hidden;
    }
    button {
      border: 0;
      background: transparent;
      color: var(--if-header-text-muted);
      font: inherit;
      font-size: 12px;
      font-weight: 600;
      letter-spacing: 0.04em;
      padding: 6px 10px;
      cursor: pointer;
    }
    button:hover {
      color: var(--if-header-text);
    }
    button.active {
      background: var(--if-header-active-bg);
      color: var(--if-header-text);
    }
    button:focus-visible {
      outline: 2px solid var(--if-focus-ring);
      outline-offset: -2px;
    }
  `,
})
export class LanguageSwitcherComponent {
  protected readonly language = inject(LanguageService);

  protected select(lang: Language): void {
    this.language.use(lang);
  }
}
