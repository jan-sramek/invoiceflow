import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTransloco, Translation, TranslocoLoader } from '@jsverse/transloco';
import { of } from 'rxjs';
import { App } from './app';

class InlineLoader implements TranslocoLoader {
  getTranslation() {
    return of({
      app: {
        name: 'InvoiceFlow',
        nav: { label: 'Main navigation', invoices: 'Invoices', upload: 'Upload' },
        devUser: { label: 'Acting as', hint: 'hint', clerk: 'Clara Clerk', approver: 'Adam Approver', finance: 'Fiona Finance' },
        language: { label: 'Language', en: 'English', cs: 'Čeština', de: 'Deutsch' },
        skipToContent: 'Skip to content',
      },
    } satisfies Translation);
  }
}

describe('App', () => {
  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTransloco({
          config: { availableLangs: ['en', 'cs', 'de'], defaultLang: 'en', prodMode: true },
          loader: InlineLoader,
        }),
      ],
    }).compileComponents();
  });

  it('shows the brand and the navigation in the shell header', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const root = fixture.nativeElement as HTMLElement;

    expect(root.querySelector('header .brand')?.textContent).toContain('InvoiceFlow');
    const links = Array.from(root.querySelectorAll('nav a')).map((a) => a.textContent?.trim());
    expect(links).toEqual(['Invoices', 'Upload']);
  });

  it('has the development user and language switchers in the header', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const root = fixture.nativeElement as HTMLElement;

    expect(root.querySelector('header if-dev-user-switcher')?.textContent).toContain('Clara Clerk');
    const languages = Array.from(root.querySelectorAll('header if-language-switcher button')).map((b) =>
      b.textContent?.trim(),
    );
    expect(languages).toEqual(['EN', 'CS', 'DE']);
  });

  it('provides the routed page inside the main landmark', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector('main#main router-outlet')).not.toBeNull();
  });
});
