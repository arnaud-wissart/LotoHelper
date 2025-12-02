import { Component } from '@angular/core';
import { MatchDistribution, StrategyBacktestRequest, StrategyBacktestResult } from '../../models/strategy-analysis.models';
import { PredictionStrategy } from '../../models/predictions.model';
import { StrategyAnalysisService } from '../../services/strategy-analysis.service';

@Component({
  selector: 'app-strategy-analysis',
  templateUrl: './strategy-analysis.component.html',
  styleUrls: ['./strategy-analysis.component.scss'],
  standalone: false
})
export class StrategyAnalysisComponent {
  strategies: { value: PredictionStrategy; label: string; detail: string }[] = [
    { value: 'Uniform', label: 'Uniforme', detail: 'Distribution parfaitement uniforme, sans prendre en compte l’historique.' },
    { value: 'FrequencyGlobal', label: 'Fréquentiel global', detail: 'Poids basés sur toutes les occurrences passées.' },
    { value: 'FrequencyRecent', label: 'Fréquentiel récent', detail: 'Même principe mais uniquement sur une fenêtre récente.' },
    { value: 'Cold', label: 'Numéros en retard', detail: 'Favorise les numéros peu sortis dernièrement.' },
    { value: 'Cooccurrence', label: 'Co-occurrence', detail: 'Tente de recomposer des paires/combinaisons souvent vues ensemble.' }
  ];

  selectedStrategy: PredictionStrategy = 'FrequencyGlobal';
  dateFrom?: string;
  dateTo?: string;
  sampleSize?: number | null;

  result?: StrategyBacktestResult;
  isLoading = false;
  errorMessage: string | null = null;

  readonly mainBuckets = [0, 1, 2, 3, 4, 5];

  constructor(private readonly strategyAnalysisService: StrategyAnalysisService) {}

  getStrategyLabel(strategy: PredictionStrategy): string {
    return this.strategies.find(s => s.value === strategy)?.label ?? strategy;
  }

  runAnalysis(): void {
    const payload: StrategyBacktestRequest = {
      strategy: this.selectedStrategy,
      dateFrom: this.dateFrom || undefined,
      dateTo: this.dateTo || undefined,
      sampleSize: this.sampleSize && this.sampleSize > 0 ? this.sampleSize : undefined
    };

    this.isLoading = true;
    this.errorMessage = null;

    this.strategyAnalysisService.backtest(payload).subscribe({
      next: res => {
        this.result = res;
        this.isLoading = false;
      },
      error: err => {
        console.error('Erreur lors du backtest', err);
        this.errorMessage = 'Impossible de lancer l’analyse pour le moment.';
        this.isLoading = false;
      }
    });
  }

  get distributionRows(): MatchDistribution[] {
    if (!this.result) {
      return [];
    }

    return [...this.result.distributions].sort((a, b) => {
      if (a.matchedMain !== b.matchedMain) {
        return b.matchedMain - a.matchedMain;
      }

      if (a.matchedLucky === b.matchedLucky) {
        return 0;
      }

      return a.matchedLucky ? -1 : 1;
    });
  }

  getMainCount(main: number): number {
    if (!this.result) {
      return 0;
    }

    return this.result.distributions
      .filter(d => d.matchedMain === main)
      .reduce((acc, curr) => acc + curr.count, 0);
  }

  getMainBarWidth(main: number): number {
    const max = this.getMaxMainCount();
    if (max <= 0) {
      return 0;
    }

    return (this.getMainCount(main) / max) * 100;
  }

  private getMaxMainCount(): number {
    const counts = this.mainBuckets.map(b => this.getMainCount(b));
    return counts.length ? Math.max(...counts, 0) : 0;
  }

  getCount(main: number, lucky: boolean): number {
    if (!this.result) {
      return 0;
    }

    return (
      this.result.distributions.find(d => d.matchedMain === main && d.matchedLucky === lucky)?.count ?? 0
    );
  }

  getPercentage(count: number): string {
    if (!this.result || this.result.totalDrawsAnalyzed === 0) {
      return '0 %';
    }

    const pct = (count / this.result.totalDrawsAnalyzed) * 100;
    return `${pct.toFixed(1)} %`;
  }

  formatDate(value?: string | null): string | undefined {
    if (!value) {
      return undefined;
    }

    const parsed = new Date(value);
    if (isNaN(parsed.getTime())) {
      return value;
    }

    return parsed.toLocaleDateString('fr-FR');
  }
}
