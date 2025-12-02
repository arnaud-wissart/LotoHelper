import { PredictionStrategy } from './predictions.model';

export interface StrategyBacktestRequest {
  strategy: PredictionStrategy;
  dateFrom?: string;
  dateTo?: string;
  sampleSize?: number;
}

export interface MatchDistribution {
  matchedMain: number;
  matchedLucky: boolean;
  count: number;
}

export interface StrategyBacktestResult {
  strategy: PredictionStrategy;
  from?: string | null;
  to?: string | null;
  totalDrawsAnalyzed: number;
  averageMatchedMain: number;
  distributions: MatchDistribution[];
}
