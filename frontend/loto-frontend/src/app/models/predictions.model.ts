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
