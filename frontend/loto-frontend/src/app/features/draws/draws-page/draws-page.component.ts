import { Component, OnDestroy, OnInit } from '@angular/core';
import { Subscription } from 'rxjs';
import { Draw } from '../../../models/draw';
import { DrawsFilter } from '../../../models/draws-filter';
import { DrawsService } from '../../../services/draws.service';

@Component({
    selector: 'app-draws-page',
    templateUrl: './draws-page.component.html',
    styleUrls: ['./draws-page.component.scss'],
    standalone: false
})
export class DrawsPageComponent implements OnInit, OnDestroy {
  draws: Draw[] = [];
  isLoading = false;
  errorMessage: string | null = null;

  page = 1;
  pageSize = 10;
  totalCount = 0;
  totalPages = 0;

  dateFrom: Date | null = null;
  dateTo: Date | null = null;

  private sub?: Subscription;

  constructor(private readonly drawsService: DrawsService) { }

  ngOnInit(): void {
    this.loadDraws();
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  loadDraws(): void {
    this.isLoading = true;
    this.errorMessage = null;

    const filter: DrawsFilter = {
      page: this.page,
      pageSize: this.pageSize,
      dateFrom: this.dateFrom ? this.toApiDate(this.dateFrom) : undefined,
      dateTo: this.dateTo ? this.toApiDate(this.dateTo) : undefined
    };

    this.sub?.unsubscribe();
    this.sub = this.drawsService.getDraws(filter).subscribe({
      next: result => {
        this.draws = result.items;
        this.page = result.page;
        this.pageSize = result.pageSize;
        this.totalCount = result.totalCount;
        this.totalPages = result.totalPages;
        this.isLoading = false;
      },
      error: err => {
        console.error('Erreur lors du chargement des tirages', err);
        this.errorMessage = 'Une erreur est survenue lors du chargement des tirages.';
        this.isLoading = false;
      }
    });
  }

  onPageChange(newPage: number): void {
    if (newPage < 1 || (this.totalPages > 0 && newPage > this.totalPages)) {
      return;
    }
    this.page = newPage;
    this.loadDraws();
  }

  onPageSizeChange(newSize: number): void {
    this.pageSize = newSize;
    this.page = 1;
    this.loadDraws();
  }

  onDateRangeChanged(): void {
    this.page = 1;
    this.loadDraws();
  }

  applyPreset(preset: '7d' | '30d' | '6m' | '1y'): void {
    const now = new Date();
    const end = new Date(now);
    const start = new Date(now);

    switch (preset) {
      case '7d':
        start.setDate(now.getDate() - 7);
        break;
      case '30d':
        start.setDate(now.getDate() - 30);
        break;
      case '6m':
        start.setMonth(now.getMonth() - 6);
        break;
      case '1y':
        start.setFullYear(now.getFullYear() - 1);
        break;
    }

    this.dateFrom = start;
    this.dateTo = end;
    this.page = 1;
    this.loadDraws();
  }

  clearDateFilter(): void {
    this.dateFrom = null;
    this.dateTo = null;
    this.page = 1;
    this.loadDraws();
  }

  retry(): void {
    this.loadDraws();
  }

  private toApiDate(date: Date): string {
    const y = date.getFullYear();
    const m = (date.getMonth() + 1).toString().padStart(2, '0');
    const d = date.getDate().toString().padStart(2, '0');
    return `${y}-${m}-${d}`;
  }
}
