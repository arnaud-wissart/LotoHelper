import { Component, Inject, OnInit } from '@angular/core';
import { DOCUMENT } from '@angular/common';

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    styleUrls: ['./app.component.scss'],
    standalone: false
})
export class AppComponent implements OnInit {
  title = 'loto-frontend';

  theme: 'light' | 'dark' = 'light';

  constructor(@Inject(DOCUMENT) private readonly document: Document) { }

  ngOnInit(): void {
    const stored = localStorage.getItem('theme') as 'light' | 'dark' | null;
    const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    this.theme = stored ?? (prefersDark ? 'dark' : 'light');
    this.applyTheme();
  }

  toggleTheme(): void {
    this.theme = this.theme === 'light' ? 'dark' : 'light';
    localStorage.setItem('theme', this.theme);
    this.applyTheme();
  }

  private applyTheme(): void {
    const body = this.document.body;
    body.classList.remove('theme-light', 'theme-dark');
    body.classList.add(`theme-${this.theme}`);
  }
}
