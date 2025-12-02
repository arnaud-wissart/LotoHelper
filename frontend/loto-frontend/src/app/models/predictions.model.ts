export type PredictionStrategy =
  | 'Uniform'
  | 'FrequencyGlobal'
  | 'FrequencyRecent'
  | 'Cold'
  | 'Cooccurrence';

export interface PredictionRequestDto {
  count: number;
  strategy: PredictionStrategy;
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
