export type PredictionStrategy =
  | 'Uniform'
  | 'FrequencyGlobal'
  | 'FrequencyRecent'
  | 'Cold'
  | 'Cooccurrence';

export interface PredictionRequestDto {
  count: number;
  strategy: PredictionStrategy;

  minSum?: number;
  maxSum?: number;

  minEven?: number;
  maxEven?: number;

  minLow?: number;
  maxLow?: number;

  includeNumbers?: number[];
  excludeNumbers?: number[];
}

export interface PredictedDraw {
  numbers: number[];
  luckyNumber: number;
  score: number;
}

export interface PredictionsResponse {
  generatedAtUtc: string;
  count: number;
  draws: PredictedDraw[];
}
