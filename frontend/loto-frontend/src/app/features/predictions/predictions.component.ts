import { Component } from '@angular/core';
import { PredictedDraw, PredictionStrategy, PredictionsResponse } from '../../models/predictions.model';
import { PredictionsService } from '../../services/predictions.service';

@Component({
  selector: 'app-predictions',
  templateUrl: './predictions.component.html',
  styleUrls: ['./predictions.component.scss'],
  standalone: false
})
export class PredictionsComponent {
  count = 10;
  minCount = 1;
  maxCount = 100;

  strategies: { value: PredictionStrategy; label: string }[] = [
    { value: 'Uniform',           label: 'Uniforme' },
    { value: 'FrequencyGlobal',   label: 'Fréquentiel global' },
    { value: 'FrequencyRecent',   label: 'Fréquentiel récent' },
    { value: 'Cold',              label: 'Numéros en retard' },
    { value: 'Cooccurrence',      label: 'Co-occurrence' }
  ];

  selectedStrategy: PredictionStrategy = 'FrequencyGlobal';
  lastUsedStrategy: PredictionStrategy | null = null;

  isLoading = false;
  isAnimating = false;
  errorMessage: string | null = null;

  predictions: PredictedDraw[] = [];
  generatedAtUtc?: string;

  animationDurationMs = 2000;

  constructor(private readonly predictionsService: PredictionsService) { }

  onGenerateClick(): void {
    if (this.count < this.minCount || this.count > this.maxCount) {
      return;
    }

    this.isLoading = true;
    this.isAnimating = true;
    this.errorMessage = null;

    const startedAt = Date.now();

    this.predictionsService.generate(this.count, this.selectedStrategy).subscribe({
      next: (response: PredictionsResponse) => {
        const remaining = this.animationDurationMs - (Date.now() - startedAt);
        const delay = remaining > 0 ? remaining : 0;

        setTimeout(() => {
          this.isLoading = false;
          this.isAnimating = false;
          this.predictions = response.draws;
          this.generatedAtUtc = response.generatedAtUtc;
          this.lastUsedStrategy = this.selectedStrategy;
        }, delay);
      },
      error: err => {
        console.error('Erreur lors de la génération des prédictions', err);
        this.isLoading = false;
        this.isAnimating = false;
        this.errorMessage = 'Une erreur est survenue lors du calcul des prédictions.';
      }
    });
  }

  trackByIndex(index: number, _item: PredictedDraw): number {
    return index;
  }

  formatGeneratedAt(): string | undefined {
    if (!this.generatedAtUtc) {
      return undefined;
    }

    const date = new Date(this.generatedAtUtc);
    return `${date.toLocaleDateString('fr-FR')} à ${date.toLocaleTimeString('fr-FR')}`;
  }

  getScoreAsPercent(score: number): string {
    const pct = Math.round(score * 100);
    return `${pct} %`;
  }

  getStrategyLabel(strategy: PredictionStrategy | null): string | undefined {
    const match = this.strategies.find(s => s.value === strategy);
    return match?.label;
  }
}
