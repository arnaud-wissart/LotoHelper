import { Component, OnInit } from '@angular/core';
import { forkJoin } from 'rxjs';
import {
  CooccurrenceStats,
  NumberFrequency,
  PatternDistribution,
  StatsFrequencies,
  StatsOverview
} from '../../../models/stats.models';
import { StatsService } from '../../../services/stats.service';

@Component({
  selector: 'app-stats-page',
  templateUrl: './stats-page.component.html',
  styleUrls: ['./stats-page.component.scss'],
  standalone: false
})
export class StatsPageComponent implements OnInit {
  overview?: StatsOverview;
  frequencies?: StatsFrequencies;
  patterns?: PatternDistribution;
  cooccurrence?: CooccurrenceStats;

  isLoading = false;
  isLoadingPatterns = false;
  isLoadingCooccurrence = false;
  errorMessage: string | null = null;

  hotMainNumbers: NumberFrequency[] = [];
  coldMainNumbers: NumberFrequency[] = [];
  hotLuckyNumbers: NumberFrequency[] = [];
  coldLuckyNumbers: NumberFrequency[] = [];

  selectedBaseNumber = 13;
  cooccurrenceTop = 15;
  numbersRange: number[] = Array.from({ length: 49 }, (_, i) => i + 1);

  constructor(private readonly statsService: StatsService) {}

  ngOnInit(): void {
    this.loadStats();
    this.loadCooccurrence();
  }

  loadStats(): void {
    this.isLoading = true;
    this.isLoadingPatterns = true;
    this.errorMessage = null;

    forkJoin({
      overview: this.statsService.getOverview(),
      frequencies: this.statsService.getFrequencies(),
      patterns: this.statsService.getPatterns(10)
    }).subscribe({
      next: ({ overview, frequencies, patterns }) => {
        this.overview = overview;
        this.frequencies = frequencies;
        this.patterns = patterns;

        this.hotMainNumbers = frequencies.mainNumbers.slice(0, 10);
        this.coldMainNumbers = [...frequencies.mainNumbers].reverse().slice(0, 10);

        this.hotLuckyNumbers = frequencies.luckyNumbers.slice(0, 5);
        this.coldLuckyNumbers = [...frequencies.luckyNumbers].reverse().slice(0, 5);

        this.isLoading = false;
        this.isLoadingPatterns = false;
      },
      error: err => {
        console.error(err);
        this.errorMessage = 'Impossible de charger les statistiques.';
        this.isLoading = false;
        this.isLoadingPatterns = false;
      }
    });
  }

  loadCooccurrence(): void {
    this.isLoadingCooccurrence = true;

    this.statsService.getCooccurrence(this.selectedBaseNumber, this.cooccurrenceTop).subscribe({
      next: stats => {
        this.cooccurrence = stats;
        this.isLoadingCooccurrence = false;
      },
      error: err => {
        console.error(err);
        this.isLoadingCooccurrence = false;
      }
    });
  }

  getKeys(dict: { [key: number]: number } | undefined): number[] {
    if (!dict) {
      return [];
    }

    return Object.keys(dict)
      .map(k => +k)
      .sort((a, b) => a - b);
  }

  getMaxBucketCount(): number {
    if (!this.patterns || this.patterns.sumBuckets.length === 0) {
      return 1;
    }

    return Math.max(...this.patterns.sumBuckets.map(b => b.count), 1);
  }

  getMaxDistributionValue(dict: { [key: number]: number } | undefined): number {
    if (!dict) {
      return 1;
    }

    const values = Object.values(dict);
    return values.length ? Math.max(...values, 1) : 1;
  }

  getRelativeWidth(value: number, max: number): number {
    if (max <= 0) {
      return 0;
    }

    return (value / max) * 100;
  }
}
